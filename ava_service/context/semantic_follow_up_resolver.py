from __future__ import annotations

import re
import unicodedata

from dataclasses import dataclass

from core.models import (
    ConversationState,
)


# =========================================================
# RESULT
# =========================================================

@dataclass
class SemanticFollowUpResult:
    standalone_question: str
    confidence: float
    need_clarification: bool = False
    clarification_question: str | None = None
    reason_code: str = "SEMANTIC_FOLLOW_UP"


# =========================================================
# RESOLVER
# =========================================================

class SemanticFollowUpResolver:
    """
    Generic semantic follow-up resolver.

    Mục tiêu:
    - xử lý câu nối tiếp ngắn mà deterministic rules
      chưa resolve được;
    - không thay thế authorization;
    - không thêm fact mới;
    - không ép mọi câu thành follow-up;
    - ưu tiên structured ConversationState.

    Phase 07 deliberately không phụ thuộc LLM/provider.
    Provider abstraction vẫn thuộc phase sau.
    """

    MIN_ACCEPT_CONFIDENCE = 0.72

    def normalize(
        self,
        text: str,
    ) -> str:

        if not text:
            return ""

        value = unicodedata.normalize(
            "NFC",
            str(text),
        )

        value = re.sub(
            r"\s+",
            " ",
            value,
        )

        return value.strip()

    # =====================================================
    # RECENT HISTORY
    # =====================================================

    def _last_user_message(
        self,
        state: ConversationState,
    ) -> str | None:

        for turn in reversed(
            state.recent_turns
        ):

            if not isinstance(
                turn,
                dict,
            ):
                continue

            role = str(
                turn.get(
                    "role",
                    "",
                )
            ).strip().lower()

            content = self.normalize(
                turn.get(
                    "content",
                    "",
                )
            )

            if (
                role == "user"
                and content
            ):
                return content

        return None

    def _last_assistant_message(
        self,
        state: ConversationState,
    ) -> str | None:

        for turn in reversed(
            state.recent_turns
        ):

            if not isinstance(
                turn,
                dict,
            ):
                continue

            role = str(
                turn.get(
                    "role",
                    "",
                )
            ).strip().lower()

            content = self.normalize(
                turn.get(
                    "content",
                    "",
                )
            )

            if (
                role == "assistant"
                and content
            ):
                return content

        return None

    # =====================================================
    # NEW TOPIC GUARD
    # =====================================================

    def _looks_like_clear_new_question(
        self,
        current: str,
    ) -> bool:
        """
        Ngăn giữ context cũ khi user chuyển chủ đề rõ ràng.

        Ví dụ:
        Salary context
            ↓
        "Python là gì?"

        => new question.
        """

        q = current.lower().strip()

        if len(q) < 8:
            return False

        # Explicit topic switch markers.
        switch_markers = [
            "chuyển chủ đề",
            "câu khác",
            "hỏi cái khác",
            "nhân tiện",
            "ngoài ra tôi muốn hỏi",
        ]

        if any(
            marker in q
            for marker in switch_markers
        ):
            return True

        # General explicit "X là gì?" questions usually
        # carry enough information without old context.
        if re.search(
            r"^.{2,80}\s+là\s+gì\??$",
            q,
            re.IGNORECASE,
        ):
            return True

        # Explicit "hãy giải thích X" where X is substantial.
        if (
            q.startswith(
                (
                    "giải thích ",
                    "hãy giải thích ",
                    "cho tôi biết về ",
                    "tôi muốn hỏi về ",
                )
            )
            and len(q.split()) >= 5
        ):
            return True

        return False

    # =====================================================
    # GENERIC FOLLOW-UP DETECTION
    # =====================================================

    def _is_generic_follow_up(
        self,
        current: str,
    ) -> bool:

        q = (
            current
            .lower()
            .strip()
            .rstrip("?.!")
        )

        patterns = [
            r"^bao nhiêu$",
            r"^cụ thể là bao nhiêu$",
            r"^gồm gì$",
            r"^gồm những gì$",
            r"^cụ thể gồm gì$",
            r"^cụ thể gồm những gì$",
            r"^chi tiết hơn$",
            r"^nói rõ hơn$",
            r"^giải thích thêm$",
            r"^cụ thể hơn$",
            r"^vì sao$",
            r"^tại sao$",
            r"^thế thì sao$",
            r"^còn cái này$",
            r"^còn cái đó$",
            r"^còn trường hợp này$",
            r"^còn trường hợp đó$",
            r"^của tôi thì sao$",
            r"^của mình thì sao$",
            r"^vậy của tôi$",
            r"^thế của tôi$",
            r"^nó hoạt động thế nào$",
            r"^hoạt động thế nào$",
            r"^làm thế nào$",
            r"^làm sao$",
        ]

        if any(
            re.search(
                pattern,
                q,
                re.IGNORECASE,
            )
            for pattern in patterns
        ):
            return True

        # Very short connective utterances.
        if (
            len(q.split()) <= 5
            and q.startswith(
                (
                    "thế ",
                    "vậy ",
                    "còn ",
                    "rồi ",
                    "và ",
                )
            )
        ):
            return True

        return False

    # =====================================================
    # TOPIC CANONICALIZATION
    # =====================================================

    def _topic_question(
        self,
        topic: str,
        current: str,
    ) -> str | None:

        q = (
            current
            .lower()
            .strip()
            .rstrip("?.!")
        )

        # -------------------------------------------------
        # EMPLOYEE COUNT
        # -------------------------------------------------

        if topic == "employee_count":

            if q in {
                "bao nhiêu",
                "cụ thể là bao nhiêu",
            }:
                return (
                    "Hiện có bao nhiêu nhân viên?"
                )

            if q in {
                "gồm gì",
                "gồm những gì",
                "gồm những ai",
                "có những ai",
            }:
                return (
                    "Danh sách nhân viên hiện tại "
                    "gồm những ai?"
                )

        # -------------------------------------------------
        # EMPLOYEE LIST
        # -------------------------------------------------

        if topic == "employee_list":

            if q in {
                "bao nhiêu",
                "cụ thể là bao nhiêu",
            }:
                return (
                    "Hiện có bao nhiêu nhân viên?"
                )

            return (
                "Hãy giải thích chi tiết hơn về "
                "danh sách nhân viên hiện tại."
            )

        # -------------------------------------------------
        # SALARY
        # -------------------------------------------------

        if topic == "my_salary":

            if q in {
                "bao nhiêu",
                "cụ thể là bao nhiêu",
                "của tôi thì sao",
                "của mình thì sao",
                "vậy của tôi",
                "thế của tôi",
            }:
                return (
                    "Lương của tôi hiện tại "
                    "là bao nhiêu?"
                )

            if q in {
                "chi tiết hơn",
                "nói rõ hơn",
                "giải thích thêm",
                "cụ thể hơn",
            }:
                return (
                    "Hãy giải thích chi tiết hơn "
                    "về lương của tôi."
                )

        # -------------------------------------------------
        # CONTRACT
        # -------------------------------------------------

        if topic == "my_contract":

            if q in {
                "của tôi thì sao",
                "của mình thì sao",
                "vậy của tôi",
                "thế của tôi",
            }:
                return (
                    "Hợp đồng của tôi hiện tại "
                    "như thế nào?"
                )

            if q in {
                "chi tiết hơn",
                "nói rõ hơn",
                "giải thích thêm",
                "cụ thể hơn",
            }:
                return (
                    "Hãy giải thích chi tiết hơn "
                    "về hợp đồng của tôi."
                )

        # -------------------------------------------------
        # LEAVE
        # -------------------------------------------------

        if topic == "my_leave":

            if q in {
                "của tôi thì sao",
                "của mình thì sao",
            }:
                return (
                    "Đơn nghỉ phép của tôi "
                    "hiện tại như thế nào?"
                )

        # -------------------------------------------------
        # PROFILE
        # -------------------------------------------------

        if topic == "my_profile":

            if q in {
                "của tôi thì sao",
                "của mình thì sao",
                "vậy của tôi",
                "thế của tôi",
            }:
                return (
                    "Thông tin hồ sơ của tôi "
                    "là gì?"
                )

        # -------------------------------------------------
        # ATTENDANCE
        # -------------------------------------------------

        if topic == "attendance_late":

            if q in {
                "vì sao",
                "tại sao",
            }:
                return (
                    "Vì sao việc đi muộn được "
                    "xử lý theo quy định đó?"
                )

            return (
                "Hãy giải thích chi tiết hơn "
                "về quy định đi muộn."
            )

        if topic == "attendance_early_leave":

            return (
                "Hãy giải thích chi tiết hơn "
                "về quy định về sớm."
            )

        if topic == "attendance_missing_checkin":

            return (
                "Hãy giải thích chi tiết hơn "
                "về xử lý trường hợp "
                "quên check-in."
            )

        if topic == "attendance_missing_checkout":

            return (
                "Hãy giải thích chi tiết hơn "
                "về xử lý trường hợp "
                "quên check-out."
            )

        if topic == "attendance_overtime":

            return (
                "Hãy giải thích chi tiết hơn "
                "về quy định tăng ca."
            )

        return None

    # =====================================================
    # PENDING SLOT
    # =====================================================

    def _resolve_pending_slot(
        self,
        current: str,
        state: ConversationState,
    ) -> SemanticFollowUpResult | None:

        pending_slot = (
            state.pending_slot
            or ""
        ).strip()

        if not pending_slot:
            return None

        # Một câu trả lời ngắn cho slot đang chờ.
        if (
            len(current.split()) > 14
            or self._looks_like_clear_new_question(
                current
            )
        ):
            return None

        task = (
            state.task_state
            if isinstance(
                state.task_state,
                dict,
            )
            else {}
        )

        task_name = str(
            task.get(
                "task",
                "",
            )
        ).strip()

        if task_name:

            standalone = (
                "Trong tác vụ "
                f"{task_name}, "
                f"giá trị người dùng cung cấp cho "
                f"{pending_slot} là: {current}"
            )

        else:

            standalone = (
                "Câu trả lời của người dùng cho "
                f"thông tin đang được hỏi "
                f"({pending_slot}) là: {current}"
            )

        return SemanticFollowUpResult(
            standalone_question=standalone,
            confidence=0.90,
            need_clarification=False,
            reason_code="PENDING_SLOT",
        )

    # =====================================================
    # HISTORY FALLBACK
    # =====================================================

    def _rewrite_from_history(
        self,
        current: str,
        state: ConversationState,
    ) -> SemanticFollowUpResult | None:

        previous_user = (
            self._last_user_message(
                state
            )
        )

        if not previous_user:
            return None

        q = (
            current
            .lower()
            .strip()
            .rstrip("?.!")
        )

        if q in {
            "chi tiết hơn",
            "nói rõ hơn",
            "giải thích thêm",
            "cụ thể hơn",
        }:

            return SemanticFollowUpResult(
                standalone_question=(
                    "Hãy giải thích chi tiết hơn "
                    "cho câu hỏi trước của người dùng: "
                    f"{previous_user}"
                ),
                confidence=0.82,
                reason_code="HISTORY_ELABORATION",
            )

        if q in {
            "vì sao",
            "tại sao",
        }:

            return SemanticFollowUpResult(
                standalone_question=(
                    "Hãy giải thích nguyên nhân hoặc lý do "
                    "liên quan đến câu hỏi trước: "
                    f"{previous_user}"
                ),
                confidence=0.78,
                reason_code="HISTORY_WHY",
            )

        return None

    # =====================================================
    # MAIN
    # =====================================================

    def resolve(
        self,
        current: str,
        state: ConversationState,
    ) -> SemanticFollowUpResult | None:

        current = self.normalize(
            current
        )

        if not current:
            return None

        # Explicit new topic wins.
        if self._looks_like_clear_new_question(
            current
        ):
            return None

        # Pending structured task has priority.
        pending_result = (
            self._resolve_pending_slot(
                current,
                state,
            )
        )

        if pending_result:
            return pending_result

        if not self._is_generic_follow_up(
            current
        ):
            return None

        # Structured topic is stronger than raw history.
        if state.active_topic:

            standalone = (
                self._topic_question(
                    state.active_topic,
                    current,
                )
            )

            if standalone:

                return SemanticFollowUpResult(
                    standalone_question=standalone,
                    confidence=0.88,
                    reason_code=(
                        "ACTIVE_TOPIC_SEMANTIC"
                    ),
                )

        # Generic history fallback.
        history_result = (
            self._rewrite_from_history(
                current,
                state,
            )
        )

        if history_result:
            return history_result

        # We think this is a follow-up but lack enough context.
        return SemanticFollowUpResult(
            standalone_question=current,
            confidence=0.35,
            need_clarification=True,
            clarification_question=(
                "Bạn muốn hỏi tiếp về nội dung "
                "nào trong cuộc trao đổi trước?"
            ),
            reason_code="SEMANTIC_CONTEXT_LOW",
        )
