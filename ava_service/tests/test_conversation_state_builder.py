from core.conversation_state import (
    DEFAULT_RECENT_TURN_LIMIT,
    build_conversation_state,
)
from core.enums import AiRoute


def make_history(count: int) -> list[dict[str, str]]:
    return [
        {
            "role": "user",
            "content": f"message-{index}",
        }
        for index in range(count)
    ]


def test_default_recent_turn_limit_is_8():
    assert DEFAULT_RECENT_TURN_LIMIT == 8


def test_builder_accepts_empty_history():
    state = build_conversation_state(
        history=[]
    )

    assert state.recent_turns == []


def test_builder_accepts_none_history():
    state = build_conversation_state(
        history=None
    )

    assert state.recent_turns == []


def test_builder_keeps_history_under_limit():
    state = build_conversation_state(
        history=make_history(3)
    )

    assert len(state.recent_turns) == 3


def test_builder_keeps_last_8_turns():
    state = build_conversation_state(
        history=make_history(12)
    )

    assert len(state.recent_turns) == 8

    assert (
        state.recent_turns[0]["content"]
        == "message-4"
    )

    assert (
        state.recent_turns[-1]["content"]
        == "message-11"
    )


def test_builder_does_not_reuse_history_objects():
    history = make_history(2)

    state = build_conversation_state(
        history=history
    )

    assert state.recent_turns is not history
    assert state.recent_turns[0] is not history[0]


def test_zero_limit_disables_recent_history():
    state = build_conversation_state(
        history=make_history(5),
        recent_turn_limit=0,
    )

    assert state.recent_turns == []


def test_hydrates_live_late_attendance_goal_from_history():
    state = build_conversation_state(
        history=[
            {
                "role": "user",
                "content": (
                    "Tháng này phòng IT có bao nhiêu "
                    "nhân viên đi muộn?"
                ),
            },
            {
                "role": "assistant",
                "content": "Kết quả trước đó.",
            },
        ]
    )

    assert state.active_topic == "attendance_late"
    assert state.time_scope == "current_month"
    assert state.last_route == AiRoute.LIVE_SQL
    assert state.expected_answer_type == "count"
    assert state.task_state["metric"] == "count_late_employees"
    assert state.task_state["target_department"] == "IT"
    assert state.active_entities[0].entity_type == "department"
    assert state.active_entities[0].value == "IT"
