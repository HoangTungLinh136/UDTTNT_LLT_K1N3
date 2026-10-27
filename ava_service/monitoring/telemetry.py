from __future__ import annotations

import json
import os
import re

from datetime import datetime, timezone
from pathlib import Path
from threading import Lock
from typing import Any, Optional


class TelemetryService:
    """
    JSONL telemetry an toÃ n cho AI-HRMS.

    NguyÃªn táº¯c:
    - KhÃ´ng log prompt/question.
    - KhÃ´ng log answer.
    - KhÃ´ng log payload DB.
    - KhÃ´ng log token/API key/password/connection string.
    - Logging lá»—i khÃ´ng Ä‘Æ°á»£c lÃ m há»ng request chÃ­nh.
    """

    SECRET_PATTERNS = [
        (
            re.compile(
                r"(?i)\b(api[_ -]?key|secret|token|"
                r"access[_ -]?token|refresh[_ -]?token|"
                r"password|pwd|pass)\b"
                r"\s*[:=]\s*([^\s,;]+)"
            ),
            r"\1=[REDACTED]",
        ),
        (
            re.compile(
                r"\b(?:sk|gsk)_[A-Za-z0-9_-]{12,}\b"
            ),
            "[REDACTED_SECRET]",
        ),
        (
            re.compile(
                r"(?i)\b("
                r"Server|Data Source|Database|Initial Catalog|"
                r"User ID|UID|Password|Pwd"
                r")\s*=\s*([^;]+)"
            ),
            r"\1=[REDACTED]",
        ),
        (
            re.compile(
                r"(?i)\b[A-Z]:\\"
                r"(?:Users|ProgramData|Windows|inetpub|"
                r"private|internal|secrets)"
                r"\\[^\r\n\t<>\"|?*]+"
            ),
            "[REDACTED_INTERNAL_PATH]",
        ),
    ]

    def __init__(
        self,
        log_file: Optional[str] = None,
    ):

        if log_file:

            self.log_file = os.path.abspath(
                log_file
            )

            self.log_dir = os.path.dirname(
                self.log_file
            )

        else:

            base_dir = os.path.dirname(
                os.path.dirname(
                    os.path.abspath(__file__)
                )
            )

            self.log_dir = os.path.join(
                base_dir,
                "logs",
            )

            self.log_file = os.path.join(
                self.log_dir,
                "ai_requests.jsonl",
            )

        os.makedirs(
            self.log_dir,
            exist_ok=True,
        )

        self._lock = Lock()

    # =====================================================
    # SANITIZE
    # =====================================================

    def _sanitize_text(
        self,
        value: Optional[str],
    ) -> Optional[str]:

        if value is None:
            return None

        cleaned = str(value)

        for pattern, replacement in (
            self.SECRET_PATTERNS
        ):

            cleaned = pattern.sub(
                replacement,
                cleaned,
            )

        # Telemetry fields should be compact.
        if len(cleaned) > 500:
            cleaned = (
                cleaned[:500]
                + "...[TRUNCATED]"
            )

        return cleaned

    def _sanitize_value(
        self,
        value: Any,
    ) -> Any:

        if value is None:
            return None

        if isinstance(
            value,
            (
                str,
                int,
                float,
                bool,
            ),
        ):

            if isinstance(value, str):
                return self._sanitize_text(
                    value
                )

            return value

        # KhÃ´ng serialize payload object phá»©c táº¡p.
        # Chá»‰ lÆ°u tÃªn type Ä‘á»ƒ trÃ¡nh rÃ² dá»¯ liá»‡u.
        return (
            f"<{type(value).__name__}>"
        )

    # =====================================================
    # EVENT STATUS
    # =====================================================

    def _resolve_status(
        self,
        *,
        safety_decision: Optional[str],
        verification_status: Optional[str],
        warning: Optional[str],
        error_type: Optional[str],
    ) -> str:

        if error_type:
            return "ERROR"

        safety = (
            safety_decision
            or ""
        ).upper()

        if safety in {
            "BLOCK",
            "REQUIRE_AUTH",
        }:
            return "BLOCKED"

        verification = (
            verification_status
            or ""
        ).upper()

        if (
            verification
            in {
                "FAIL",
                "WARNING",
            }
            or warning
        ):
            return "WARNING"

        return "OK"

    # =====================================================
    # WRITE
    # =====================================================

    def _write_event(
        self,
        event: dict[str, Any],
    ) -> None:

        safe_event = {
            key: self._sanitize_value(
                value
            )
            for key, value
            in event.items()
        }

        line = json.dumps(
            safe_event,
            ensure_ascii=False,
            default=str,
        )

        with self._lock:

            with open(
                self.log_file,
                "a",
                encoding="utf-8",
            ) as file:

                file.write(
                    line + "\n"
                )

    # =====================================================
    # RAG RETRIEVAL EVENT
    #
    # Chá»‰ log metrics ká»¹ thuáº­t, khÃ´ng log query/document.
    # =====================================================

    def log_rag_retrieval(
        self,
        *,
        request_id: str,
        trace_id: str,
        candidate_count: int,
        evidence_count: int,
        candidate_top_k: int,
        final_top_k: int,
        max_distance: float,
        top_distance: Optional[float],
        retrieval_ms: float,
        vector_search_ms: float,
        query_variants: int,
        no_evidence: bool,
        source_count: int = 0,
        citation_count: int = 0,
        metadata_filtered_count: int = 0,
        permission_filtered_count: int = 0,
        permission_denied_count: int = 0,
        pre_rerank_count: int = 0,
        post_rerank_count: int = 0,
        evidence_pruned_count: int = 0,
        best_rerank_score: Optional[float] = None,
        best_lexical_affinity: Optional[float] = None,
    ) -> None:

        event: dict[str, Any] = {
            "timestamp": datetime.now(
                timezone.utc
            ).isoformat(),

            "event_type": (
                "AI_RAG_RETRIEVAL"
            ),

            "request_id": request_id,
            "trace_id": trace_id,

            "route": "RAG",

            "candidate_count": int(
                candidate_count
            ),

            "evidence_count": int(
                evidence_count
            ),

            "candidate_top_k": int(
                candidate_top_k
            ),

            "final_top_k": int(
                final_top_k
            ),

            "max_distance": float(
                max_distance
            ),

            "top_distance": (
                float(top_distance)
                if top_distance
                is not None
                else None
            ),

            "retrieval_ms": round(
                float(retrieval_ms),
                2,
            ),

            "vector_search_ms": round(
                float(vector_search_ms),
                2,
            ),

            "query_variants": int(
                query_variants
            ),

            "no_evidence": bool(
                no_evidence
            ),

            "source_count": int(
                source_count
            ),

            "citation_count": int(
                citation_count
            ),

            "metadata_filtered_count": int(
                metadata_filtered_count
            ),

            "permission_filtered_count": int(
                permission_filtered_count
            ),

            "permission_denied_count": int(
                permission_denied_count
            ),

            "pre_rerank_count": int(
                pre_rerank_count
            ),

            "post_rerank_count": int(
                post_rerank_count
            ),

            "evidence_pruned_count": int(
                evidence_pruned_count
            ),

            "best_rerank_score": (
                float(best_rerank_score)
                if best_rerank_score
                is not None
                else None
            ),

            "best_lexical_affinity": (
                float(best_lexical_affinity)
                if best_lexical_affinity
                is not None
                else None
            ),
        }

        self._write_event(
            event
        )

    # =====================================================
    # REQUEST EVENT
    # =====================================================

    def log_request(
        self,
        *,
        request_id: str,
        trace_id: Optional[str] = None,
        route: Optional[str],
        intent: Optional[str],
        latency_ms: float,
        safety_decision: Optional[str],
        verification_status: Optional[str],
        warning: Optional[str],
        error_type: Optional[str] = None,
        safety_reason: Optional[str] = None,
        provider: Optional[str] = None,
        model: Optional[str] = None,
        status: Optional[str] = None,
        prompt_tokens: Optional[int] = None,
        completion_tokens: Optional[int] = None,
        total_tokens: Optional[int] = None,
        estimated_cost_usd: Optional[float] = None,
    ) -> None:

        resolved_status = (
            status
            or
            self._resolve_status(
                safety_decision=(
                    safety_decision
                ),
                verification_status=(
                    verification_status
                ),
                warning=warning,
                error_type=error_type,
            )
        )

        event: dict[str, Any] = {
            "timestamp": datetime.now(
                timezone.utc
            ).isoformat(),

            "event_type": "AI_REQUEST",

            "request_id": request_id,

            "trace_id": (
                trace_id
                or request_id
            ),

            "status": resolved_status,

            "route": route,

            "intent": intent,

            "provider": provider,

            "model": model,

            "latency_ms": round(
                float(latency_ms),
                2,
            ),

            "safety_decision":
                safety_decision,

            "safety_reason":
                safety_reason,

            "verification_status":
                verification_status,

            "warning": warning,

            "error_type": error_type,

            # Phase 19C:
            # chỉ scalar usage kỹ thuật từ provider.
            # Không log prompt/answer/payload.
            "prompt_tokens": (
                int(prompt_tokens)
                if prompt_tokens is not None
                else None
            ),

            "completion_tokens": (
                int(completion_tokens)
                if completion_tokens is not None
                else None
            ),

            "total_tokens": (
                int(total_tokens)
                if total_tokens is not None
                else None
            ),

            # Chỉ có giá trị khi pricing đã được xác minh
            # và caller chủ động truyền vào.
            "estimated_cost_usd": (
                float(estimated_cost_usd)
                if estimated_cost_usd is not None
                else None
            ),
        }

        self._write_event(
            event
        )
