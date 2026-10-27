from __future__ import annotations

import hashlib
import re
import unicodedata


class EvidenceSelector:
    """
    Phase 10 - Final evidence selection.

    Chạy SAU reranker và TRƯỚC KnowledgeContextBuilder.

    Mục tiêu:
    - loại duplicate chunk;
    - tránh 1 source chiếm toàn bộ context;
    - bỏ chunk rỗng/quá ngắn;
    - giữ thứ tự relevance từ reranker;
    - không tự tạo/chỉnh sửa evidence content.
    """

    def __init__(
        self,
        *,
        max_items: int = 4,
        max_per_source: int = 2,
        min_document_chars: int = 20,
    ):

        self.max_items = max(
            1,
            int(max_items),
        )

        self.max_per_source = max(
            1,
            int(max_per_source),
        )

        self.min_document_chars = max(
            1,
            int(min_document_chars),
        )

    # =====================================================
    # NORMALIZE
    # =====================================================

    @staticmethod
    def _normalize_text(
        value: str | None,
    ) -> str:

        if not value:
            return ""

        text = unicodedata.normalize(
            "NFKC",
            str(value),
        )

        text = text.lower()

        text = re.sub(
            r"\s+",
            " ",
            text,
        )

        return text.strip()

    # =====================================================
    # DUPLICATE KEY
    # =====================================================

    @classmethod
    def _document_key(
        cls,
        item: dict,
    ) -> str:

        document = cls._normalize_text(
            item.get(
                "document"
            )
        )

        return hashlib.sha256(
            document.encode(
                "utf-8"
            )
        ).hexdigest()

    # =====================================================
    # SOURCE KEY
    # =====================================================

    @classmethod
    def _source_key(
        cls,
        item: dict,
    ) -> str:

        metadata = (
            item.get(
                "metadata"
            )
            or {}
        )

        if not isinstance(
            metadata,
            dict,
        ):
            return "unknown"

        raw = (
            metadata.get(
                "document_id"
            )
            or metadata.get(
                "source"
            )
            or metadata.get(
                "file_name"
            )
            or metadata.get(
                "title"
            )
            or "unknown"
        )

        return cls._normalize_text(
            str(raw)
        )

    # =====================================================
    # BASIC QUALITY
    # =====================================================

    def _is_usable(
        self,
        item: dict,
    ) -> bool:

        if not isinstance(
            item,
            dict,
        ):
            return False

        document = str(
            item.get(
                "document"
            )
            or ""
        ).strip()

        if (
            len(document)
            <
            self.min_document_chars
        ):
            return False

        return True

    # =====================================================
    # SELECT
    # =====================================================

    def select(
        self,
        evidence: list[dict],
    ) -> list[dict]:

        selected: list[dict] = []

        seen_documents: set[str] = set()

        source_counts: dict[
            str,
            int,
        ] = {}

        for item in (
            evidence
            or []
        ):

            if not self._is_usable(
                item
            ):
                continue

            document_key = (
                self._document_key(
                    item
                )
            )

            if (
                document_key
                in seen_documents
            ):
                continue

            source_key = (
                self._source_key(
                    item
                )
            )

            current_source_count = (
                source_counts.get(
                    source_key,
                    0,
                )
            )

            if (
                current_source_count
                >=
                self.max_per_source
            ):
                continue

            selected.append(
                item
            )

            seen_documents.add(
                document_key
            )

            source_counts[
                source_key
            ] = (
                current_source_count
                + 1
            )

            if (
                len(selected)
                >=
                self.max_items
            ):
                break

        return selected
