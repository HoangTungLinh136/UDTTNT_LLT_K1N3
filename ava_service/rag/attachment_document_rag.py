from __future__ import annotations

import re
from functools import lru_cache
from typing import Any

import numpy as np

from core.models import AttachmentRef
from rag.embedding_service import EmbeddingService


@lru_cache(maxsize=1)
def _embedder() -> EmbeddingService:
    return EmbeddingService()


def _chunks(text: str, file_name: str) -> list[dict[str, Any]]:
    sections = re.split(r"(?=\[(?:PAGE|SHEET|TABLE|SECTION)\s+[^\]]+\])", text or "")
    results: list[dict[str, Any]] = []
    for section in sections:
        section = section.strip()
        if not section:
            continue
        marker = re.match(r"\[(PAGE|SHEET|TABLE|SECTION)\s+([^\]]+)\]", section)
        kind, label = (marker.group(1).lower(), marker.group(2).strip()) if marker else ("section", "")
        body = section[marker.end():].strip() if marker else section
        start = 0
        while start < len(body) and len(results) < 500:
            end = min(start + 700, len(body))
            if end < len(body):
                boundary = body.rfind("\n", start + 450, end)
                if boundary > start:
                    end = boundary
            value = body[start:end].strip()
            if value:
                results.append({"text": value, "file_name": file_name, "section": label or None,
                                "page": label if kind == "page" else None, "kind": kind})
            if end >= len(body):
                break
            start = max(start + 1, end - 100)
    return results


def _is_broad_document_request(question: str) -> bool:
    value = re.sub(r"\s+", " ", (question or "").strip().casefold())
    return bool(re.search(
        r"\b(tóm tắt|đọc(?: và)? tóm tắt|phân tích (?:tài liệu|file)|"
        r"nội dung (?:của )?(?:tài liệu|file)|(?:tài liệu|file) (?:này )?(?:nói|viết) gì)\b",
        value,
    ))


def retrieve_attachment_evidence(question: str, attachments: list[AttachmentRef], top_k: int = 8) -> list[dict[str, Any]]:
    chunks: list[dict[str, Any]] = []
    for attachment in attachments:
        text = attachment.extracted_text or ""
        visual = attachment.metadata.get("visual_observation")
        if isinstance(visual, str) and visual.strip():
            text = f"{text}\n[SECTION Visual description]\n{visual}"
        chunks.extend(_chunks(text, attachment.file_name or attachment.file_id or "attachment"))
    if not chunks:
        return []
    if len(chunks) > 500:
        chunks = chunks[:500]
    if _is_broad_document_request(question):
        # A summary needs representative coverage, not only chunks whose
        # embeddings happen to resemble the generic words "read/summarize".
        count = min(top_k, len(chunks))
        indices = np.linspace(0, len(chunks) - 1, num=count, dtype=int)
        return [
            {**chunks[int(index)], "score": 1.0, "selection": "document_coverage"}
            for index in dict.fromkeys(indices.tolist())
        ]
    embedder = _embedder()
    query = np.asarray(embedder.embed_query(question or "Tóm tắt tài liệu"), dtype=np.float32)
    vectors = np.asarray(embedder.embed_documents([item["text"] for item in chunks]), dtype=np.float32)
    scores = vectors @ query
    indices = np.argsort(scores)[::-1][:min(top_k, len(chunks))]
    return [{**chunks[int(index)], "score": round(float(scores[int(index)]), 5)} for index in indices]
