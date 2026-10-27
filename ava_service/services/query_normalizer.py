from __future__ import annotations

import re


class QueryNormalizer:
    """
    Chuẩn hóa chat tiếng Việt ở mức an toàn.
    Không fuzzy-match mã nhân viên/tên riêng.
    Không tự đoán acronym.
    """

    _REPLACEMENTS = (
        (r"\bko\b", "không"),
        (r"\bkhum\b", "không"),
        (r"\bhok\b", "không"),
        (r"\bhem\b", "không"),
        (r"\bmk\b", "mình"),
        (r"\bvs\b", "với"),
        (r"\bđc\b", "được"),
        (r"\bdc\b", "được"),
        (r"\bntn\b", "như thế nào"),
    )

    @classmethod
    def normalize(cls, text: str) -> str:
        value = (text or "").strip()
        if not value:
            return ""

        for pattern, replacement in cls._REPLACEMENTS:
            value = re.sub(
                pattern,
                replacement,
                value,
                flags=re.IGNORECASE,
            )

        value = re.sub(r"\s+", " ", value).strip()
        return value
