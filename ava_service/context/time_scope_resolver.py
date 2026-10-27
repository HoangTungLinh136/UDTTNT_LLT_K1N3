import re
from datetime import date


class TimeScopeResolver:

    def detect(
        self,
        text: str,
    ) -> str | None:

        if not text:
            return None

        q = text.lower().strip()

        # ==========================================
        # RELATIVE MONTH
        # ==========================================

        if any(
            term in q
            for term in [
                "tháng này",
                "tháng hiện tại",
                "trong tháng này",
            ]
        ):
            return "current_month"

        if any(
            term in q
            for term in [
                "tháng trước",
                "tháng vừa rồi",
                "tháng trước đó",
            ]
        ):
            return "previous_month"

        # ==========================================
        # TODAY / YESTERDAY
        # ==========================================

        if any(
            term in q
            for term in [
                "hôm nay",
                "ngày hôm nay",
            ]
        ):
            return "today"

        if any(
            term in q
            for term in [
                "hôm qua",
                "ngày hôm qua",
            ]
        ):
            return "yesterday"

        # ==========================================
        # YEAR
        # ==========================================

        if any(
            term in q
            for term in [
                "năm nay",
                "năm hiện tại",
            ]
        ):
            return "current_year"

        if any(
            term in q
            for term in [
                "năm trước",
                "năm ngoái",
            ]
        ):
            return "previous_year"

        # ==========================================
        # EXPLICIT MONTH
        # Ví dụ: tháng 8, tháng 09
        # ==========================================

        match = re.search(
            r"\btháng\s+(0?[1-9]|1[0-2])\b",
            q,
        )

        if match:

            month = int(
                match.group(1)
            )

            return f"month:{month:02d}"

        return None

    # ==============================================
    # TEMPORAL FOLLOW-UP
    # ==============================================

    def is_time_follow_up(
        self,
        text: str,
    ) -> bool:

        if not text:
            return False

        q = text.lower().strip()

        patterns = [
            r"^còn tháng trước\??$",
            r"^thế tháng trước\??$",
            r"^tháng trước thì sao\??$",
            r"^thế còn tháng trước(?: thì sao)?\??$",
            r"^vậy còn tháng trước(?: thì sao)?\??$",
            r"^còn tháng này\??$",
            r"^thế tháng này\??$",
            r"^thế còn tháng này(?: thì sao)?\??$",
            r"^vậy còn tháng này(?: thì sao)?\??$",
            r"^còn hôm qua\??$",
            r"^thế hôm qua\??$",
            r"^thế còn hôm qua(?: thì sao)?\??$",
            r"^còn hôm nay\??$",
            r"^thế còn hôm nay(?: thì sao)?\??$",
            r"^còn năm ngoái\??$",
            r"^thế năm ngoái\??$",
            r"^thế còn năm ngoái(?: thì sao)?\??$",
            r"^còn tháng\s+(0?[1-9]|1[0-2])\??$",
            r"^thế còn tháng\s+(0?[1-9]|1[0-2])(?: thì sao)?\??$",
        ]

        return any(
            re.search(
                pattern,
                q,
                re.IGNORECASE,
            )
            for pattern in patterns
        )
