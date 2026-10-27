from __future__ import annotations


import re
from typing import Optional

from core.enums import (
    AiRoute,
    GuardrailDecision,
)

from core.models import (
    NormalizedAiInput,
    SafetyResult,
)


class GuardrailService:

    MAX_INPUT_LENGTH = 8000
    MAX_ATTACHMENT_TEXT_LENGTH = 20000

    # =====================================================
    # INPUT PROMPT-INJECTION PATTERNS
    # =====================================================

    PROMPT_INJECTION_PATTERNS = [
        r"\bignore\s+(?:all\s+)?previous\s+instructions\b",
        r"\bignore\s+(?:the\s+)?system\s+instructions\b",
        r"\bdisregard\s+(?:all\s+)?previous\s+instructions\b",
        r"\bforget\s+(?:all\s+)?previous\s+instructions\b",
        r"\boverride\s+(?:the\s+)?system\s+prompt\b",
        r"\breveal\s+(?:the\s+)?system\s+prompt\b",
        r"\bshow\s+(?:the\s+)?system\s+prompt\b",
        r"\bprint\s+(?:the\s+)?system\s+prompt\b",
        r"\bdeveloper\s+message\b",
        r"\bsystem\s+message\b",
        r"\bhidden\s+instructions\b",
        r"\binternal\s+instructions\b",

        r"bỏ qua\s+(?:mọi\s+|tất cả\s+)?chỉ dẫn trước",
        r"bỏ qua\s+(?:mọi\s+|tất cả\s+)?hướng dẫn trước",
        r"bỏ qua\s+(?:mọi\s+|tất cả\s+)?quy tắc trước",
        r"quên\s+(?:mọi\s+|tất cả\s+)?hướng dẫn trước",
        r"ghi đè\s+(?:system\s+prompt|prompt hệ thống)",
        r"tiết lộ\s+(?:system\s+prompt|prompt hệ thống)",
        r"hiển thị\s+(?:system\s+prompt|prompt hệ thống)",
        r"cho tôi xem\s+(?:system\s+prompt|prompt hệ thống)",
        r"in ra\s+(?:system\s+prompt|prompt hệ thống)",
        r"chỉ dẫn ẩn",
        r"hướng dẫn ẩn",
        r"thông điệp developer",
        r"developer message",
    ]

    # OCR/parser output is untrusted DATA. These rules intentionally require
    # an instruction-changing verb plus a protected target. Ordinary UI text
    # such as "System Message" or "Exception" is not malicious by itself.
    ATTACHMENT_INJECTION_PATTERNS = [
        r"\b(?:ignore|disregard|forget)\s+(?:all\s+)?(?:previous|prior|system|developer)\s+(?:instructions?|messages?|prompts?)\b",
        r"\b(?:reveal|show|print|expose)\s+(?:the\s+)?(?:hidden\s+)?(?:system|developer)\s+(?:prompt|message|instructions?)\b",
        r"\b(?:override|replace)\s+(?:the\s+)?(?:system|developer)\s+(?:prompt|instructions?)\b",
        r"(?:bỏ qua|quên)\s+(?:mọi\s+|tất cả\s+)?(?:chỉ dẫn|hướng dẫn|quy tắc)\s+(?:trước|hệ thống)",
        r"(?:tiết lộ|hiển thị|cho tôi xem|in ra)\s+(?:system prompt|prompt hệ thống|chỉ dẫn ẩn|hướng dẫn ẩn)",
    ]

    # =====================================================
    # HIGH-RISK TOOL / DATA MODIFICATION INSTRUCTIONS
    #
    # Chỉ chặn khi câu có động từ thực thi + thao tác phá huỷ.
    # Không chặn câu hỏi học thuật kiểu:
    # "DROP TABLE là gì?"
    # =====================================================

    DANGEROUS_EXECUTION_PATTERNS = [
        (
            r"\b(?:execute|run|thực thi|chạy)\b"
            r".{0,80}\b(?:drop\s+table|truncate\s+table|"
            r"delete\s+from|xp_cmdshell)\b"
        ),
        (
            r"\b(?:execute|run|thực thi|chạy)\b"
            r".{0,80}\b(?:rm\s+-rf|del\s+/f|format\s+[a-z]:)\b"
        ),
        (
            r"\b(?:xóa|xoá)\s+(?:toàn bộ|tất cả)\b"
            r".{0,80}\b(?:database|cơ sở dữ liệu|bảng|table)\b"
        ),
    ]

    # =====================================================
    # OUTPUT SECRET / INTERNAL INFO PATTERNS
    # =====================================================

    OUTPUT_SECRET_PATTERNS = [
        # Common API key/token assignments.
        (
            re.compile(
                r"(?i)\b(api[_ -]?key|secret|token|"
                r"access[_ -]?token|refresh[_ -]?token)\b"
                r"\s*[:=]\s*([^\s,;]+)"
            ),
            r"\1=[REDACTED]",
        ),

        # Password assignments.
        (
            re.compile(
                r"(?i)\b(password|pwd|pass)\b"
                r"\s*[:=]\s*([^\s,;]+)"
            ),
            r"\1=[REDACTED]",
        ),

        # OpenAI/Groq-style long secret-looking keys.
        (
            re.compile(
                r"\b(?:sk|gsk)_[A-Za-z0-9_-]{16,}\b"
            ),
            "[REDACTED_API_KEY]",
        ),

        # JWT-looking tokens.
        (
            re.compile(
                r"\beyJ[A-Za-z0-9_-]{10,}\."
                r"[A-Za-z0-9_-]{10,}\."
                r"[A-Za-z0-9_-]{10,}\b"
            ),
            "[REDACTED_TOKEN]",
        ),

        # SQL Server connection string password/uid values.
        (
            re.compile(
                r"(?i)\b("
                r"Password|Pwd|User\s*ID|UID"
                r")\s*=\s*([^;]+)"
            ),
            r"\1=[REDACTED]",
        ),

        # Windows internal absolute paths.
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

    # =====================================================
    # HELPERS
    # =====================================================

    def _matches_any(
        self,
        text: str,
        patterns: list[str],
    ) -> bool:

        lowered = (
            text
            or ""
        ).lower()

        return any(
            re.search(
                pattern,
                lowered,
                re.IGNORECASE
                | re.DOTALL,
            )
            for pattern
            in patterns
        )

    def _sanitize_attachment_instructions(
        self,
        ai_input: NormalizedAiInput,
    ) -> list[str]:

        matched_rule_ids: list[str] = []

        def sanitize(value: str, attachment_index: int, field: str) -> str:
            safe_lines: list[str] = []
            for line in (value or "").splitlines():
                matched = next(
                    (
                        index
                        for index, pattern in enumerate(self.ATTACHMENT_INJECTION_PATTERNS)
                        if re.search(pattern, line, re.IGNORECASE)
                    ),
                    None,
                )
                if matched is None:
                    safe_lines.append(line)
                else:
                    matched_rule_ids.append(
                        f"ATTACHMENT_INJECTION_{matched + 1}:{attachment_index}:{field}"
                    )
            return "\n".join(safe_lines).strip()

        for index, attachment in enumerate(ai_input.attachments):

            extracted_text = (
                attachment.extracted_text
                or ""
            ).strip()

            if extracted_text:
                bounded = extracted_text[:self.MAX_ATTACHMENT_TEXT_LENGTH]
                if len(extracted_text) > self.MAX_ATTACHMENT_TEXT_LENGTH:
                    matched_rule_ids.append(
                        f"ATTACHMENT_TEXT_TRUNCATED:{index}:extracted_text"
                    )
                attachment.extracted_text = sanitize(
                    bounded, index, "extracted_text"
                ) or None

            visual = attachment.metadata.get("visual_observation")
            if isinstance(visual, str) and visual.strip():
                attachment.metadata["visual_observation"] = sanitize(
                    visual[:self.MAX_ATTACHMENT_TEXT_LENGTH], index, "visual_observation"
                )

        if matched_rule_ids:
            user_text = str(
                ai_input.metadata.get("user_text")
                or ai_input.metadata.get("effective_prompt")
                or ""
            ).strip()
            safe_parts: list[str] = []
            for attachment in ai_input.attachments:
                label = (attachment.file_name or attachment.file_id or "attachment")[:180]
                if attachment.extracted_text:
                    safe_parts.append(
                        f"[SOURCE {label} | {attachment.mime_type or 'unknown'}]\n"
                        f"{attachment.extracted_text}"
                    )
                visual = attachment.metadata.get("visual_observation")
                if isinstance(visual, str) and visual.strip():
                    safe_parts.append(f"[VISUAL_OBSERVATION {label}]\n{visual.strip()}")
            ai_input.text = user_text
            if safe_parts:
                ai_input.text = (
                    f"{user_text}\n\n[ATTACHMENT_DATA]\n"
                    + "\n\n".join(safe_parts)
                ).strip()
            ai_input.metadata["attachment_guardrail"] = {
                "decision": "SANITIZED",
                "rule_ids": matched_rule_ids,
            }

        return matched_rule_ids

    # =====================================================
    # INPUT INSPECTION
    # =====================================================

    def inspect_input(
        self,
        ai_input: NormalizedAiInput,
    ) -> SafetyResult:

        text = str(ai_input.metadata.get("user_text") or "").strip()
        if "user_text" not in ai_input.metadata:
            text = (ai_input.text or "").split("[ATTACHMENT_DATA]", 1)[0].strip()

        # ==============================================
        # EMPTY
        # ==============================================

        if not text and not ai_input.attachments:

            return SafetyResult(
                decision=(
                    GuardrailDecision.BLOCK
                ),
                reason_code=(
                    "EMPTY_INPUT"
                ),
                public_message=(
                    "Bạn chưa nhập câu hỏi."
                ),
            )

        # ==============================================
        # TOO LONG
        # ==============================================

        if (
            len(text)
            > self.MAX_INPUT_LENGTH
        ):

            return SafetyResult(
                decision=(
                    GuardrailDecision.BLOCK
                ),
                reason_code=(
                    "INPUT_TOO_LONG"
                ),
                public_message=(
                    "Nội dung đầu vào quá dài."
                ),
            )

        # ==============================================
        # PROMPT INJECTION
        # ==============================================

        if self._matches_any(
            text,
            self.PROMPT_INJECTION_PATTERNS,
        ):

            return SafetyResult(
                decision=(
                    GuardrailDecision.BLOCK
                ),
                reason_code=(
                    "PROMPT_INJECTION_DETECTED"
                ),
                public_message=(
                    "Yêu cầu này không thể "
                    "được thực hiện."
                ),
            )

        # ==============================================
        # DANGEROUS EXECUTION REQUEST
        # ==============================================

        if self._matches_any(
            text,
            self.DANGEROUS_EXECUTION_PATTERNS,
        ):

            return SafetyResult(
                decision=(
                    GuardrailDecision.BLOCK
                ),
                reason_code=(
                    "DANGEROUS_TOOL_REQUEST"
                ),
                public_message=(
                    "Yêu cầu thực thi thao tác "
                    "nguy hiểm không được phép."
                ),
            )

        # ==============================================
        # ATTACHMENT INJECTION
        #
        # Attachment/OCR là DATA, không phải instruction.
        #
        # Với pipeline hiện tại, Orchestrator coi mọi
        # decision khác ALLOW là terminal. Vì vậy ta BLOCK
        # attachment chứa instruction injection thay vì trả
        # SANITIZE rồi vô tình dừng sai semantics.
        #
        # Khi Document Adapter hoàn thiện, có thể chuyển
        # thành SANITIZE + giữ phần dữ liệu sạch.
        # ==============================================

        self._sanitize_attachment_instructions(ai_input)

        # ==============================================
        # ALLOW
        # ==============================================

        return SafetyResult(
            decision=(
                GuardrailDecision.ALLOW
            )
        )

    # =====================================================
    # OUTPUT MASKING
    #
    # Dùng sau khi Composer tạo answer.
    # Không làm thay đổi business data hợp lệ như salary.
    # Chỉ mask secret/token/password/internal path.
    # =====================================================

    def mask_output(
        self,
        text: str,
    ) -> tuple[
        str,
        SafetyResult,
    ]:

        original = (
            text
            or ""
        )

        masked = original

        for (
            pattern,
            replacement,
        ) in self.OUTPUT_SECRET_PATTERNS:

            masked = pattern.sub(
                replacement,
                masked,
            )

        if masked != original:

            return (
                masked,
                SafetyResult(
                    decision=(
                        GuardrailDecision.MASK
                    ),
                    reason_code=(
                        "SENSITIVE_OUTPUT_MASKED"
                    ),
                    public_message=None,
                ),
            )

        return (
            original,
            SafetyResult(
                decision=(
                    GuardrailDecision.ALLOW
                )
            ),
        )

    # =====================================================
    # ROUTE OUTPUT CHECK
    #
    # Đây là lớp phòng thủ bổ sung.
    # Authorization / service scope vẫn là nguồn enforcement
    # chính cho Personal Data.
    # =====================================================

    def inspect_output(
        self,
        text: str,
        route: Optional[AiRoute],
    ) -> tuple[
        str,
        SafetyResult,
    ]:

        masked_text, result = (
            self.mask_output(
                text
            )
        )

        if (
            result.decision
            == GuardrailDecision.MASK
        ):
            return (
                masked_text,
                result,
            )

        return (
            masked_text,
            SafetyResult(
                decision=(
                    GuardrailDecision.ALLOW
                )
            ),
        )
