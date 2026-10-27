from __future__ import annotations

import re

from core.enums import (
    AiRoute,
    VerificationStatus,
)

from core.models import (
    RouteResult,
    VerificationResult,
)

from response.output_quality_gate import (
    OutputQualityGate,
)


class AnswerVerifier:

    def __init__(
        self,
    ):
        self.quality_gate = (
            OutputQualityGate()
        )


    REASONING_MARKERS = (
        "<think>",
        "</think>",
        "okay, the user",
        "the user is asking",
        "let me think",
        "i should",
        "let me check",
        "wait,",
        "system says",
        "chain-of-thought",
        "but the rules say",
        "output only the final answer",
        "without any reasoning",
        "so the answer should",
        "the answer should be",
        "i need to answer",
        "we need answer",
        "but the user wrote",
        "the user wrote",
        "the user said",
        "the user asks",
        "the user is asking",
        "the rules say",
        "the system says",
        "i need to",
        "i should",
        "i must",
        "so the answer should",
        "the answer should",
        "response should",
        "reasoning:",
        "analysis:",
    )

    @classmethod
    def _sanitize_answer(
        cls,
        text: str,
    ) -> tuple[str, bool]:
        raw = str(
            text
            or ""
        ).strip()

        if not raw:
            return "", False

        original = raw
        lower = raw.lower()

        # Qwen/Ollama case:
        # reasoning... </think> FINAL
        if "</think>" in lower:
            idx = lower.rfind(
                "</think>"
            )

            raw = raw[
                idx
                + len("</think>"):
            ].strip()

            lower = raw.lower()

        # Balanced hidden blocks.
        raw = re.sub(
            r"(?is)<think>.*?</think>",
            "",
            raw,
        ).strip()

        # Dangling <think> with no final section.
        lower = raw.lower()

        if "<think>" in lower:
            idx = lower.find(
                "<think>"
            )

            prefix = raw[
                :idx
            ].strip()

            raw = prefix

        # Explicit Analysis ... Final pattern.
        match = re.search(
            (
                r"(?is)"
                r"(?:^|\n)"
                r"(?:final|answer)"
                r"\s*:\s*(.+)$"
            ),
            raw,
        )

        if match:
            raw = (
                match.group(1)
                .strip()
            )

        lower = raw.lower()

        # -------------------------------------------------
        # Browser-observed model/meta prefixes.
        #
        # Những prefix này không phải nội dung trả lời cho
        # người dùng. Có thể bỏ prefix nếu phía sau vẫn còn
        # một câu trả lời hữu ích.
        # -------------------------------------------------

        meta_prefix_patterns = (
            r"(?is)^\s*maybe\s*:\s*",
            (
                r"(?is)^\s*alternatively\s*,?\s*"
                r"(?:the answer could be\s*:\s*)?"
            ),
            r"(?is)^\s*the source says\s*:\s*",
            r"(?is)^\s*in vietnamese\s*,?\s*",
        )

        meta_prefix_removed = False

        for pattern in meta_prefix_patterns:
            cleaned = re.sub(
                pattern,
                "",
                raw,
                count=1,
            ).strip()

            if cleaned != raw:
                raw = cleaned
                meta_prefix_removed = True

        # "In Vietnamese, \"...\"" -> ...
        if (
            meta_prefix_removed
            and len(raw) >= 2
            and raw[0] == raw[-1]
            and raw[0] in {'"', "'"}
        ):
            raw = raw[1:-1].strip()

        lower = raw.lower()

        # Nếu vẫn chỉ còn reasoning, không render nó.
        remaining_markers = [
            marker
            for marker in cls.REASONING_MARKERS
            if marker in lower
        ]

        if remaining_markers:
            # Fail closed:
            # nếu vẫn còn meta-reasoning sau bước extract final,
            # không render bất kỳ fragment nào của reasoning.
            raw = ""

        changed = (
            raw.strip()
            != original.strip()
        )

        return raw.strip(), changed


    # =====================================================
    # ROUTE WARNINGS THAT ARE VALID SAFE FALLBACKS
    # =====================================================

    WARNING_CODES = {
        "NO_INTERNAL_EVIDENCE",
        "LIVE_QUERY_NOT_WHITELISTED",
        "LIVE_SQL_SCHEMA_NOT_SUPPORTED",
        "LIVE_SQL_FAILED",
        "PERSONAL_SALARY_NOT_FOUND",
        "PERSONAL_SALARY_PERIOD_NOT_FOUND",
        "PERSONAL_CONTRACT_NOT_FOUND",
        "PERSONAL_QUERY_NOT_WHITELISTED",
        "TARGET_EMPLOYEE_NOT_FOUND",
        "PERSONAL_QUERY_INVALID",
        "PERSONAL_DATA_FAILED",
        "GENERAL_LLM_FAILED",
        "LLM_GENERATION_FAILED",
        "EMPTY_GENERATED_ANSWER",
    }

    # =====================================================
    # VERIFY
    # =====================================================

    # P2-B RAG FINAL VIETNAMESE HELPER
    @staticmethod
    def _is_clean_rag_final_answer(
        text: str,
    ) -> bool:

        value = str(
            text
            or ""
        ).strip()

        if not value:
            return False

        lower = value.lower()

        bad_prefixes = (
            "so the ",
            "so, the ",
            "so in ",
            "so, in ",
            "the answer",
            "the key point",
            "the source",
            "the rule",
            "the rules",
            "the policy",
            "the context",
            "the question",
            "but ",
            "however ",
            "rule ",
            "instruction ",
            "according to ",
            "based on ",
            "let me ",
            "maybe:",
            "alternatively",
            "here is ",
            "here's ",
            "in vietnamese",
        )

        if lower.startswith(
            bad_prefixes
        ):
            return False

        # RAG HRMS contract:
        # final user-facing answer phải là tiếng Việt.
        vietnamese_chars = (
            "ăâđêôơư"
            "áàảãạ"
            "ắằẳẵặ"
            "ấầẩẫậ"
            "éèẻẽẹ"
            "ếềểễệ"
            "íìỉĩị"
            "óòỏõọ"
            "ốồổỗộ"
            "ớờởỡợ"
            "úùủũụ"
            "ứừửữự"
            "ýỳỷỹỵ"
        )

        if any(
            ch in lower
            for ch in vietnamese_chars
        ):
            return True

        # Fallback cho câu Việt ít dấu.
        vietnamese_markers = (
            "quy định",
            "hệ thống",
            "nhân viên",
            "chấm công",
            "đi muộn",
            "nghỉ phép",
            "hợp đồng",
            "phút",
            "không ",
            "chưa ",
            "được ",
            "theo ",
        )

        marker_count = sum(
            1
            for marker
            in vietnamese_markers
            if marker in lower
        )

        return marker_count >= 2

    def verify(
        self,
        result: RouteResult,
    ) -> VerificationResult:

        # P2-B RAG FINAL VIETNAMESE GUARD CALL
        if (
            result.route
            == AiRoute.RAG
            and not self._is_clean_rag_final_answer(
                result.answer_draft
            )
        ):
            return VerificationResult(
                status=VerificationStatus.FAIL,
                checks=[
                    "RAG_OUTPUT_NOT_FINAL_VI",
                ],
                warning=(
                    "Câu trả lời RAG không ở dạng "
                    "câu trả lời cuối cùng tiếng Việt."
                ),
            )

        checks: list[str] = []

        # Keep the provider output only for quality classification.
        # Never render this raw value to the user.
        original_answer_draft = (
            result.answer_draft
        )

        # =================================================
        # 0. OUTPUT SANITIZATION
        #
        # Luôn sanitize trước Quality Gate.
        # Quality Gate chỉ đánh giá nội dung thực sự chuẩn bị
        # render lên UI.
        # =================================================

        sanitized, was_sanitized = (
            self._sanitize_answer(
                result.answer_draft
            )
        )

        result.answer_draft = sanitized

        if was_sanitized:
            checks.append(
                "OUTPUT_REASONING_SANITIZED"
            )

        # =================================================
        # PHASE 11 OUTPUT QUALITY GATE
        # =================================================

        quality = (
            self.quality_gate
            .evaluate(
                result.answer_draft
            )
        )

        # If sanitization removed the entire answer, preserve a
        # stronger quality diagnosis from the original provider
        # output instead of degrading it to QUALITY_EMPTY_OUTPUT.
        if (
            not result.answer_draft
            and was_sanitized
        ):
            original_quality = (
                self.quality_gate
                .evaluate(
                    original_answer_draft
                )
            )

            if (
                not original_quality.passed
                and original_quality.reason_code
                == "QUALITY_REASONING_LEAK"
            ):
                quality = original_quality

        if not quality.passed:

            return VerificationResult(
                status=(
                    VerificationStatus.FAIL
                ),
                checks=[
                    quality.reason_code
                    or
                    "OUTPUT_QUALITY_FAILED"
                ],
                warning=(
                    quality.public_message
                    or
                    "Câu trả lời không đạt kiểm tra chất lượng."
                ),
            )

        checks.append(
            "OUTPUT_QUALITY_PASS"
        )


        # Output đã được sanitize trước Quality Gate.

        # =================================================
        # 1. ANSWER EXISTS
        # =================================================

        if not result.answer_draft:

            return VerificationResult(
                status=(
                    VerificationStatus.FAIL
                ),
                checks=[
                    "EMPTY_ANSWER"
                ],
                warning=(
                    "Không có nội dung trả lời."
                ),
            )

        checks.append(
            "ANSWER_NOT_EMPTY"
        )

        # =================================================
        # 2. KNOWN ROUTE WARNINGS
        # =================================================

        if (
            result.warning
            in self.WARNING_CODES
        ):

            # ---------------------------------------------
            # RAG SAFE FALLBACK
            # ---------------------------------------------

            if (
                result.warning
                == "NO_INTERNAL_EVIDENCE"
            ):

                return VerificationResult(
                    status=(
                        VerificationStatus.WARNING
                    ),
                    checks=[
                        "NO_INTERNAL_EVIDENCE"
                    ],
                    warning=(
                        "Không có bằng chứng nội bộ "
                        "đủ tin cậy."
                    ),
                )

            checks.append(
                (
                    "ROUTE_WARNING:"
                    f"{result.warning}"
                )
            )

        # =================================================
        # 3. RAG
        # =================================================

        if (
            result.route
            == AiRoute.RAG
        ):

            # ---------------------------------------------
            # SOURCE
            # ---------------------------------------------

            if not result.sources:

                return VerificationResult(
                    status=(
                        VerificationStatus.FAIL
                    ),
                    checks=[
                        "RAG_SOURCE_MISSING"
                    ],
                    warning=(
                        "Câu trả lời RAG không có "
                        "nguồn kiểm chứng."
                    ),
                )

            checks.append(
                "RAG_SOURCE_EXISTS"
            )

            # ---------------------------------------------
            # CITATION
            # ---------------------------------------------

            if not result.citations:

                return VerificationResult(
                    status=(
                        VerificationStatus.WARNING
                    ),
                    checks=(
                        checks
                        + [
                            "RAG_CITATION_MISSING"
                        ]
                    ),
                    warning=(
                        "Có nguồn RAG nhưng thiếu "
                        "citation chi tiết."
                    ),
                )

            checks.append(
                "RAG_CITATION_EXISTS"
            )

            # =============================================
            # BASIC GROUNDING CHECK FOR VALUE 0
            # =============================================

            answer_lower = (
                result.answer_draft
                .lower()
            )

            safe_zero_phrases = [
                "không được tự suy luận",
                "không thể kết luận",
                "chưa đủ thông tin để kết luận",
                "chưa cấu hình giờ ca",
                "chưa cấu hình ngưỡng",
                "hệ thống chưa cấu hình",
                "hiện chưa cấu hình",
            ]

            contains_safe_explanation = any(
                phrase
                in answer_lower
                for phrase
                in safe_zero_phrases
            )

            if not contains_safe_explanation:

                unsafe_zero_patterns = [
                    (
                        r"0 phút[^.\n]{0,80}"
                        r"(?:có nghĩa là|nghĩa là|tức là)"
                        r"\s*không bị trừ"
                    ),
                    (
                        r"0 phút[^.\n]{0,80}"
                        r"(?:có nghĩa là|nghĩa là|tức là)"
                        r"\s*bị trừ toàn bộ"
                    ),
                    (
                        r"0 phút[^.\n]{0,80}"
                        r"(?:có nghĩa là|nghĩa là|tức là)"
                        r"\s*không giới hạn"
                    ),
                    (
                        r"0 phút[^.\n]{0,80}"
                        r"(?:có nghĩa là|nghĩa là|tức là)"
                        r"\s*không có thời gian công"
                    ),
                    (
                        r"0 phút[^.\n]{0,80}"
                        r"(?:có nghĩa là|nghĩa là|tức là)"
                        r"\s*không có khoản trừ"
                    ),
                ]

                for pattern in (
                    unsafe_zero_patterns
                ):

                    if re.search(
                        pattern,
                        answer_lower,
                        re.DOTALL,
                    ):

                        return VerificationResult(
                            status=(
                                VerificationStatus.FAIL
                            ),
                            checks=(
                                checks
                                + [
                                    "UNSUPPORTED_ZERO_INTERPRETATION"
                                ]
                            ),
                            warning=(
                                "Câu trả lời đã suy diễn "
                                "ý nghĩa của cấu hình 0."
                            ),
                        )

            checks.append(
                "RAG_BASIC_GROUNDING_CHECK"
            )

        # =================================================
        # 4. LIVE SQL
        # =================================================

        elif (
            result.route
            == AiRoute.LIVE_SQL
        ):

            if result.data is None:

                return VerificationResult(
                    status=(
                        VerificationStatus.FAIL
                    ),
                    checks=[
                        "SQL_STRUCTURED_DATA_MISSING"
                    ],
                    warning=(
                        "Live SQL không có "
                        "structured data."
                    ),
                )

            checks.append(
                "SQL_STRUCTURED_DATA_EXISTS"
            )

            if not result.sources:

                return VerificationResult(
                    status=(
                        VerificationStatus.WARNING
                    ),
                    checks=(
                        checks
                        + [
                            "SQL_SOURCE_MISSING"
                        ]
                    ),
                    warning=(
                        "Live SQL thiếu thông tin nguồn."
                    ),
                )

            checks.append(
                "SQL_SOURCE_EXISTS"
            )

        # =================================================
        # 5. PERSONAL DATA
        # =================================================

        elif (
            result.route
            == AiRoute.PERSONAL_DATA
        ):

            # ---------------------------------------------
            # Một số safe fallback Personal Data vẫn có
            # structured metadata và warning.
            # ---------------------------------------------

            if result.data is None:

                return VerificationResult(
                    status=(
                        VerificationStatus.FAIL
                    ),
                    checks=[
                        "PERSONAL_DATA_MISSING"
                    ],
                    warning=(
                        "Personal Data không có "
                        "structured result."
                    ),
                )

            checks.append(
                "PERSONAL_RESULT_EXISTS"
            )

            if result.sources:

                checks.append(
                    "PERSONAL_SOURCE_EXISTS"
                )

        # =================================================
        # 6. GENERAL LLM
        # =================================================

        elif (
            result.route
            == AiRoute.GENERAL_LLM
        ):

            checks.append(
                "GENERAL_LLM_RESPONSE"
            )

        # =================================================
        # 7. UNKNOWN ROUTE
        # =================================================

        else:

            return VerificationResult(
                status=(
                    VerificationStatus.FAIL
                ),
                checks=(
                    checks
                    + [
                        "UNKNOWN_RESPONSE_ROUTE"
                    ]
                ),
                warning=(
                    "Không xác định được route "
                    "để kiểm chứng câu trả lời."
                ),
            )

        # =================================================
        # 8. FINAL RESULT
        # =================================================

        if result.warning:

            return VerificationResult(
                status=(
                    VerificationStatus.WARNING
                ),
                checks=checks,
                warning=(
                    result.warning
                ),
            )

        return VerificationResult(
            status=(
                VerificationStatus.PASS
            ),
            checks=checks,
        )

