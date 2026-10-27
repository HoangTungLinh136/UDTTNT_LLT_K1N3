from __future__ import annotations

import re

from dataclasses import dataclass


@dataclass(frozen=True)
class OutputQualityResult:
    passed: bool
    reason_code: str | None = None
    public_message: str | None = None


class OutputQualityGate:
    """
    Phase 11.

    Deterministic output checks.

    Không dùng LLM để tự đánh giá chính nó.
    Không thay authorization/business logic.
    """

    REASONING_PATTERNS = (
        r"(?im)^\s*analysis\s*:",
        r"(?im)^\s*reasoning\s*:",
        r"(?i)\blet me think\b",
        r"(?i)\blet me reason\b",
        r"(?i)\bchain[- ]of[- ]thought\b",
        r"(?i)\bthe system prompt\b",
        r"(?i)\bthe system says\b",
        r"(?i)\bwe need to\b",
        r"(?i)\bi need to\b",
        r"(?i)\bi should\b",
        r"(?i)\bi must\b",
        r"(?i)^\s*wait,\s*the user",
        r"(?i)^\s*wait,\s*the user's question",
    )

    BROKEN_PREFIX_PATTERNS = (
        r"(?i)^\s*but the code\b",
        r"(?i)^\s*but the user\b",
        r"(?i)^\s*the user asked\b",
        r"(?i)^\s*the user wants\b",

        # Browser-observed meta/model prefixes.
        r"(?i)^\s*maybe\s*:",
        r"(?i)^\s*alternatively\b",
        r"(?i)^\s*the source says\s*:",
        r"(?i)^\s*in vietnamese\s*,?",

        # RAG must never render a source label as the answer.
        r"(?i)^\s*(?:source|nguồn)\s*#?\s*\d+\s*[\.:]?\s*$",
        # P2-B V2 global meta-output guard
        r"(?i)^\s*but the source(?:\s+also)?\s+says\b",
        r"(?i)^\s*the source(?:\s+also)?\s+says\b",
        r"(?i)^\s*let me rephrase\b",
        r"(?i)^\s*but the question\b",
        r"(?i)^\s*but the answer\b",
        r"(?i)^\s*but the context\b",
        # P2-B V3 META ANSWER PREFIX GUARD
        r"(?i)^\s*so[,]?\s+the\s+answer(?:\s+in\s+[a-z-]+)?\s+(?:is|would\s+be)\s*:",
        r"(?i)^\s*the\s+answer(?:\s+in\s+[a-z-]+)?\s+(?:is|would\s+be)\s*:",
        r"(?i)^\s*so[,]?\s+in\s+vietnamese\b.*?:",
        r"(?i)^\s*in\s+vietnamese[,]?\s+(?:the\s+answer\s+)?(?:is|would\s+be)\s*:",
        r"(?i)^\s*here(?:'s|\s+is)\s+(?:the\s+)?answer\b",
        # P2-B V4 GENERAL META OUTPUT GUARD
        r"(?i)^\s*(?:but|however)\s+(?:the\s+)?(?:source|context|question|answer|rule|rules|policy|prompt|instruction|instructions|evidence|document|text|data)\b",
        r"(?i)^\s*(?:the\s+)?(?:source|context|question|rule|rules|prompt|instruction|instructions|evidence)\s+(?:also\s+)?(?:says?|states?|indicates?|means?)\b",
        r"(?i)^\s*(?:based\s+on|according\s+to)\s+(?:the\s+)?(?:source|context|evidence|document)\b",
        # P2-B V5 FINAL META PREFIX GUARD
        r"(?i)^\s*(?:so\s*,?\s+)?the\s+answer\b",
        r"(?i)^\s*(?:rule|instruction)\s*#?\s*\d+\s*[.:]?\s*$",
    )

    @staticmethod
    def _clean(
        value: str | None,
    ) -> str:

        return str(
            value
            or ""
        ).strip()

    @classmethod
    def _has_reasoning_leak(
        cls,
        text: str,
    ) -> bool:

        return any(
            re.search(
                pattern,
                text,
            )
            is not None
            for pattern
            in cls.REASONING_PATTERNS
        )

    @classmethod
    def _has_broken_prefix(
        cls,
        text: str,
    ) -> bool:

        return any(
            re.search(
                pattern,
                text,
            )
            is not None
            for pattern
            in cls.BROKEN_PREFIX_PATTERNS
        )

    @staticmethod
    def _has_unclosed_code_fence(
        text: str,
    ) -> bool:

        count = text.count(
            "```"
        )

        return (
            count % 2
            != 0
        )

    @staticmethod
    def _looks_truncated(
        text: str,
    ) -> bool:

        if not text:
            return True

        tail = text[-80:].strip()

        # Không coi câu kết bằng punctuation bình thường là truncated.
        if tail.endswith(
            (
                ".",
                "!",
                "?",
                ":",
                ";",
                ")",
                "]",
                "}",
                "`",
                "…",
            )
        ):
            return False

        suspicious_endings = (
            " và",
            " hoặc",
            " nhưng",
            " vì",
            " nên",
            " là",
            " gồm",
            " như",
            " bằng",
            " với",
            " để",
            " nếu",
            " khi",
            " then",
            " and",
            " or",
            " because",
            " with",
            " to",
            " the",
        )

        lowered = tail.lower()

        return any(
            lowered.endswith(
                ending
            )
            for ending
            in suspicious_endings
        )

    def evaluate(
        self,
        answer: str | None,
    ) -> OutputQualityResult:

        text = self._clean(
            answer
        )

        if not text:

            return OutputQualityResult(
                passed=False,
                reason_code="QUALITY_EMPTY_OUTPUT",
                public_message=(
                    "Không có nội dung trả lời hợp lệ."
                ),
            )

        if self._has_reasoning_leak(
            text
        ):

            return OutputQualityResult(
                passed=False,
                reason_code="QUALITY_REASONING_LEAK",
                public_message=(
                    "Câu trả lời chứa nội dung nội bộ "
                    "không được phép hiển thị."
                ),
            )

        if self._has_broken_prefix(
            text
        ):

            return OutputQualityResult(
                passed=False,
                reason_code="QUALITY_BROKEN_MODEL_OUTPUT",
                public_message=(
                    "Câu trả lời của mô hình không hoàn chỉnh."
                ),
            )

        if self._has_unclosed_code_fence(
            text
        ):

            return OutputQualityResult(
                passed=False,
                reason_code="QUALITY_UNCLOSED_CODE_BLOCK",
                public_message=(
                    "Khối mã trong câu trả lời chưa hoàn chỉnh."
                ),
            )

        if self._looks_truncated(
            text
        ):

            return OutputQualityResult(
                passed=False,
                reason_code="QUALITY_TRUNCATED_OUTPUT",
                public_message=(
                    "Câu trả lời có dấu hiệu bị cắt giữa chừng."
                ),
            )

        return OutputQualityResult(
            passed=True,
        )
