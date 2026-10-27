from __future__ import annotations

import re
import unicodedata

from context.entity_resolver import (
    EntityResolver,
)

from context.time_scope_resolver import (
    TimeScopeResolver,
)

from context.semantic_follow_up_resolver import (
    SemanticFollowUpResolver,
)

from core.models import (
    ConversationEntity,
    ConversationState,
    NormalizedAiInput,
)


class ContextResolver:

    def __init__(self):

        self.time_scope_resolver = (
            TimeScopeResolver()
        )

        self.entity_resolver = (
            EntityResolver()
        )

        self.semantic_follow_up_resolver = (
            SemanticFollowUpResolver()
        )

    # =====================================================
    # NORMALIZE
    # =====================================================

    def normalize_text(
        self,
        text: str,
    ) -> str:

        if not text:
            return ""

        text = unicodedata.normalize(
            "NFC",
            text,
        )

        text = re.sub(
            r"\s+",
            " ",
            text,
        )

        return text.strip()

    # =====================================================
    # EMPLOYEE CODE
    # =====================================================

    def _extract_employee_code(
        self,
        text: str,
    ) -> str | None:

        if not text:
            return None

        match = re.search(
            r"\bNV[\s-]*\d{3,}\b",
            text,
            re.IGNORECASE,
        )

        if not match:
            return None

        return re.sub(
            r"[\s-]+",
            "",
            match.group(0),
        ).upper()

    # =====================================================
    # TOPIC DETECTION
    # =====================================================

    def detect_topic(
        self,
        text: str,
    ) -> str | None:

        q = (
            self.normalize_text(
                text
            )
            .lower()
        )

        employee_code = (
            self._extract_employee_code(
                text
            )
        )

        # =================================================
        # ATTENDANCE KNOWLEDGE
        # =================================================

        if any(
            term in q
            for term in [
                "đi muộn",
                "đến muộn",
                "đi làm trễ",
                "đến trễ",
                "late arrival",
            ]
        ):

            return (
                "attendance_late"
            )

        if any(
            term in q
            for term in [
                "về sớm",
                "tan làm sớm",
                "về trước giờ",
                "early leave",
            ]
        ):

            return (
                "attendance_early_leave"
            )

        if any(
            term in q
            for term in [
                "quên check-in",
                "thiếu check-in",
                "không check-in",
                "quên chấm công vào",
                "thiếu giờ vào",
            ]
        ):

            return (
                "attendance_missing_checkin"
            )

        if any(
            term in q
            for term in [
                "quên check-out",
                "thiếu check-out",
                "không check-out",
                "quên chấm công ra",
                "thiếu giờ ra",
            ]
        ):

            return (
                "attendance_missing_checkout"
            )

        if any(
            term in q
            for term in [
                "tăng ca",
                "làm thêm giờ",
                "overtime",
                "làm thêm sau ca",
                " ot ",
            ]
        ):

            return (
                "attendance_overtime"
            )

        # =================================================
        # LIVE DATA
        #
        # Phải check count trước generic contract/profile
        # để:
        #
        # "Có bao nhiêu hợp đồng?"
        #
        # không bị hiểu thành PersonalData.
        # =================================================

        # =================================================
        # EMPLOYEE LIST
        #
        # Chỉ coi là topic độc lập khi câu có "nhân viên".
        # Câu ngắn "gồm những ai" chỉ được hiểu nhờ history
        # ở resolve_question(), không tự đoán khi chat mới.
        # =================================================

        if (
            "nhân viên" in q
            and any(
                term in q
                for term in [
                    "danh sách nhân viên",
                    "liệt kê nhân viên",
                    "nhân viên gồm những ai",
                    "nhân viên hiện tại gồm những ai",
                    "những nhân viên nào",
                    "có những nhân viên nào",
                    "kể tên nhân viên",
                ]
            )
        ):
            return "employee_list"

        if any(
            term in q
            for term in [
                "bao nhiêu nhân viên",
                "số nhân viên",
                "tổng nhân viên",
                "hiện có bao nhiêu nhân viên",
            ]
        ):

            return (
                "employee_count"
            )

        if any(
            term in q
            for term in [
                "bao nhiêu hợp đồng",
                "số hợp đồng",
                "tổng hợp đồng",
            ]
        ):

            return (
                "contract_count"
            )

        if any(
            term in q
            for term in [
                "bao nhiêu đơn nghỉ",
                "bao nhiêu đơn nghỉ phép",
                "số đơn nghỉ",
                "số đơn nghỉ phép",
            ]
        ):

            return (
                "leave_count"
            )

        if any(
            term in q
            for term in [
                "chấm công hôm nay",
                "lượt chấm công hôm nay",
                "bao nhiêu lượt chấm công hôm nay",
                "bao nhiêu người chấm công hôm nay",
            ]
        ):

            return (
                "attendance_today"
            )

        # =================================================
        # PERSONAL SALARY
        # =================================================

        salary_terms = [
            "lương",
            "tiền lương",
            "thu nhập",
        ]

        if any(
            term in q
            for term in salary_terms
        ):

            if (
                employee_code
                or
                "của tôi" in q
                or
                "lương tháng này" in q
                or
                "lương tháng trước" in q
            ):

                return (
                    "my_salary"
                )

        # =================================================
        # PERSONAL CONTRACT
        #
        # "Còn hợp đồng?"
        # được hiểu là personal follow-up.
        #
        # Count contract đã được bắt ở phía trên.
        # =================================================

        if (
            "hợp đồng" in q
            and
            not any(
                term in q
                for term in [
                    "bao nhiêu hợp đồng",
                    "số hợp đồng",
                    "tổng hợp đồng",
                ]
            )
        ):

            if (
                employee_code
                or
                "của tôi" in q
                or
                self.is_follow_up(
                    text
                )
            ):

                return (
                    "my_contract"
                )

        # =================================================
        # PERSONAL LEAVE
        # =================================================

        if any(
            term in q
            for term in [
                "đơn nghỉ phép",
                "đơn nghỉ",
                "nghỉ phép",
                "lịch sử nghỉ phép",
            ]
        ):

            if (
                employee_code
                or
                "của tôi" in q
                or
                self.is_follow_up(
                    text
                )
            ):

                return (
                    "my_leave"
                )

        # =================================================
        # PERSONAL PROFILE / SELF IDENTITY
        #
        # Examples:
        #   "toi la ai?"
        #   "chao cau toi la ai??"
        #   "ten toi la gi?"
        #
        # These are PERSONAL_DATA questions, not General LLM.
        # =================================================

        if any(
            term in q
            for term in [
                "tôi là ai",
                "mình là ai",
                "tôi tên gì",
                "tên tôi là gì",
                "tên của tôi là gì",
                "cho tôi biết tôi là ai",
                "tôi là nhân viên nào",
                "mã nhân viên của tôi",
            ]
        ):
            return "my_profile"

        if any(
            term in q
            for term in [
                "hồ sơ",
                "thông tin cá nhân",
                "thông tin nhân viên",
            ]
        ):

            if (
                employee_code
                or
                "của tôi" in q
            ):

                return (
                    "my_profile"
                )

        if (
            employee_code
            and
            "thông tin" in q
        ):

            return (
                "my_profile"
            )

        return None

    # =====================================================
    # FOLLOW-UP
    # =====================================================

    def is_follow_up(
        self,
        text: str,
    ) -> bool:

        q = (
            self.normalize_text(
                text
            )
            .lower()
        )

        patterns = [
            r"^thế còn\b",
            r"^còn\b",
            r"^thế\b",
            r"^vậy còn\b",
            r"^vậy\b",
            r"^cái đó\b",
            r"^cái kia\b",
            r"^trường hợp đó\b",
            r"^nó thì sao\b",
            r"^thì sao\b",
            r"^gồm những ai\??$",
            r"^có những ai\??$",
            r"^là những ai\??$",
            r"^những ai\??$",
            r"^họ là ai\??$",
            r"^kể tên(?: đi)?\??$",
            r"^liệt kê(?: đi)?\??$",
            r"^danh sách(?: đi)?\??$",
        ]

        return any(
            re.search(
                pattern,
                q,
                re.IGNORECASE,
            )
            for pattern
            in patterns
        )

    # =====================================================
    # TOPIC -> OWN QUESTION
    # =====================================================

    def topic_to_question(
        self,
        topic: str,
    ) -> str | None:

        mapping = {

            "attendance_late":
                (
                    "Đi muộn bị xử lý "
                    "thế nào?"
                ),

            "attendance_early_leave":
                (
                    "Về sớm bị xử lý "
                    "thế nào?"
                ),

            "attendance_missing_checkin":
                (
                    "Quên check-in phải "
                    "xử lý thế nào?"
                ),

            "attendance_missing_checkout":
                (
                    "Quên check-out phải "
                    "xử lý thế nào?"
                ),

            "attendance_overtime":
                (
                    "Quy định tăng ca và "
                    "làm thêm giờ như thế nào?"
                ),

            "my_salary":
                (
                    "Lương của tôi "
                    "như thế nào?"
                ),

            "my_contract":
                (
                    "Hợp đồng của tôi "
                    "như thế nào?"
                ),

            "my_leave":
                (
                    "Đơn nghỉ phép của tôi "
                    "như thế nào?"
                ),

            "my_profile":
                (
                    "Thông tin hồ sơ "
                    "của tôi là gì?"
                ),

            "employee_count":
                (
                    "Hiện có bao nhiêu "
                    "nhân viên?"
                ),

            "employee_list":
                (
                    "Danh sách nhân viên "
                    "hiện tại gồm những ai?"
                ),

            "contract_count":
                (
                    "Hiện có bao nhiêu "
                    "hợp đồng?"
                ),

            "leave_count":
                (
                    "Hiện có bao nhiêu "
                    "đơn nghỉ phép?"
                ),

            "attendance_today":
                (
                    "Có bao nhiêu lượt "
                    "chấm công hôm nay?"
                ),
        }

        return mapping.get(
            topic
        )

    # =====================================================
    # TOPIC -> EMPLOYEE QUESTION
    # =====================================================

    def _topic_to_employee_question(
        self,
        topic: str,
        employee_code: str,
    ) -> str | None:

        if topic == "my_salary":

            return (
                "Lương của nhân viên "
                f"{employee_code} "
                "như thế nào?"
            )

        if topic == "my_contract":

            return (
                "Hợp đồng của nhân viên "
                f"{employee_code} "
                "như thế nào?"
            )

        if topic == "my_leave":

            return (
                "Đơn nghỉ phép của nhân viên "
                f"{employee_code} "
                "như thế nào?"
            )

        if topic == "my_profile":

            return (
                "Thông tin hồ sơ của nhân viên "
                f"{employee_code} là gì?"
            )

        return None

    # =====================================================
    # ACTIVE EMPLOYEE CODE
    # =====================================================

    def _active_employee_code(
        self,
        state: ConversationState,
    ) -> str | None:

        entity = (
            self._find_active_entity(
                state=state,
                entity_type=(
                    "employee_code"
                ),
            )
        )

        if entity is None:
            return None

        return str(
            entity.value
        ).strip().upper()

    # =====================================================
    # BUILD TEMPORAL QUESTION
    # =====================================================

    def _rewrite_previous_question_time(
        self,
        state: ConversationState,
        time_scope: str,
    ) -> str | None:
        """Replace only the time phrase in the previous user question.

        This deterministic rewrite preserves the previous operation, metric,
        target and route semantics.  It does not infer facts or permissions.
        """

        task_state = (
            state.task_state
            if isinstance(state.task_state, dict)
            else {}
        )
        previous = self.normalize_text(
            str(task_state.get("previous_user_question", ""))
        )

        if not previous:
            return None

        replacements = {
            "current_month": "Tháng này",
            "previous_month": "Tháng trước",
            "today": "Hôm nay",
            "yesterday": "Hôm qua",
            "current_year": "Năm nay",
            "previous_year": "Năm ngoái",
        }

        target = replacements.get(time_scope)
        if time_scope.startswith("month:"):
            target = f"Tháng {int(time_scope.split(':', 1)[1])}"

        if not target:
            return None

        time_pattern = (
            r"\b(?:hôm nay|hôm qua|tháng này|tháng trước|"
            r"tháng vừa rồi|tháng hiện tại|năm nay|năm ngoái|"
            r"năm trước|tháng\s+(?:0?[1-9]|1[0-2]))\b"
        )
        def replace_time(match: re.Match[str]) -> str:
            if match.start() == 0:
                return target
            return target[:1].lower() + target[1:]

        rewritten, count = re.subn(
            time_pattern,
            replace_time,
            previous,
            count=1,
            flags=re.IGNORECASE,
        )

        if count == 0:
            return None

        return rewritten

    def _build_temporal_question(
        self,
        topic: str,
        time_scope: str,
        employee_code: str | None = None,
    ) -> str | None:

        # =================================================
        # SALARY
        # =================================================

        if topic == "my_salary":

            if employee_code:

                if (
                    time_scope
                    == "current_month"
                ):

                    return (
                        "Lương tháng này của "
                        f"nhân viên {employee_code} "
                        "như thế nào?"
                    )

                if (
                    time_scope
                    == "previous_month"
                ):

                    return (
                        "Lương tháng trước của "
                        f"nhân viên {employee_code} "
                        "như thế nào?"
                    )

                if time_scope.startswith(
                    "month:"
                ):

                    month = (
                        time_scope.split(
                            ":",
                            1,
                        )[1]
                    )

                    return (
                        f"Lương tháng {month} "
                        "của nhân viên "
                        f"{employee_code} "
                        "như thế nào?"
                    )

            # ---------------------------------------------
            # OWN SALARY
            # ---------------------------------------------

            if (
                time_scope
                == "current_month"
            ):

                return (
                    "Lương tháng này của tôi "
                    "như thế nào?"
                )

            if (
                time_scope
                == "previous_month"
            ):

                return (
                    "Lương tháng trước của tôi "
                    "như thế nào?"
                )

            if time_scope.startswith(
                "month:"
            ):

                month = (
                    time_scope.split(
                        ":",
                        1,
                    )[1]
                )

                return (
                    f"Lương tháng {month} "
                    "của tôi như thế nào?"
                )

        # =================================================
        # ATTENDANCE
        # =================================================

        if (
            topic
            == "attendance_today"
        ):

            if (
                time_scope
                == "today"
            ):

                return (
                    "Có bao nhiêu lượt "
                    "chấm công hôm nay?"
                )

            if (
                time_scope
                == "yesterday"
            ):

                return (
                    "Có bao nhiêu lượt "
                    "chấm công hôm qua?"
                )

        # =================================================
        # GENERIC
        # =================================================

        if employee_code:

            employee_question = (
                self._topic_to_employee_question(
                    topic=topic,
                    employee_code=(
                        employee_code
                    ),
                )
            )

            if employee_question:

                return (
                    f"{employee_question} "
                    "Phạm vi thời gian: "
                    f"{time_scope}."
                )

        base_question = (
            self.topic_to_question(
                topic
            )
        )

        if not base_question:
            return None

        return (
            f"{base_question} "
            "Phạm vi thời gian: "
            f"{time_scope}."
        )

    # =====================================================
    # BUILD ENTITY QUESTION
    # =====================================================

    def _build_entity_question(
        self,
        topic: str,
        entity: dict,
    ) -> str | None:

        entity_type = (
            entity.get(
                "type"
            )
        )

        entity_value = (
            entity.get(
                "value"
            )
        )

        if not entity_type:
            return None

        if not entity_value:
            return None

        # =================================================
        # DEPARTMENT
        # =================================================

        if (
            topic
            == "employee_count"
            and
            entity_type
            == "department"
        ):

            return (
                "Hiện có bao nhiêu nhân viên "
                f"thuộc phòng {entity_value}?"
            )

        if (
            topic
            == "contract_count"
            and
            entity_type
            == "department"
        ):

            return (
                "Hiện có bao nhiêu hợp đồng "
                "liên quan đến phòng "
                f"{entity_value}?"
            )

        if (
            topic
            == "employee_list"
            and
            entity_type
            == "department"
        ):

            return (
                "Danh sách nhân viên "
                f"thuộc phòng {entity_value} "
                "gồm những ai?"
            )

        # =================================================
        # EMPLOYEE
        # =================================================

        if (
            entity_type
            == "employee_code"
        ):

            employee_question = (
                self._topic_to_employee_question(
                    topic=topic,
                    employee_code=(
                        str(
                            entity_value
                        ).upper()
                    ),
                )
            )

            if employee_question:
                return employee_question

            base_question = (
                self.topic_to_question(
                    topic
                )
            )

            if base_question:

                return (
                    f"{base_question} "
                    "Nhân viên có mã "
                    f"{entity_value}."
                )

        return None

    # =====================================================
    # FIND ACTIVE ENTITY
    # =====================================================

    def _find_active_entity(
        self,
        state: ConversationState,
        entity_type: str,
    ) -> ConversationEntity | None:

        for entity in reversed(
            state.active_entities
        ):

            if (
                entity.entity_type
                == entity_type
            ):

                return entity

        return None

    # =====================================================
    # RESOLVE ENTITY COREFERENCE
    # =====================================================

    def _resolve_entity_coreference(
        self,
        current: str,
        state: ConversationState,
    ) -> tuple[
        str,
        float,
        bool,
        str | None,
    ] | None:

        if not (
            self.entity_resolver
            .is_coreference(
                current
            )
        ):

            return None

        q = (
            current
            .lower()
            .strip()
        )

        # =================================================
        # DEPARTMENT
        # =================================================

        department_words = [
            "phòng đó",
            "phòng ấy",
            "phòng này",
            "bộ phận đó",
            "bộ phận ấy",
            "bộ phận này",
        ]

        if any(
            term in q
            for term
            in department_words
        ):

            entity = (
                self._find_active_entity(
                    state=state,
                    entity_type=(
                        "department"
                    ),
                )
            )

            if entity is None:

                return (
                    current,
                    0.25,
                    True,
                    (
                        "Bạn đang nhắc tới "
                        "phòng ban nào?"
                    ),
                )

            if (
                state.active_topic
                == "employee_count"
            ):

                return (
                    (
                        "Hiện có bao nhiêu nhân viên "
                        f"thuộc phòng {entity.value}?"
                    ),
                    0.93,
                    False,
                    None,
                )

            if (
                state.active_topic
                == "contract_count"
            ):

                return (
                    (
                        "Hiện có bao nhiêu hợp đồng "
                        "liên quan đến phòng "
                        f"{entity.value}?"
                    ),
                    0.88,
                    False,
                    None,
                )

            base_question = (
                self.topic_to_question(
                    state.active_topic
                )
                if state.active_topic
                else None
            )

            if base_question:

                return (
                    (
                        f"{base_question} "
                        "Phòng được nhắc tới là "
                        f"{entity.value}."
                    ),
                    0.80,
                    False,
                    None,
                )

            return (
                current,
                0.30,
                True,
                (
                    "Bạn muốn hỏi gì về "
                    f"phòng {entity.value}?"
                ),
            )

        # =================================================
        # EMPLOYEE COREFERENCE
        # =================================================

        employee_words = [
            "nhân viên đó",
            "nhân viên ấy",
            "nhân viên này",
            "người đó",
            "người ấy",
            "người này",
        ]

        if any(
            term in q
            for term
            in employee_words
        ):

            entity = (
                self._find_active_entity(
                    state=state,
                    entity_type=(
                        "employee_code"
                    ),
                )
            )

            if entity is None:

                return (
                    current,
                    0.25,
                    True,
                    (
                        "Bạn đang nhắc tới "
                        "nhân viên nào?"
                    ),
                )

            if state.active_topic:

                employee_question = (
                    self._topic_to_employee_question(
                        topic=(
                            state.active_topic
                        ),
                        employee_code=(
                            str(
                                entity.value
                            ).upper()
                        ),
                    )
                )

                if employee_question:

                    return (
                        employee_question,
                        0.90,
                        False,
                        None,
                    )

                base_question = (
                    self.topic_to_question(
                        state.active_topic
                    )
                )

                if base_question:

                    return (
                        (
                            f"{base_question} "
                            "Nhân viên có mã "
                            f"{entity.value}."
                        ),
                        0.85,
                        False,
                        None,
                    )

            return (
                current,
                0.30,
                True,
                (
                    "Bạn muốn hỏi thông tin gì "
                    "về nhân viên "
                    f"{entity.value}?"
                ),
            )

        return None

    # =====================================================
    # CONTRACT FOLLOW-UP
    # =====================================================

    def _resolve_contract_follow_up(
        self,
        current: str,
        state: ConversationState,
    ) -> tuple[
        str,
        float,
        bool,
        str | None,
    ] | None:

        q = (
            current
            .lower()
            .strip()
        )

        if (
            state.active_topic
            != "my_contract"
        ):

            return None

        expiry_patterns = [
            r"^khi nào hết hạn\??$",
            r"^bao giờ hết hạn\??$",
            r"^ngày hết hạn là khi nào\??$",
            r"^hết hạn khi nào\??$",
        ]

        if not any(
            re.search(
                pattern,
                q,
                re.IGNORECASE,
            )
            for pattern
            in expiry_patterns
        ):

            return None

        employee_code = (
            self._active_employee_code(
                state
            )
        )

        if employee_code:

            return (
                (
                    "Hợp đồng của nhân viên "
                    f"{employee_code} "
                    "khi nào hết hạn?"
                ),
                0.92,
                False,
                None,
            )

        return (
            (
                "Hợp đồng của tôi "
                "khi nào hết hạn?"
            ),
            0.92,
            False,
            None,
        )

    # =====================================================
    # EMPLOYEE PROFILE / IDENTITY QUESTION
    # =====================================================

    def _is_employee_profile_question(
        self,
        text: str,
    ) -> bool:
        q = (
            self.normalize_text(
                text
            )
            .lower()
        )

        patterns = [
            r"\blà ai\b",
            r"\bai là\b",
            r"\bthông tin về\b",
            r"\bcho tôi biết về\b",
            r"\bhồ sơ\b",
            r"\bprofile\b",
            r"\bngười nào\b",
        ]

        return any(
            re.search(
                pattern,
                q,
                re.IGNORECASE,
            )
            for pattern in patterns
        )

    # =====================================================
    # RESOLVE QUESTION
    # =====================================================

    def resolve_question(
        self,
        ai_input: NormalizedAiInput,
        state: ConversationState,
    ) -> tuple[
        str,
        float,
        bool,
        str | None,
    ]:

        current = (
            self.normalize_text(
                ai_input.text
            )
        )

        # =================================================
        # 1. EMPTY
        # =================================================

        if not current:

            return (
                "",
                0.0,
                True,
                (
                    "Bạn muốn hỏi "
                    "nội dung gì?"
                ),
            )

        # =================================================
        # 2. TIME SCOPE
        # =================================================

        current_time_scope = (
            self.time_scope_resolver
            .detect(
                current
            )
        )

        # =================================================
        # 3. ENTITY
        # =================================================

        current_entity = (
            self.entity_resolver
            .detect(
                current
            )
        )

        # -------------------------------------------------
        # Fallback employee-code detection
        #
        # Các câu ngắn như "Còn NV002?" có thể không được
        # EntityResolver đánh dấu là entity follow-up.
        # Mã NVxxx là định danh rõ ràng nên bắt deterministic.
        # -------------------------------------------------

        if not current_entity:

            fallback_employee_code = (
                self._extract_employee_code(
                    current
                )
            )

            if fallback_employee_code:

                current_entity = {
                    "type": "employee_code",
                    "value": fallback_employee_code,
                    "confidence": 0.98,
                }

        # =================================================
        # 4. CURRENT TOPIC
        #
        # Detect sớm để:
        #
        # "Thế lương tháng trước?"
        #
        # không dùng nhầm active_topic trước đó.
        # =================================================

        current_topic = (
            self.detect_topic(
                current
            )
        )

        active_employee_code = (
            self._active_employee_code(
                state
            )
        )

        # V19.1 SELF IDENTITY HOTFIX
        #
        # Clear self-identity questions must become
        # canonical PERSONAL_DATA questions immediately.

        self_identity_markers = (
            "t\u00f4i l\u00e0 ai",
            "m\u00ecnh l\u00e0 ai",
            "t\u00f4i t\u00ean g\u00ec",
            "t\u00ean t\u00f4i l\u00e0 g\u00ec",
            "t\u00ean c\u1ee7a t\u00f4i l\u00e0 g\u00ec",
            "t\u00f4i l\u00e0 nh\u00e2n vi\u00ean n\u00e0o",
            "m\u00e3 nh\u00e2n vi\u00ean c\u1ee7a t\u00f4i",
        )

        if (
            current_topic == "my_profile"
            and any(
                item in current.lower()
                for item in self_identity_markers
            )
        ):
            return (
                "Th\u00f4ng tin h\u1ed3 s\u01a1 c\u1ee7a t\u00f4i l\u00e0 g\u00ec?",
                0.99,
                False,
                None,
            )


        # =================================================
        # 4.0 HISTORY TRANSITION: COUNT -> LIST
        #
        # User:
        #   "Hiện có bao nhiêu nhân viên?"
        # Assistant:
        #   "Hiện hệ thống có 6 nhân viên."
        # User:
        #   "gồm những ai"
        #
        # active_topic = employee_count
        # => rewrite thành employee_list, KHÔNG rơi General LLM.
        # =================================================

        list_follow_up_patterns = [
            r"^gồm những ai\??$",
            r"^có những ai\??$",
            r"^là những ai\??$",
            r"^những ai\??$",
            r"^họ là ai\??$",
            r"^kể tên(?: đi)?\??$",
            r"^liệt kê(?: đi)?\??$",
            r"^danh sách(?: đi)?\??$",
        ]

        is_list_follow_up = any(
            re.search(
                pattern,
                current.lower().strip(),
                re.IGNORECASE,
            )
            for pattern
            in list_follow_up_patterns
        )

        if (
            is_list_follow_up
            and state.active_topic
            in {
                "employee_count",
                "employee_list",
            }
        ):
            active_department = (
                self._find_active_entity(
                    state=state,
                    entity_type="department",
                )
            )

            if active_department is not None:
                return (
                    (
                        "Danh sách nhân viên "
                        f"thuộc phòng {active_department.value} "
                        "gồm những ai?"
                    ),
                    0.98,
                    False,
                    None,
                )

            return (
                "Danh sách nhân viên hiện tại gồm những ai?",
                0.98,
                False,
                None,
            )

        # =================================================
        # 4.1 EMPLOYEE NAME/CODE -> PROFILE
        #
        # Ví dụ:
        #   "Hoàng Tùng Linh là ai"
        #
        # EntityResolver:
        #   Hoàng Tùng Linh -> NV001
        #
        # Nếu NV001 cũng là identity đang xác thực:
        #   rewrite -> "Hồ sơ của tôi"
        #
        # Như vậy PersonalDataService dùng đúng SQL HRMS,
        # không để GENERAL_LLM đoán nhân viên nội bộ.
        # =================================================

        if (
            current_entity
            and current_entity.get(
                "type"
            )
            == "employee_code"
            and self._is_employee_profile_question(
                current
            )
        ):
            target_employee_code = str(
                current_entity.get(
                    "value"
                )
            ).strip().upper()

            if (
                active_employee_code
                and target_employee_code
                == active_employee_code
            ):
                return (
                    "Hồ sơ của tôi",
                    0.99,
                    False,
                    None,
                )

            # Nhân viên khác vẫn phải đi Personal route
            # để authorization/data-scope quyết định,
            # tuyệt đối không fallback General LLM.
            return (
                (
                    "Thông tin hồ sơ của nhân viên "
                    f"{target_employee_code} là gì?"
                ),
                0.97,
                False,
                None,
            )

        # =================================================
        # 5. TEMPORAL FOLLOW-UP
        # =================================================

        if (
            current_time_scope
            and
            self.time_scope_resolver
            .is_time_follow_up(
                current
            )
        ):

            history_temporal_question = (
                self._rewrite_previous_question_time(
                    state=state,
                    time_scope=current_time_scope,
                )
            )

            if history_temporal_question:
                return (
                    history_temporal_question,
                    0.97,
                    False,
                    None,
                )

            topic_for_time = (
                current_topic
                or
                state.active_topic
            )

            if topic_for_time:

                # -----------------------------------------
                # Nếu câu hiện tại có NVxxx mới,
                # ưu tiên target mới.
                # -----------------------------------------

                current_employee_code = None

                if (
                    current_entity
                    and
                    current_entity.get(
                        "type"
                    )
                    == "employee_code"
                ):

                    current_employee_code = (
                        str(
                            current_entity.get(
                                "value"
                            )
                        ).upper()
                    )

                employee_code = (
                    current_employee_code
                    or
                    active_employee_code
                )

                standalone = (
                    self._build_temporal_question(
                        topic=(
                            topic_for_time
                        ),
                        time_scope=(
                            current_time_scope
                        ),
                        employee_code=(
                            employee_code
                        ),
                    )
                )

                if standalone:

                    return (
                        standalone,
                        0.92,
                        False,
                        None,
                    )

            return (
                current,
                0.30,
                True,
                (
                    "Bạn đang muốn hỏi "
                    "khoảng thời gian này "
                    "cho nội dung nào?"
                ),
            )

        # =================================================
        # 5.1 SALARY + EXPLICIT TIME SCOPE
        #
        # Fix các case:
        #   Thế lương tháng trước?
        #   Còn lương tháng 8?
        #   Lương tháng trước NV001
        #   Lương tháng này NV002
        #
        # Time scope phải được ưu tiên trước khi
        # canonicalize thành câu salary chung.
        # =================================================

        if (
            current_topic
            == "my_salary"
            and
            current_time_scope
        ):

            current_employee_code = None

            if (
                current_entity
                and
                current_entity.get(
                    "type"
                )
                == "employee_code"
            ):

                current_employee_code = (
                    str(
                        current_entity.get(
                            "value"
                        )
                    )
                    .strip()
                    .upper()
                )

            # Nếu câu hiện tại nói rõ "của tôi"
            # thì không được kế thừa employee target cũ.
            asks_own_data = (
                "của tôi"
                in current.lower()
            )

            if asks_own_data:

                employee_code = None

            else:

                employee_code = (
                    current_employee_code
                    or
                    (
                        active_employee_code
                        if self.is_follow_up(
                            current
                        )
                        else None
                    )
                )

            standalone = (
                self._build_temporal_question(
                    topic=(
                        "my_salary"
                    ),
                    time_scope=(
                        current_time_scope
                    ),
                    employee_code=(
                        employee_code
                    ),
                )
            )

            if standalone:

                return (
                    standalone,
                    0.96,
                    False,
                    None,
                )

        # =================================================
        # 6. EXPLICIT EMPLOYEE + CLEAR TOPIC
        #
        # Lương NV001
        # Hồ sơ NV001
        # Hợp đồng NV001
        # =================================================

        if (
            current_entity
            and
            current_entity.get(
                "type"
            )
            == "employee_code"
            and
            current_topic
        ):

            employee_question = (
                self._build_entity_question(
                    topic=(
                        current_topic
                    ),
                    entity=(
                        current_entity
                    ),
                )
            )

            if employee_question:

                # Nếu user đã viết câu rõ ràng,
                # canonicalize để downstream nhất quán.
                return (
                    employee_question,
                    0.98,
                    False,
                    None,
                )

        # =================================================
        # 7. ENTITY FOLLOW-UP
        #
        # Còn NV002?
        # =================================================

        if (
            current_entity
            and
            (
                self.entity_resolver
                .is_entity_follow_up(
                    current
                )
                or
                (
                    current_entity.get(
                        "type"
                    )
                    == "employee_code"
                    and
                    self.is_follow_up(
                        current
                    )
                )
            )
        ):

            topic_for_entity = (
                current_topic
                or
                state.active_topic
            )

            if topic_for_entity:

                standalone = (
                    self._build_entity_question(
                        topic=(
                            topic_for_entity
                        ),
                        entity=(
                            current_entity
                        ),
                    )
                )

                if standalone:

                    return (
                        standalone,
                        0.92,
                        False,
                        None,
                    )

            # FINAL ATTACHMENT ENTITY FOLLOW-UP
            #
            # Nếu user nêu rõ employee code trong một lượt follow-up
            # và conversation đang có attachment_context đáng tin cậy,
            # không được hỏi lại topic một cách giả tạo.
            #
            # Ví dụ:
            #   Turn 1: [ảnh có Employee NV002]
            #   Turn 2: "Thế NV002 thì sao?"
            #
            # ai_input.text đã chứa PREVIOUS_ATTACHMENT_CONTEXT,
            # nên giữ nguyên current để downstream xử lý trên
            # attachment context đó.
            if (
                current_entity.get("type")
                == "employee_code"
                and bool(
                    state.attachment_context
                )
            ):
                return (
                    current,
                    0.95,
                    False,
                    None,
                )

            return (
                current,
                0.40,
                True,
                (
                    "Bạn đang muốn hỏi "
                    "thông tin này cho "
                    "nội dung nào?"
                ),
            )

        # =================================================
        # 8. CONTRACT FOLLOW-UP
        # =================================================

        contract_follow_up = (
            self._resolve_contract_follow_up(
                current=(
                    current
                ),
                state=(
                    state
                ),
            )
        )

        if contract_follow_up:
            return contract_follow_up

        # =================================================
        # 9. ENTITY COREFERENCE
        # =================================================

        entity_coreference = (
            self._resolve_entity_coreference(
                current=(
                    current
                ),
                state=(
                    state
                ),
            )
        )

        if entity_coreference:
            return entity_coreference

        # =================================================
        # 10. CURRENT TOPIC
        # =================================================

        if current_topic:

            if self.is_follow_up(
                current
            ):

                # -----------------------------------------
                # Personal follow-up phải giữ active NV.
                #
                # Hồ sơ NV001
                # → Còn hợp đồng?
                # -----------------------------------------

                if (
                    current_topic
                    in {
                        "my_salary",
                        "my_contract",
                        "my_leave",
                        "my_profile",
                    }
                    and
                    active_employee_code
                ):

                    employee_question = (
                        self._topic_to_employee_question(
                            topic=(
                                current_topic
                            ),
                            employee_code=(
                                active_employee_code
                            ),
                        )
                    )

                    if employee_question:

                        return (
                            employee_question,
                            0.95,
                            False,
                            None,
                        )

                standalone = (
                    self.topic_to_question(
                        current_topic
                    )
                    or
                    current
                )

                return (
                    standalone,
                    0.95,
                    False,
                    None,
                )

            # ---------------------------------------------
            # Câu topic rõ nhưng không follow-up.
            #
            # Nếu nó không có entity mới nhưng conversation
            # đang giữ employee target và là personal topic,
            # giữ target đó.
            #
            # Ví dụ:
            #
            # Hồ sơ NV001
            # Hợp đồng thế nào?
            # ---------------------------------------------

            if (
                current_topic
                in {
                    "my_salary",
                    "my_contract",
                    "my_leave",
                    "my_profile",
                }
                and
                active_employee_code
                and
                not self._extract_employee_code(
                    current
                )
                and
                "của tôi"
                not in current.lower()
            ):

                employee_question = (
                    self._topic_to_employee_question(
                        topic=(
                            current_topic
                        ),
                        employee_code=(
                            active_employee_code
                        ),
                    )
                )

                if employee_question:

                    return (
                        employee_question,
                        0.92,
                        False,
                        None,
                    )

            return (
                current,
                1.0,
                False,
                None,
            )

        # A connective follow-up can still contain a complete new subject.
        # Example: "Thế còn vector database?".  It must remain GENERAL_LLM,
        # not be forced to clarify merely because it starts with "thế còn".
        explicit_subject_match = re.match(
            r"^(?:thế còn|vậy còn|còn)\s+(.+?)(?:\s+thì sao)?\??$",
            current,
            re.IGNORECASE,
        )

        if explicit_subject_match:
            explicit_subject = explicit_subject_match.group(1).strip()
            vague_subjects = {
                "cái đó",
                "cái này",
                "trường hợp đó",
                "nó",
            }
            temporal_subjects = {
                "tháng trước",
                "tháng này",
                "hôm qua",
                "hôm nay",
                "năm ngoái",
            }

            if (
                explicit_subject.lower() not in vague_subjects
                and explicit_subject.lower() not in temporal_subjects
            ):
                return (
                    current,
                    0.90,
                    False,
                    None,
                )

        # A short employee-code follow-up is sufficiently grounded when the
        # same isolated conversation carries bounded attachment context. The
        # context itself was appended by the trusted API layer; no raw bytes
        # or cross-conversation state are used here.
        if (
            state.attachment_context
            and current_entity
            and current_entity.get("type") == "employee_code"
        ):
            return (
                current,
                0.90,
                False,
                None,
            )

        # =================================================
        # PHASE 07 SEMANTIC FOLLOW-UP
        # =================================================

        semantic_follow_up = (
            self.semantic_follow_up_resolver
            .resolve(
                current=current,
                state=state,
            )
        )

        if semantic_follow_up:

            if (
                semantic_follow_up
                .need_clarification
            ):

                return (
                    current,
                    semantic_follow_up.confidence,
                    True,
                    (
                        semantic_follow_up
                        .clarification_question
                        or
                        "Bạn có thể nói rõ hơn "
                        "nội dung muốn hỏi tiếp không?"
                    ),
                )

            if (
                semantic_follow_up.confidence
                >=
                self.semantic_follow_up_resolver
                .MIN_ACCEPT_CONFIDENCE
            ):

                return (
                    semantic_follow_up
                    .standalone_question,
                    semantic_follow_up
                    .confidence,
                    False,
                    None,
                )

        # =================================================
        # 11. FOLLOW-UP WITHOUT NEW TOPIC
        # =================================================

        if self.is_follow_up(
            current
        ):

            if state.active_topic:

                # -----------------------------------------
                # Preserve employee target.
                # -----------------------------------------

                if (
                    state.active_topic
                    in {
                        "my_salary",
                        "my_contract",
                        "my_leave",
                        "my_profile",
                    }
                    and
                    active_employee_code
                ):

                    employee_question = (
                        self._topic_to_employee_question(
                            topic=(
                                state.active_topic
                            ),
                            employee_code=(
                                active_employee_code
                            ),
                        )
                    )

                    if employee_question:

                        return (
                            employee_question,
                            0.82,
                            False,
                            None,
                        )

                standalone = (
                    self.topic_to_question(
                        state.active_topic
                    )
                )

                if standalone:

                    return (
                        standalone,
                        0.80,
                        False,
                        None,
                    )

            return (
                current,
                0.30,
                True,
                (
                    "Bạn đang muốn hỏi tiếp "
                    "về nội dung nào?"
                ),
            )

        # =================================================
        # 12. SHORT / AMBIGUOUS
        # =================================================

        vague_patterns = [
            r"^cái đó\??$",
            r"^cái kia\??$",
            r"^nó\??$",
            r"^sao\??$",
            r"^như nào\??$",
            r"^thế nào\??$",
            r"^thì sao\??$",
        ]

        for pattern in (
            vague_patterns
        ):

            if re.match(
                pattern,
                current.lower(),
            ):

                if state.active_topic:

                    if (
                        state.active_topic
                        in {
                            "my_salary",
                            "my_contract",
                            "my_leave",
                            "my_profile",
                        }
                        and
                        active_employee_code
                    ):

                        employee_question = (
                            self._topic_to_employee_question(
                                topic=(
                                    state.active_topic
                                ),
                                employee_code=(
                                    active_employee_code
                                ),
                            )
                        )

                        if employee_question:

                            return (
                                employee_question,
                                0.72,
                                False,
                                None,
                            )

                    standalone = (
                        self.topic_to_question(
                            state.active_topic
                        )
                    )

                    if standalone:

                        return (
                            standalone,
                            0.70,
                            False,
                            None,
                        )

                return (
                    current,
                    0.20,
                    True,
                    (
                        "Bạn đang nhắc tới "
                        "nội dung nào?"
                    ),
                )

        # =================================================
        # 13. NORMAL
        # =================================================

        return (
            current,
            1.0,
            False,
            None,
        )
