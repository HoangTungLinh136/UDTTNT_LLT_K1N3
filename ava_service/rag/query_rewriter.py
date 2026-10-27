class QueryRewriter:

    SYNONYM_MAP = {
        "đi muộn": [
            "đến muộn",
            "trễ giờ",
            "late arrival",
        ],

        "về sớm": [
            "ra về sớm",
            "early leave",
        ],

        "tăng ca": [
            "làm thêm giờ",
            "overtime",
            "OT",
        ],

        "quên chấm công": [
            "thiếu check-in",
            "thiếu check-out",
            "missing attendance",
        ],
    }

    def expand(
        self,
        standalone_question: str,
    ) -> str:

        query = standalone_question

        q_lower = query.lower()

        additions = []

        for key, synonyms in self.SYNONYM_MAP.items():

            if key in q_lower:
                additions.extend(
                    synonyms
                )

        if additions:
            return (
                query
                + " | "
                + " | ".join(additions)
            )

        return query