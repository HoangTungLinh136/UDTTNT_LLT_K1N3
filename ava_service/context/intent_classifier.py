from __future__ import annotations

import re

from core.enums import AiRoute
from core.models import (
    ContextIntentResult,
    ConversationEntity,
    IntentSignals,
)


class IntentClassifier:
    """
    Deterministic-first classifier V17.

    Mục tiêu:
    - Personal profile/salary/contract/leave không rơi GENERAL_LLM.
    - Live aggregate/current value -> LIVE_SQL.
    - Internal policy/process -> RAG.
    - General knowledge -> GENERAL_LLM.
    """

    @staticmethod
    def _normalize(
        text: str,
    ) -> str:
        return re.sub(
            r"\s+",
            " ",
            str(
                text
                or ""
            ).strip().lower(),
        )

    @staticmethod
    def _employee_entities(
        *texts: str,
    ) -> list[ConversationEntity]:
        entities: list[
            ConversationEntity
        ] = []

        seen: set[str] = set()

        for text in texts:
            for match in re.finditer(
                r"\bNV[\s-]*\d{3,}\b",
                text or "",
                re.IGNORECASE,
            ):
                code = re.sub(
                    r"[\s-]+",
                    "",
                    match.group(0),
                ).upper()

                if code in seen:
                    continue

                seen.add(
                    code
                )

                entities.append(
                    ConversationEntity(
                        entity_type=(
                            "employee_code"
                        ),
                        value=code,
                        confidence=0.99,
                    )
                )

        return entities

    @staticmethod
    def _has_any(
        q: str,
        values: tuple[str, ...],
    ) -> bool:
        return any(
            value in q
            for value in values
        )

    def classify(
        self,
        *,
        original_question: str,
        standalone_question: str,
        resolution_confidence: float,
        need_clarification: bool,
        clarification_question: str | None,
    ) -> ContextIntentResult:

        original = self._normalize(
            original_question
        )

        standalone = self._normalize(
            standalone_question
        )

        q = standalone or original

        entities = (
            self._employee_entities(
                original_question,
                standalone_question,
            )
        )

        if need_clarification:
            return ContextIntentResult(
                original_question=(
                    original_question
                ),
                standalone_question=(
                    standalone_question
                ),
                intent="Clarify",
                entities=entities,
                resolution_confidence=(
                    resolution_confidence
                ),
                intent_confidence=(
                    resolution_confidence
                ),
                route_hint=None,
                signals=IntentSignals(),
                need_clarification=True,
                clarification_question=(
                    clarification_question
                ),
            )

        # -------------------------------------------------
        # PERSONAL DATA
        # -------------------------------------------------

        # Protected HR data must never fall through to
        # GENERAL_LLM / Web. This includes self, explicit
        # employee codes, named employees and bulk requests.
        raw_target_text = str(
            standalone_question
            or original_question
            or ""
        )

        has_employee_code = (
            re.search(
                r"\bNV[\s-]*\d{3,}\b",
                raw_target_text,
                re.IGNORECASE,
            )
            is not None
        )

        bulk_personal_scope = self._has_any(
            q,
            (
                "tất cả nhân viên",
                "toàn bộ nhân viên",
                "mọi nhân viên",
                "danh sách nhân viên",
                "danh sách lương",
            ),
        )

        named_person_reference = (
            re.search(
                (
                    r"\b(?:của|nhân viên)\s+"
                    r"([A-ZÀ-ỴĐ]"
                    r"[A-Za-zÀ-ỹĐđ'’-]+"
                    r"(?:\s+[A-ZÀ-ỴĐ]"
                    r"[A-Za-zÀ-ỹĐđ'’-]+){1,4})\b"
                ),
                raw_target_text,
            )
            is not None
        )

        protected_personal_subject = self._has_any(
            q,
            (
                "lương",
                "tiền lương",
                "thu nhập",
                "cccd",
                "cmnd",
                "căn cước",
                "số căn cước",
                "hợp đồng",
                "ngày phép",
                "hồ sơ",
                "thông tin",
            ),
        )

        explicit_self_scope = self._has_any(
            q,
            (
                "của tôi",
                "của mình",
            ),
        )

        personal_protected_request = (
            protected_personal_subject
            and (
                has_employee_code
                or bulk_personal_scope
                or named_person_reference
                or explicit_self_scope
            )
        )

        personal_profile = (
            self._has_any(
                q,
                (
                    "hồ sơ của tôi",
                    "hồ sơ cá nhân của tôi",
                    "hồ sơ nhân sự của tôi",
                    "thông tin hồ sơ của tôi",
                    "thông tin hồ sơ nhân sự của tôi",
                    "hồ sơ nhân viên của tôi",
                    "thông tin nhân sự của tôi",
                    "thông tin của tôi",
                    "thông tin cá nhân của tôi",
                    "thông tin hồ sơ của nhân viên nv",
                    "hồ sơ của nhân viên nv",
                ),
            )
            or (
                "nhân viên nv" in q
                and self._has_any(
                    q,
                    (
                        "hồ sơ",
                        "thông tin",
                        "là ai",
                    ),
                )
            )
        )

        personal_salary = (
            self._has_any(
                q,
                (
                    "lương",
                    "tiền lương",
                    "thu nhập",
                ),
            )
            and (
                "của tôi" in q
                or "nhân viên nv" in q
            )
        )

        personal_position = (
            self._has_any(
                q,
                (
                    "chức vụ",
                    "chức danh",
                    "vị trí công việc",
                    "vị trí hiện tại",
                ),
            )
            and self._has_any(
                q,
                (
                    "của tôi",
                    "tôi",
                    "mình",
                ),
            )
        )

        personal_contract = (
            "hợp đồng" in q
            and (
                "của tôi" in q
                or "nhân viên nv" in q
            )
        )

        personal_leave = (
            self._has_any(
                q,
                (
                    "nghỉ phép",
                    "đơn nghỉ",
                ),
            )
            and (
                "của tôi" in q
                or "nhân viên nv" in q
            )
        )

        personal_leave_balance = (
            self._has_any(
                q,
                (
                    "ngày phép",
                    "số ngày phép",
                    "phép còn lại",
                    "phép còn bao nhiêu",
                ),
            )
            and self._has_any(
                q,
                (
                    "của tôi",
                    "tôi",
                    "mình",
                ),
            )
        )

        personal_late_count = (
            self._has_any(q, ("đi muộn", "đến muộn", "đi trễ"))
            and self._has_any(q, ("mấy lần", "bao nhiêu lần", "số lần"))
            and self._has_any(q, ("của tôi", "tôi", "mình"))
        )

        if (
            personal_protected_request
            or personal_profile
            or personal_salary
            or personal_position
            or personal_contract
            or personal_leave
            or personal_leave_balance
            or personal_late_count
        ):
            return ContextIntentResult(
                original_question=(
                    original_question
                ),
                standalone_question=(
                    standalone_question
                ),
                intent="Personal",
                entities=entities,
                resolution_confidence=(
                    resolution_confidence
                ),
                intent_confidence=0.99,
                route_hint=(
                    AiRoute.PERSONAL_DATA
                ),
                signals=IntentSignals(
                    requires_user_specific_data=True,
                ),
            )

        # -------------------------------------------------
        # LIVE SQL
        # -------------------------------------------------

        # FINAL DEPARTMENT PEOPLE COUNT
        department_people_count = (
            self._has_any(
                q,
                (
                    "phòng ",
                    "phòng ban",
                    "bộ phận",
                ),
            )
            and self._has_any(
                q,
                (
                    "bao nhiêu người",
                    "bao nhiêu nhân viên",
                    "số người",
                    "số nhân viên",
                ),
            )
        )

        if department_people_count:
            return ContextIntentResult(
                original_question=original_question,
                standalone_question=standalone_question,
                intent="LiveData",
                entities=entities,
                resolution_confidence=resolution_confidence,
                intent_confidence=0.99,
                route_hint=AiRoute.LIVE_SQL,
                signals=IntentSignals(
                    requires_current_hrms_value=True,
                ),
            )

        live_patterns = (
            "bao nhiêu nhân viên",
            "số nhân viên",
            "tổng nhân viên",
            "danh sách nhân viên",
            "liệt kê nhân viên",
            "nhân viên hiện tại gồm những ai",
            "nhân viên gồm những ai",
            "những nhân viên nào",
            "kể tên nhân viên",
            "bao nhiêu hợp đồng",
            "số hợp đồng",
            "tổng hợp đồng",
            "bao nhiêu đơn nghỉ",
            "số đơn nghỉ",
            "chấm công hôm nay",
            "lượt chấm công hôm nay",
            "hợp đồng sắp hết hạn",
            "đơn nghỉ chờ duyệt",
            "tổng chi phí lương",
            "bao nhiêu nhân viên đi muộn",
            "số nhân viên đi muộn",
            "bao nhiêu người đi muộn",
        )

        live_verification = (
            self._has_any(
                q,
                (
                    "đúng không",
                    "có đúng không",
                    "phải không",
                    "có phải",
                ),
            )
            and self._has_any(
                q,
                (
                    "nhân viên",
                    "phòng ",
                    "phòng ban",
                    "bộ phận",
                    "đơn nghỉ",
                    "nghỉ phép",
                    "chấm công",
                    "đi muộn",
                    "hợp đồng",
                    "lương",
                ),
            )
        )

        if (
            self._has_any(
                q,
                live_patterns,
            )
            or live_verification
        ):
            return ContextIntentResult(
                original_question=(
                    original_question
                ),
                standalone_question=(
                    standalone_question
                ),
                intent="LiveData",
                entities=entities,
                resolution_confidence=(
                    resolution_confidence
                ),
                intent_confidence=0.98,
                route_hint=(
                    AiRoute.LIVE_SQL
                ),
                signals=IntentSignals(
                    requires_current_hrms_value=True,
                ),
            )

        # -------------------------------------------------
        # INTERNAL KNOWLEDGE / RAG
        # -------------------------------------------------

        internal_patterns = (
            "quy định",
            "chính sách",
            "quy trình",
            "đi muộn",
            "đến muộn",
            "về sớm",
            "check-in",
            "check-out",
            "chấm công",
            "tăng ca",
            "làm thêm giờ",
            "nội quy",
            "phúc lợi",
            "nghỉ phép được",
            "xử lý thế nào",
        )

        if self._has_any(
            q,
            internal_patterns,
        ):
            return ContextIntentResult(
                original_question=(
                    original_question
                ),
                standalone_question=(
                    standalone_question
                ),
                intent="Knowledge",
                entities=entities,
                resolution_confidence=(
                    resolution_confidence
                ),
                intent_confidence=0.96,
                route_hint=AiRoute.RAG,
                signals=IntentSignals(
                    requires_internal_knowledge=True,
                ),
            )

        # -------------------------------------------------
        # GENERAL
        # -------------------------------------------------

        return ContextIntentResult(
            original_question=(
                original_question
            ),
            standalone_question=(
                standalone_question
            ),
            intent="General",
            entities=entities,
            resolution_confidence=(
                resolution_confidence
            ),
            intent_confidence=0.90,
            route_hint=(
                AiRoute.GENERAL_LLM
            ),
            signals=IntentSignals(
                requires_general_knowledge=True,
            ),
        )

