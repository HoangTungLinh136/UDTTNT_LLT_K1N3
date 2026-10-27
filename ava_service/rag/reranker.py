from __future__ import annotations

import re
import unicodedata

from config.settings import settings


class RagReranker:
    """
    Phase 15B - Query-aware lexical reranker.

    Nguyên tắc:
    1. Giữ threshold distance hiện tại.
    2. Không bỏ semantic distance.
    3. Dùng lexical affinity từ question với:
       - metadata.title
       - metadata.section
       - metadata.category
       - document text
    4. Chỉ giữ evidence đủ gần với best item để giảm nhiễu.
    """

    SCORE_GAP_LIMIT = 0.18

    # P2-A WEAK SEMANTIC MATCH GUARD
    WEAK_MATCH_DISTANCE = 0.70
    MIN_LEXICAL_AFFINITY_FOR_WEAK_MATCH = 0.20
    MIN_LEXICAL_AFFINITY = 0.12

    TITLE_WEIGHT = 0.45
    SECTION_WEIGHT = 0.20
    CATEGORY_WEIGHT = 0.15
    DOCUMENT_WEIGHT = 0.20

    STOPWORDS = {
        "la",
        "gi",
        "cua",
        "the",
        "nao",
        "nhu",
        "hien",
        "tai",
        "he",
        "thong",
        "quy",
        "dinh",
        "nhan",
        "vien",
        "co",
        "bao",
        "nhieu",
        "duoc",
        "neu",
        "thi",
        "xu",
        "ly",
        "va",
        "hoac",
        "trong",
        "ve",
        "cho",
        "toi",
    }

    # =====================================================
    # TEXT NORMALIZATION
    # =====================================================

    def _normalize(
        self,
        text: str | None,
    ) -> str:

        if not text:
            return ""

        normalized = unicodedata.normalize(
            "NFD",
            str(text).lower(),
        )

        normalized = "".join(
            char
            for char in normalized
            if unicodedata.category(char)
            != "Mn"
        )

        normalized = normalized.replace(
            "đ",
            "d",
        )

        normalized = re.sub(
            r"[^a-z0-9]+",
            " ",
            normalized,
        )

        return re.sub(
            r"\s+",
            " ",
            normalized,
        ).strip()

    def _tokens(
        self,
        text: str | None,
    ) -> set[str]:

        normalized = self._normalize(
            text
        )

        return {
            token
            for token in normalized.split()
            if (
                len(token) >= 2
                and token
                not in self.STOPWORDS
            )
        }

    # =====================================================
    # AFFINITY
    # =====================================================

    def _overlap(
        self,
        question_tokens: set[str],
        text: str | None,
    ) -> float:

        if not question_tokens:
            return 0.0

        text_tokens = self._tokens(
            text
        )

        if not text_tokens:
            return 0.0

        matched = (
            question_tokens
            .intersection(
                text_tokens
            )
        )

        return (
            len(matched)
            / len(question_tokens)
        )

    def _lexical_affinity(
        self,
        question: str,
        item: dict,
    ) -> float:

        metadata = (
            item.get("metadata")
            or {}
        )

        question_tokens = (
            self._tokens(
                question
            )
        )

        title_score = self._overlap(
            question_tokens,
            metadata.get("title"),
        )

        section_score = self._overlap(
            question_tokens,
            metadata.get("section"),
        )

        category_score = self._overlap(
            question_tokens,
            metadata.get("category"),
        )

        document_score = self._overlap(
            question_tokens,
            item.get("document"),
        )

        return (
            title_score
            * self.TITLE_WEIGHT
            +
            section_score
            * self.SECTION_WEIGHT
            +
            category_score
            * self.CATEGORY_WEIGHT
            +
            document_score
            * self.DOCUMENT_WEIGHT
        )

    # =====================================================
    # RERANK
    # =====================================================

    def rerank(
        self,
        candidates: list[dict],
        question: str | None = None,
    ) -> list[dict]:

        valid: list[dict] = []

        for item in candidates:

            distance = item.get(
                "distance"
            )

            if distance is None:
                continue

            distance = float(
                distance
            )

            if (
                distance
                > settings.RAG_MAX_DISTANCE
            ):
                continue

            lexical_affinity = 0.0

            if question:
                lexical_affinity = (
                    self._lexical_affinity(
                        question,
                        item,
                    )
                )

                # Fail closed for internal-policy questions whose semantic
                # vector is merely nearby but shares no meaningful business
                # vocabulary with the evidence.  This prevents unrelated
                # policy answers from being generated from weak matches.
                if lexical_affinity < self.MIN_LEXICAL_AFFINITY:
                    continue

            # Nếu semantic match đã yếu thì phải có
            # lexical overlap đủ rõ với câu hỏi.
            #
            # Fail-closed cho tri thức nội bộ:
            # không dùng chunk gần về mặt embedding nhưng
            # khác chủ đề để tạo câu trả lời.
            if (
                question
                and distance
                >= self.WEAK_MATCH_DISTANCE
                and lexical_affinity
                < self.MIN_LEXICAL_AFFINITY_FOR_WEAK_MATCH
            ):
                continue

            adjusted_score = (
                distance
                - lexical_affinity
            )

            enriched = dict(
                item
            )

            enriched[
                "rerank_lexical_affinity"
            ] = round(
                lexical_affinity,
                6,
            )

            enriched[
                "rerank_score"
            ] = round(
                adjusted_score,
                6,
            )

            valid.append(
                enriched
            )

        valid.sort(
            key=lambda item: (
                item[
                    "rerank_score"
                ],
                float(
                    item[
                        "distance"
                    ]
                ),
            )
        )

        if not valid:
            return []

        best_score = valid[0][
            "rerank_score"
        ]

        pruned = [
            item
            for item in valid
            if (
                item[
                    "rerank_score"
                ]
                - best_score
                <= self.SCORE_GAP_LIMIT
            )
        ]

        # Luôn giữ ít nhất best evidence.
        if not pruned:
            pruned = [
                valid[0]
            ]

        return pruned[
            :settings.RAG_FINAL_TOP_K
        ]
