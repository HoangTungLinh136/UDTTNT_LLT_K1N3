from __future__ import annotations

import re
import time
import unicodedata
from typing import Any

import pyodbc

from config.settings import settings
from database.connection import build_sql_connection_string


class EntityResolver:
    """
    V17 Entity Resolver

    - EmployeeCode: NV001, NV002...
    - Employee full name: "Hoàng Tùng Linh" -> NV001
    - Department phrase: "phòng Kỹ thuật"
    - Coreference / entity follow-up helpers.

    Quan trọng:
    Entity linking tên nhân viên lấy từ SQL HRMS,
    không để GENERAL_LLM tự đoán tên người trong hệ thống.
    """

    EMPLOYEE_CACHE_TTL_SECONDS = 60.0

    def __init__(self):
        self._employee_cache: list[dict[str, str]] = []
        self._employee_cache_at = 0.0

    @staticmethod
    def _normalize(value: str) -> str:
        value = unicodedata.normalize(
            "NFD",
            str(value or ""),
        )

        value = "".join(
            ch
            for ch in value
            if unicodedata.category(ch) != "Mn"
        )

        value = value.replace(
            "đ",
            "d",
        ).replace(
            "Đ",
            "D",
        )

        value = re.sub(
            r"\s+",
            " ",
            value,
        )

        return value.strip().casefold()

    @staticmethod
    def _connection_string() -> str:
        return build_sql_connection_string(timeout=3)

    def _load_employees(
        self,
    ) -> list[dict[str, str]]:
        now = time.monotonic()

        if (
            self._employee_cache
            and (
                now
                - self._employee_cache_at
            )
            < self.EMPLOYEE_CACHE_TTL_SECONDS
        ):
            return self._employee_cache

        conn = None
        cursor = None

        try:
            conn = pyodbc.connect(
                self._connection_string()
            )

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT
                    EmployeeCode,
                    Name
                FROM Employees
                WHERE EmployeeCode IS NOT NULL
                  AND Name IS NOT NULL
                """
            )

            items: list[dict[str, str]] = []

            for row in cursor.fetchall():
                code = str(
                    row[0]
                    or ""
                ).strip()

                name = str(
                    row[1]
                    or ""
                ).strip()

                if not code or not name:
                    continue

                items.append(
                    {
                        "employee_code": code.upper(),
                        "name": name,
                        "normalized_name": (
                            self._normalize(
                                name
                            )
                        ),
                    }
                )

            # Tên dài hơn ưu tiên trước để tránh match
            # một phần tên ngắn khi có nhiều candidate.
            items.sort(
                key=lambda x: len(
                    x[
                        "normalized_name"
                    ]
                ),
                reverse=True,
            )

            self._employee_cache = items
            self._employee_cache_at = now

            return items

        except Exception:
            # Entity resolver không được làm sập toàn bộ chatbot.
            return self._employee_cache

        finally:
            if cursor:
                try:
                    cursor.close()
                except Exception:
                    pass

            if conn:
                try:
                    conn.close()
                except Exception:
                    pass

    @staticmethod
    def _extract_employee_code(
        text: str,
    ) -> str | None:
        match = re.search(
            r"\bNV[\s-]*\d{3,}\b",
            text or "",
            re.IGNORECASE,
        )

        if not match:
            return None

        return re.sub(
            r"[\s-]+",
            "",
            match.group(0),
        ).upper()

    def _detect_employee_name(
        self,
        text: str,
    ) -> dict[str, Any] | None:
        normalized_text = self._normalize(
            text
        )

        if not normalized_text:
            return None

        candidates: list[
            dict[str, str]
        ] = []

        for employee in (
            self._load_employees()
        ):
            normalized_name = employee[
                "normalized_name"
            ]

            if len(
                normalized_name
            ) < 4:
                continue

            if normalized_name in normalized_text:
                candidates.append(
                    employee
                )

        if not candidates:
            return None

        # Nếu cùng một tên trỏ tới nhiều mã khác nhau,
        # không tự đoán.
        best_name = candidates[0][
            "normalized_name"
        ]

        best = [
            item
            for item in candidates
            if item[
                "normalized_name"
            ]
            == best_name
        ]

        codes = {
            item[
                "employee_code"
            ]
            for item in best
        }

        if len(codes) != 1:
            return None

        selected = best[0]

        return {
            "type": "employee_code",
            "value": selected[
                "employee_code"
            ],
            "confidence": 0.99,
            "metadata": {
                "employee_name": selected[
                    "name"
                ],
                "resolved_by": (
                    "employee_name_sql"
                ),
            },
        }

    def _detect_department(
        self,
        text: str,
    ) -> dict[str, Any] | None:
        # Chỉ nhận diện khi câu có từ khóa phòng/ban rõ ràng.
        match = re.search(
            (
                r"\b(?:phòng|phong|ban)\s+"
                r"([A-Za-zÀ-ỹ0-9]["
                r"A-Za-zÀ-ỹ0-9\s\-_]{1,60})"
            ),
            text or "",
            re.IGNORECASE,
        )

        if not match:
            return None

        value = re.sub(
            r"\s+",
            " ",
            match.group(1),
        ).strip(
            " .,!?:;"
        )

        # Loại đuôi nghi vấn phổ biến.
        value = re.sub(
            (
                r"\s+(?:có|co|là|la|"
                r"hiện|hien|bao|bao nhiêu|"
                r"bao nhieu).*$"
            ),
            "",
            value,
            flags=re.IGNORECASE,
        ).strip()

        if not value:
            return None

        return {
            "type": "department",
            "value": value,
            "confidence": 0.80,
        }

    def detect(
        self,
        text: str,
    ) -> dict[str, Any] | None:
        text = str(
            text
            or ""
        ).strip()

        if not text:
            return None

        employee_code = (
            self._extract_employee_code(
                text
            )
        )

        if employee_code:
            return {
                "type": "employee_code",
                "value": employee_code,
                "confidence": 0.99,
                "metadata": {
                    "resolved_by": (
                        "employee_code"
                    ),
                },
            }

        employee_name = (
            self._detect_employee_name(
                text
            )
        )

        if employee_name:
            return employee_name

        return self._detect_department(
            text
        )

    def is_coreference(
        self,
        text: str,
    ) -> bool:
        q = self._normalize(
            text
        )

        patterns = [
            r"\bnhan vien (?:do|ay|nay)\b",
            r"\bnguoi (?:do|ay|nay)\b",
            r"\bphong (?:do|ay|nay)\b",
            r"\bbo phan (?:do|ay|nay)\b",
            r"\bcai (?:do|kia)\b",
            r"\btruong hop do\b",
        ]

        return any(
            re.search(
                pattern,
                q,
                re.IGNORECASE,
            )
            for pattern in patterns
        )

    def is_entity_follow_up(
        self,
        text: str,
    ) -> bool:
        q = self._normalize(
            text
        )

        return bool(
            re.search(
                (
                    r"^(?:the con|con|"
                    r"vay con|the|vay)\b"
                ),
                q,
                re.IGNORECASE,
            )
        )
