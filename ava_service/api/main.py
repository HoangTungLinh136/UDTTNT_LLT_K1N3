from __future__ import annotations

import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import base64
import binascii
import re
from dataclasses import asdict
from functools import lru_cache
from typing import Any

from fastapi import (
    Depends,
    FastAPI,
    HTTPException,
)
from fastapi.concurrency import run_in_threadpool

from api.schemas import (
    ChatRequest,
    ChatResponse,
    TranscribeRequest,
)
from api.security import require_internal_service_key
from config.settings import settings
from core.conversation_state import (
    build_conversation_state,
)
from core.enums import InputModality
from core.models import (
    AttachmentRef,
    NormalizedAiInput,
    UserContext,
)
from core.orchestrator import AiOrchestrator
from database.connection import (
    get_sql_connection,
    get_sql_driver,
)
from preprocessing.multimodal import (
    MultimodalPreprocessor,
    MultimodalRequest,
    PreprocessingError,
    RawAttachment,
)
from preprocessing.local_multimodal import local_capability_status
from rag.attachment_document_rag import retrieve_attachment_evidence


app = FastAPI(
    title="AVA HRMS Internal Service",
    docs_url=None,
    redoc_url=None,
)


@lru_cache(maxsize=1)
def get_orchestrator() -> AiOrchestrator:
    """
    Khởi tạo một AiOrchestrator dùng lại giữa các request.

    Core AVA chịu trách nhiệm:
    - Context / Intent
    - Routing
    - RAG
    - SQL
    - Personal Data
    - General LLM
    - Verifier

    Authorization cuối cùng vẫn thuộc backend/code.
    """

    return AiOrchestrator()


def _response_payload(
    response,
    conversation_id: str | None,
) -> dict:
    route = (
        response.route.value
        if response.route is not None
        else None
    )

    safety = asdict(
        response.safety
    )

    if safety.get("decision") is not None:
        safety["decision"] = (
            response.safety.decision.value
        )

    verification = asdict(
        response.verification
    )

    if verification.get("status") is not None:
        verification["status"] = (
            response.verification.status.value
        )

    payload = {
        "requestId": response.request_id,
        "traceId": response.trace_id,
        "conversationId": conversation_id,
        "answer": response.answer,
        "route": route,
        "intent": response.intent,
        "sources": [
            asdict(item)
            for item in response.sources
        ],
        "citations": [
            asdict(item)
            for item in response.citations
        ],
        "warning": response.warning,
        "canSpeak": response.can_speak,
        "verification": verification,
        "safety": safety,
    }
    environment = os.getenv("AVA_ENVIRONMENT", "production").strip().lower()
    if settings.DIAGNOSTICS_ENABLED and environment == "development" and response.diagnostics:
        payload["diagnostics"] = response.diagnostics
    return payload


def _build_normalized_input(request: ChatRequest) -> NormalizedAiInput:
    original_user_text = request.text if request.user_text is None else request.user_text
    try:
        modality = InputModality(
            request.modality.upper()
        )
    except ValueError:
        modality = InputModality.TEXT

    if any(item.content_base64 for item in request.attachments):
        raw_attachments = []
        aggregate_bytes = 0
        for item in request.attachments:
            content = None
            if item.content_base64:
                try:
                    content = base64.b64decode(item.content_base64, validate=True)
                except (binascii.Error, ValueError) as exc:
                    raise HTTPException(status_code=422, detail="ATTACHMENT_BASE64_INVALID") from exc
                aggregate_bytes += len(content)
                if aggregate_bytes > 20 * 1024 * 1024:
                    raise HTTPException(status_code=413, detail="ATTACHMENT_TOO_LARGE")
            raw_attachments.append(
                RawAttachment(
                    file_id=item.file_id,
                    file_name=item.file_name,
                    mime_type=item.mime_type,
                    content_bytes=content,
                    metadata=item.metadata,
                )
            )
        binary_attachments = [item for item in raw_attachments if item.content_bytes is not None]
        text_only_attachments = [item for item in raw_attachments if item.content_bytes is None]
        try:
            processed = MultimodalPreprocessor().preprocess(
                MultimodalRequest(
                    text=request.text,
                    modality=modality,
                    attachments=binary_attachments,
                )
            )
        except PreprocessingError as exc:
            code = str(exc)
            status = 413 if code in {"ATTACHMENT_TOO_LARGE", "TOO_MANY_ATTACHMENTS"} else 422
            raise HTTPException(status_code=status, detail=code) from exc
        attachments = processed.attachments + [
            AttachmentRef(
                file_id=item.file_id,
                file_name=item.file_name,
                mime_type=item.mime_type,
                extracted_text=item.extracted_text,
                metadata=item.metadata,
            )
            for item in text_only_attachments
        ]
        modalities_present = sorted({
            "IMAGE" if (item.mime_type or "").startswith("image/")
            else "VOICE" if (item.mime_type or "").startswith("audio/")
            else "DOCUMENT"
            for item in attachments
        })
        if "IMAGE" in modalities_present:
            modality = InputModality.IMAGE
        elif "VOICE" in modalities_present:
            modality = InputModality.VOICE
        elif "DOCUMENT" in modalities_present:
            modality = InputModality.DOCUMENT
        text_only_context = [
            f"[SOURCE {(item.file_name or item.file_id or 'attachment')[:180]}]\n"
            + MultimodalPreprocessor.normalize_text(item.extracted_text or "")
            for item in text_only_attachments
            if item.extracted_text
        ]
        if text_only_context:
            attachments = [*attachments]
            for reference, raw in zip(attachments[len(processed.attachments):], text_only_attachments):
                reference.extracted_text = MultimodalPreprocessor.normalize_text(raw.extracted_text or "")[:100_000]
        try:
            # P3 IMAGE/DOCUMENT ROUTING FIX
            # Chỉ document upload mới tự tạo evidence RAG.
            # Image/Voice phải đi qua Context/Intent bình thường.
            document_evidence = (
                retrieve_attachment_evidence(
                    request.text,
                    [
                        item for item in attachments
                        if not (item.mime_type or "").startswith(("image/", "audio/"))
                    ],
                )
                if "DOCUMENT" in modalities_present
                else []
            )
        except Exception as exc:
            raise HTTPException(status_code=503, detail="DOCUMENT_RAG_UNAVAILABLE") from exc
        # P3 IMAGE CONTEXT PRESERVATION
        # Binary IMAGE đã được OCR/Vision trong processed.text.
        # Không được bỏ kết quả preprocessing trước Context/Intent.
        normalized_text = (
            processed.text
            if modality in {InputModality.IMAGE, InputModality.VOICE}
            or len(modalities_present) > 1
            else MultimodalPreprocessor.normalize_text(request.text)
        )
        metadata = {
            **processed.metadata,
            "document_rag_evidence": document_evidence,
            "modalities_present": modalities_present,
            "user_text": original_user_text,
            "effective_prompt": request.text,
        }
        return NormalizedAiInput(
            text=normalized_text,
            modality=modality,
            attachments=attachments,
            metadata=metadata,
        )

    attachments = [
        AttachmentRef(
            file_id=item.file_id,
            file_name=item.file_name,
            mime_type=item.mime_type,
            extracted_text=item.extracted_text,
            metadata=item.metadata,
        )
        for item in request.attachments
    ]
    attachment_parts: list[str] = []

    for attachment in attachments:
        if attachment.extracted_text:
            attachment_parts.append(
                MultimodalPreprocessor.normalize_text(
                    attachment.extracted_text
                )
            )

        visual = attachment.metadata.get("visual_observation")
        if isinstance(visual, str) and visual.strip():
            attachment_parts.append(
                MultimodalPreprocessor.normalize_text(visual)[:20_000]
            )

    normalized_text = MultimodalPreprocessor.normalize_text(request.text)
    if attachment_parts:
        # Keep the routed/contextual text within the orchestrator input limit.
        # Full extracted text remains on AttachmentRef for guardrails and
        # downstream adapters; only a bounded context preview is concatenated.
        context_budget = max(0, 7_500 - len(normalized_text))
        attachment_preview = "\n\n".join(attachment_parts)[
            :context_budget
        ]
        normalized_text = (
            f"{normalized_text}\n\n[ATTACHMENT_DATA]\n"
            + attachment_preview
        ).strip()

    return NormalizedAiInput(
        text=normalized_text,
        modality=modality,
        attachments=attachments,
        metadata={
            "preprocessed": True,
            "attachment_count": len(attachments),
            "uploaded_content_is_data": True,
            "modalities_present": sorted({
                "IMAGE" if (item.mime_type or "").startswith("image/")
                else "VOICE" if (item.mime_type or "").startswith("audio/")
                else "DOCUMENT"
                for item in attachments
            }),
            "user_text": original_user_text,
            "effective_prompt": request.text,
        },
    )


def _safe_attachment_context(normalized: NormalizedAiInput) -> dict[str, Any]:
    items: list[dict[str, Any]] = []
    for attachment in normalized.attachments[:5]:
        visual = attachment.metadata.get("visual_observation")
        items.append({
            "file_name": (attachment.file_name or "")[:180],
            "mime_type": (attachment.mime_type or "")[:120],
            "extracted_text": (attachment.extracted_text or "")[:6_000],
            "visual_observation": visual[:3_000] if isinstance(visual, str) else "",
        })
    return {
        "attachment_count": len(items),
        "modalities_present": normalized.metadata.get("modalities_present", []),
        "items": items,
    }


def _process(
    request: ChatRequest,
):
    normalized = _build_normalized_input(request)

    user = UserContext(
        **request.user_context.model_dump()
    )

    history = [
        item.model_dump()
        for item in request.history
    ]

    user_memories = [
        item.model_dump()
        for item in request.user_memories
    ]

    state = build_conversation_state(
        history=history,
        conversation_summary=(
            request.conversation_summary
        ),
        user_memories=user_memories,
    )
    current_attachment_context = _safe_attachment_context(normalized)
    previous_attachment_context = next(
        (
            item.attachment_context
            for item in reversed(request.history)
            if item.attachment_context
        ),
        {},
    )
    state.attachment_context = (
        current_attachment_context
        if normalized.attachments
        else previous_attachment_context
    )

    # Follow-ups reuse only bounded extracted/visual data, never raw bytes.
    if not normalized.attachments and previous_attachment_context:
        context_parts: list[str] = []
        for item in previous_attachment_context.get("items", [])[:5]:
            if not isinstance(item, dict):
                continue
            label = str(item.get("file_name") or "attachment")[:180]
            extracted = str(item.get("extracted_text") or "")[:6_000]
            visual = str(item.get("visual_observation") or "")[:3_000]
            if extracted:
                context_parts.append(f"[SOURCE {label}]\n{extracted}")
            if visual:
                context_parts.append(f"[VISUAL_OBSERVATION {label}]\n{visual}")
        if context_parts:
            normalized.text = (
                f"{normalized.text}\n\n[PREVIOUS_ATTACHMENT_CONTEXT]\n"
                + "\n\n".join(context_parts)
            )[:8_000]

        # A document follow-up must retrieve from the same bounded conversation
        # evidence. Explicit personal questions still have router priority.
        if re.search(r"tài liệu|văn bản|file|trong đó|nội dung đó", request.text, re.IGNORECASE):
            previous_documents = [
                AttachmentRef(
                    file_name=str(item.get("file_name") or "attachment")[:180],
                    mime_type=str(item.get("mime_type") or "text/plain")[:120],
                    extracted_text=str(item.get("extracted_text") or "")[:6_000],
                )
                for item in previous_attachment_context.get("items", [])[:5]
                if isinstance(item, dict)
                and not str(item.get("mime_type") or "").startswith(("image/", "audio/"))
            ]
            normalized.metadata["document_rag_evidence"] = retrieve_attachment_evidence(
                request.text, previous_documents
            )

    response = get_orchestrator().process(
        normalized,
        user,
        state,
    )

    if request.request_id:
        response.request_id = request.request_id

    if request.trace_id:
        response.trace_id = request.trace_id

    payload = _response_payload(
        response,
        request.conversation_id,
    )
    if normalized.attachments:
        payload["attachmentContext"] = current_attachment_context
    return payload


@app.get("/")
def root() -> dict[str, str]:
    return {
        "service": "AVA HRMS Internal Service",
        "status": "online",
        "healthCheck": "/health",
        "frontend": "http://127.0.0.1:5249"
    }


@app.get("/health")
def health() -> dict[str, Any]:
    """
    Liveness/configuration endpoint
    with a lightweight SQL probe.
    """

    sql_configured = bool(
        os.getenv(
            "SQL_DATABASE",
            "HRManagementDB",
        ).strip()
    )

    sql_connectivity = "unavailable"
    sql_driver = "unavailable"

    if sql_configured:
        try:
            sql_driver = get_sql_driver()

            with get_sql_connection() as connection:
                connection.cursor().execute(
                    "SELECT 1"
                )

            sql_connectivity = "ready"

        except Exception:
            sql_connectivity = "unavailable"

    capabilities = local_capability_status()
    unavailable_capabilities = [
        name
        for name, value in capabilities.items()
        if value.get("status") != "ready"
    ]

    return {
        "status": "ready",
        "fullyOperational": not unavailable_capabilities and sql_connectivity == "ready",
        "sql": (
            "configured"
            if sql_configured
            else "not_configured"
        ),
        "sqlConnectivity": sql_connectivity,
        "sqlDriver": sql_driver,
        "vector": (
            "configured"
            if os.getenv(
                "VECTOR_DB_DIR",
                "vector_db",
            )
            else "not_configured"
        ),
        "generalProvider": "ollama",
        "capabilities": capabilities,
        "unavailableCapabilities": unavailable_capabilities,
        "diagnosticProviders": [
            "ollama", "sqlserver", "rag_local", "vision_local", "stt_local", "parser_local"
        ],
    }


@app.post(
    "/api/v1/chat",
    response_model=ChatResponse,
    dependencies=[
        Depends(
            require_internal_service_key
        )
    ],
)
async def chat(
    request: ChatRequest,
) -> ChatResponse:
    payload = await run_in_threadpool(
        _process,
        request,
    )

    return ChatResponse.model_validate(
        payload
    )


@app.post(
    "/api/v1/transcribe",
    dependencies=[Depends(require_internal_service_key)],
)
async def transcribe(request: TranscribeRequest) -> dict[str, str]:
    try:
        content = base64.b64decode(request.content_base64, validate=True)
    except (binascii.Error, ValueError) as exc:
        raise HTTPException(status_code=422, detail="ATTACHMENT_BASE64_INVALID") from exc
    if len(content) > 20 * 1024 * 1024:
        raise HTTPException(status_code=413, detail="ATTACHMENT_TOO_LARGE")
    preprocessor = MultimodalPreprocessor()
    attachment = RawAttachment(
        file_name=request.file_name,
        mime_type=request.mime_type,
        content_bytes=content,
    )
    try:
        mime = MultimodalPreprocessor._validate_attachment(attachment)
        if mime not in MultimodalPreprocessor.AUDIO_MIME:
            raise PreprocessingError("UNSUPPORTED_MIME")
        transcript = await run_in_threadpool(preprocessor.speech_to_text, attachment)
    except PreprocessingError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    return {"transcript": MultimodalPreprocessor.normalize_text(transcript)}
