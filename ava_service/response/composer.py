from core.models import (
    AiResponse,
    CitationRef,
    RouteResult,
    SafetyResult,
    SourceRef,
    VerificationResult,
)


class ResponseComposer:

    def _deduplicate_sources(
        self,
        sources: list[SourceRef],
    ) -> list[SourceRef]:

        seen = set()
        result = []

        for source in sources:

            key = (
                source.source_type,
                source.source_name,
                source.document_id,
                source.file_name,
                source.page,
                source.section,
                source.version,
            )

            if key in seen:
                continue

            seen.add(key)
            result.append(source)

        return result

    def _deduplicate_citations(
        self,
        citations: list[CitationRef],
    ) -> list[CitationRef]:

        seen = set()
        result = []

        for citation in citations:

            key = (
                citation.source_name,
                citation.document_id,
                citation.file_name,
                citation.page,
                citation.section,
            )

            if key in seen:
                continue

            seen.add(key)
            result.append(citation)

        return result

    def _merge_warning(
        self,
        route_warning: str | None,
        verification_warning: str | None,
    ) -> str | None:

        warnings = []

        for warning in [
            route_warning,
            verification_warning,
        ]:

            if not warning:
                continue

            # Tránh warning giống nhau bị nối lại.
            if warning not in warnings:
                warnings.append(warning)

        if not warnings:
            return None

        return " | ".join(warnings)

    def compose(
        self,
        route_result: RouteResult,
        intent: str,
        verification: VerificationResult,
        safety: SafetyResult | None = None,
    ) -> AiResponse:

        # ==========================================
        # 1. MERGE WARNING
        # ==========================================

        # P2-B COMPOSER FAIL-CLOSED
        verification_status = getattr(
            getattr(
                verification,
                "status",
                None,
            ),
            "value",
            getattr(
                verification,
                "status",
                None,
            ),
        )

        if str(
            verification_status
            or ""
        ).upper() == "FAIL":
            return AiResponse(
                answer=(
                    "Tôi chưa thể tạo câu trả lời "
                    "an toàn và đáng tin cậy từ "
                    "kết quả hiện tại. "
                    "Vui lòng thử lại."
                ),
                route=route_result.route,
                intent=intent,
                sources=[],
                citations=[],
                warning=(
                    verification.warning
                    or route_result.warning
                    or "ANSWER_VERIFICATION_FAILED"
                ),
                safety=safety,
                verification=verification,
            )

        warning = self._merge_warning(
            route_warning=route_result.warning,
            verification_warning=verification.warning,
        )

        # ==========================================
        # 2. DEDUPLICATE SOURCES
        # ==========================================

        sources = self._deduplicate_sources(
            route_result.sources
        )

        # ==========================================
        # 3. DEDUPLICATE CITATIONS
        # ==========================================

        citations = self._deduplicate_citations(
            route_result.citations
        )

        # ==========================================
        # 4. BUILD FINAL RESPONSE
        # ==========================================

        return AiResponse(
            answer=route_result.answer_draft,
            route=route_result.route,
            intent=intent,
            sources=sources,
            citations=citations,
            warning=warning,
            can_speak=True,
            safety=(
                safety
                if safety is not None
                else SafetyResult()
            ),
            verification=verification,
        )