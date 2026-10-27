from __future__ import annotations

import re
import unicodedata

from typing import Any, Iterable

from core.enums import AiRoute
from core.models import ConversationEntity, ConversationState


DEFAULT_RECENT_TURN_LIMIT = 8
DEFAULT_MEMORY_LIMIT = 20
MAX_SUMMARY_LENGTH = 20_000


def _normalize_history(
    history: Iterable[dict[str, Any]] | None,
) -> list[dict[str, str]]:
    """
    Normalize short-term conversation history received
    from the trusted ASP.NET API layer.
    """

    if history is None:
        return []

    normalized: list[dict[str, str]] = []

    allowed_roles = {
        "user",
        "assistant",
        "system",
    }

    for turn in history:

        if not isinstance(turn, dict):
            continue

        role = str(
            turn.get(
                "role",
                "",
            )
        ).strip().lower()

        content = str(
            turn.get(
                "content",
                "",
            )
        ).strip()

        if role not in allowed_roles:
            continue

        if not content:
            continue

        normalized.append(
            {
                "role": role,
                "content": content[:8_000],
            }
        )

    return normalized


def _normalize_memories(
    memories: Iterable[dict[str, Any]] | None,
) -> list[dict[str, Any]]:
    """
    Normalize trusted memory context sent by ASP.NET.

    Memory is personalization context only.
    It MUST NOT be treated as:
    - authentication
    - authorization
    - SQL truth
    - workflow approval
    """

    if memories is None:
        return []

    normalized: list[dict[str, Any]] = []

    for memory in memories:

        if not isinstance(memory, dict):
            continue

        memory_type = str(
            memory.get(
                "memory_type",
                memory.get(
                    "memoryType",
                    "",
                ),
            )
        ).strip()

        memory_key = str(
            memory.get(
                "memory_key",
                memory.get(
                    "memoryKey",
                    "",
                ),
            )
        ).strip()

        content = str(
            memory.get(
                "content",
                "",
            )
        ).strip()

        source = str(
            memory.get(
                "source",
                "conversation",
            )
        ).strip()

        try:
            confidence = float(
                memory.get(
                    "confidence",
                    1.0,
                )
            )
        except (
            TypeError,
            ValueError,
        ):
            confidence = 0.0

        confidence = max(
            0.0,
            min(
                confidence,
                1.0,
            ),
        )

        if not memory_type:
            continue

        if not memory_key:
            continue

        if not content:
            continue

        normalized.append(
            {
                "memory_type":
                    memory_type[:64],

                "memory_key":
                    memory_key[:128],

                "content":
                    content[:4_000],

                "confidence":
                    confidence,

                "source":
                    source[:64],
            }
        )

    return normalized


def _normalize_summary(
    summary: str | None,
) -> str | None:

    if summary is None:
        return None

    value = str(
        summary
    ).strip()

    if not value:
        return None

    return value[
        :MAX_SUMMARY_LENGTH
    ]




def _hydrate_business_context_from_history(
    state: ConversationState,
) -> ConversationState:
    """
    Hydrate minimal deterministic business context from recent history.

    Không quyết định authorization.
    Không query SQL.
    Không approve workflow.
    Chỉ khôi phục topic/time/route hints cho follow-up.
    """

    turns = list(
        getattr(
            state,
            "recent_turns",
            [],
        )
        or []
    )

    previous_user = None

    for turn in reversed(turns):

        if (
            isinstance(turn, dict)
            and str(
                turn.get(
                    "role",
                    "",
                )
            ).lower()
            == "user"
        ):
            previous_user = str(
                turn.get(
                    "content",
                    "",
                )
            ).strip()

            if previous_user:
                break

    if not previous_user:
        return state

    q = previous_user.lower()

    normalized_q = unicodedata.normalize(
        "NFD",
        q,
    )
    normalized_q = "".join(
        ch
        for ch in normalized_q
        if unicodedata.category(ch) != "Mn"
    ).replace("đ", "d")

    # Preserve the last trusted user utterance as semantic source material.
    # The resolver may replace only its time expression for a temporal
    # follow-up, retaining the original operation, target and route meaning.
    state.task_state = {
        **(
            state.task_state
            if isinstance(state.task_state, dict)
            else {}
        ),
        "previous_user_question": previous_user,
    }

    # -------------------------------------------------
    # Attendance / late / early / check-in / check-out
    # -------------------------------------------------

    if any(
        marker in q
        for marker in (
            "đi muộn",
            "đi trễ",
            "đến muộn",
            "đến trễ",
        )
    ):
        state.active_topic = state.active_topic or "attendance_late"

        if any(
            marker in q
            for marker in (
                "bao nhiêu nhân viên",
                "bao nhiêu người",
                "số nhân viên",
                "đếm nhân viên",
            )
        ):
            state.expected_answer_type = state.expected_answer_type or "count"
            state.last_route = state.last_route or AiRoute.LIVE_SQL
            state.task_state.update(
                {
                    "business_domain": "attendance",
                    "metric": "count_late_employees",
                    "route_hint": "LIVE_SQL",
                }
            )

    elif any(
        marker in q
        for marker in (
            "về sớm",
            "tan làm sớm",
            "về trước giờ",
        )
    ):
        state.active_topic = state.active_topic or "attendance_early_leave"

    elif any(
        marker in q
        for marker in (
            "quên check-in",
            "thiếu check-in",
            "không check-in",
            "quên chấm công vào",
        )
    ):
        state.active_topic = state.active_topic or "attendance_missing_checkin"

    elif any(
        marker in q
        for marker in (
            "quên check-out",
            "thiếu check-out",
            "không check-out",
            "quên chấm công ra",
        )
    ):
        state.active_topic = state.active_topic or "attendance_missing_checkout"

    elif any(
        marker in q
        for marker in (
            "tăng ca",
            "làm thêm giờ",
            "overtime",
        )
    ):
        state.active_topic = state.active_topic or "attendance_overtime"

    # -------------------------------------------------
    # Personal salary
    # -------------------------------------------------

    elif (
        "lương" in q
        and any(
            marker in q
            for marker in (
                "của tôi",
                "lương tôi",
                "lương tháng",
            )
        )
    ):
        state.active_topic = (
            state.active_topic
            or "my_salary"
        )
        state.last_route = state.last_route or AiRoute.PERSONAL_DATA
        state.task_state.update(
            {
                "business_domain": "payroll",
                "metric": "my_salary",
                "route_hint": "PERSONAL_DATA",
            }
        )

    # -------------------------------------------------
    # Personal leave
    # -------------------------------------------------

    elif (
        "nghỉ phép" in q
        and any(
            marker in q
            for marker in (
                "của tôi",
                "đơn của tôi",
                "đơn nghỉ",
            )
        )
    ):
        state.active_topic = (
            state.active_topic
            or "my_leave"
        )

    # -------------------------------------------------
    # Time scope
    # -------------------------------------------------

    department_match = re.search(
        r"\b(?:phòng|phong|ban)\s+([A-Za-zÀ-ỹ0-9_-]+)",
        previous_user,
        re.IGNORECASE,
    )

    if department_match:
        department = department_match.group(1).strip(" .,!?:;")
        if department:
            state.active_entities.append(
                ConversationEntity(
                    entity_type="department",
                    value=department,
                    confidence=0.90,
                )
            )
            state.task_state["target_department"] = department

    if "hôm nay" in q:
        state.time_scope = (
            state.time_scope
            or "today"
        )

    elif "tháng này" in q:
        state.time_scope = (
            state.time_scope
            or "current_month"
        )

    elif "tháng trước" in q:
        state.time_scope = (
            state.time_scope
            or "previous_month"
        )

    # Normalized text is for deterministic rules/diagnostics only.  It is
    # never authentication, authorization or HRMS data.
    state.task_state["normalized_previous_question"] = normalized_q

    return state

def build_conversation_state(
    *,
    history: Iterable[dict[str, Any]] | None = None,
    conversation_summary: str | None = None,
    user_memories: Iterable[dict[str, Any]] | None = None,
    recent_turn_limit: int = DEFAULT_RECENT_TURN_LIMIT,
    memory_limit: int = DEFAULT_MEMORY_LIMIT,
) -> ConversationState:
    """
    Build runtime ConversationState for one AVA request.

    Sources:
    - recent history
    - optional conversation summary
    - trusted Personal Memory supplied by ASP.NET

    This function does NOT:
    - authenticate users
    - decide permissions
    - query SQL
    - persist memories
    - approve workflows
    """

    safe_history = (
        _normalize_history(
            history
        )
    )

    safe_memories = (
        _normalize_memories(
            user_memories
        )
    )

    safe_summary = (
        _normalize_summary(
            conversation_summary
        )
    )

    if recent_turn_limit <= 0:

        recent_turns: list[
            dict[str, str]
        ] = []

    else:

        recent_turns = (
            safe_history[
                -recent_turn_limit:
            ]
        )

    if memory_limit <= 0:

        selected_memories: list[
            dict[str, Any]
        ] = []

    else:

        selected_memories = (
            safe_memories[
                :memory_limit
            ]
        )

    state = ConversationState(
        recent_turns=recent_turns,
        conversation_summary=safe_summary,
        user_memories=selected_memories,
    )

    return _hydrate_business_context_from_history(
        state
    )



