from core.models import (
    CitationRef,
    SourceRef,
)


class KnowledgeContextBuilder:

    def _real_source_name(
        self,
        metadata: dict,
    ) -> str | None:

        value = (
            metadata.get("source")
            or metadata.get("file_name")
            or metadata.get("title")
        )

        if value is None:
            return None

        value = str(value).strip()

        return value or None

    def build(
        self,
        candidates: list[dict],
    ) -> tuple[
        str,
        list[SourceRef],
        list[CitationRef],
    ]:

        context_parts = []

        sources = []
        citations = []

        for index, item in enumerate(
            candidates,
            start=1,
        ):

            document = (
                item.get("document")
                or ""
            )

            metadata = (
                item.get("metadata")
                or {}
            )

            context_parts.append(
                f"[Nguồn {index}]\n"
                f"{document}"
            )

            source_name = (
                self._real_source_name(
                    metadata
                )
            )

            # SourceRef có thể tồn tại mà source_name=None.
            # Không tự bịa tên tài liệu/source.
            sources.append(
                SourceRef(
                    source_type="RAG",
                    source_name=(
                        source_name
                    ),
                    document_id=(
                        metadata.get(
                            "document_id"
                        )
                    ),
                    file_name=(
                        metadata.get(
                            "file_name"
                        )
                    ),
                    page=(
                        str(
                            metadata.get(
                                "page"
                            )
                        )
                        if (
                            metadata.get(
                                "page"
                            )
                            is not None
                        )
                        else None
                    ),
                    section=(
                        metadata.get(
                            "section"
                        )
                    ),
                    version=(
                        metadata.get(
                            "version"
                        )
                    ),
                    metadata=metadata,
                )
            )

            # Citation chỉ được tạo khi có tên nguồn thật
            # trong metadata.
            if source_name:

                citations.append(
                    CitationRef(
                        source_name=(
                            source_name
                        ),
                        document_id=(
                            metadata.get(
                                "document_id"
                            )
                        ),
                        file_name=(
                            metadata.get(
                                "file_name"
                            )
                        ),
                        page=(
                            str(
                                metadata.get(
                                    "page"
                                )
                            )
                            if (
                                metadata.get(
                                    "page"
                                )
                                is not None
                            )
                            else None
                        ),
                        section=(
                            metadata.get(
                                "section"
                            )
                        ),
                    )
                )

        context_text = "\n\n".join(
            context_parts
        )

        return (
            context_text,
            sources,
            citations,
        )
