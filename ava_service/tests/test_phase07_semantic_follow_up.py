from __future__ import annotations

from context.semantic_follow_up_resolver import (
    SemanticFollowUpResolver,
)
from context.context_resolver import ContextResolver
from core.conversation_state import build_conversation_state
from core.models import NormalizedAiInput

from core.models import ConversationState


resolver = SemanticFollowUpResolver()


def test_employee_count_bao_nhieu():
    state = ConversationState(
        active_topic="employee_count",
    )

    result = resolver.resolve(
        "bao nhiêu?",
        state,
    )

    assert result is not None
    assert result.need_clarification is False
    assert (
        result.standalone_question
        ==
        "Hiện có bao nhiêu nhân viên?"
    )
    assert result.confidence >= 0.72


def test_salary_my_followup():
    state = ConversationState(
        active_topic="my_salary",
    )

    result = resolver.resolve(
        "của tôi thì sao?",
        state,
    )

    assert result is not None
    assert result.need_clarification is False
    assert "Lương của tôi" in result.standalone_question


def test_history_explain_more():
    state = ConversationState(
        recent_turns=[
            {
                "role": "user",
                "content": "RAG hoạt động như thế nào?",
            },
            {
                "role": "assistant",
                "content": "RAG kết hợp retrieval và generation.",
            },
        ],
    )

    result = resolver.resolve(
        "giải thích thêm",
        state,
    )

    assert result is not None
    assert result.need_clarification is False
    assert (
        "RAG hoạt động như thế nào?"
        in result.standalone_question
    )


def test_new_topic_must_not_use_old_salary():
    state = ConversationState(
        active_topic="my_salary",
        recent_turns=[
            {
                "role": "user",
                "content": "Lương tháng này của tôi?",
            }
        ],
    )

    result = resolver.resolve(
        "Python là gì?",
        state,
    )

    assert result is None


def test_low_context_should_clarify():
    state = ConversationState()

    result = resolver.resolve(
        "còn cái đó?",
        state,
    )

    assert result is not None
    assert result.need_clarification is True
    assert result.confidence < 0.72


def test_pending_slot():
    state = ConversationState(
        pending_slot="purpose",
        task_state={
            "task": "laptop_recommendation",
        },
    )

    result = resolver.resolve(
        "để học tập",
        state,
    )

    assert result is not None
    assert result.need_clarification is False
    assert "purpose" in result.standalone_question
    assert "học tập" in result.standalone_question


def test_clear_new_question_overrides_pending_slot():
    state = ConversationState(
        pending_slot="purpose",
        active_topic="my_salary",
    )

    result = resolver.resolve(
        "Python là gì?",
        state,
    )

    assert result is None


def test_live_late_attendance_temporal_followup_preserves_goal():
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

    standalone, confidence, clarify, question = (
        ContextResolver().resolve_question(
            ai_input=NormalizedAiInput(
                text="Thế còn tháng trước thì sao?"
            ),
            state=state,
        )
    )

    assert standalone == (
        "Tháng trước phòng IT có bao nhiêu "
        "nhân viên đi muộn?"
    )
    assert confidence >= 0.90
    assert clarify is False
    assert question is None


def test_general_explicit_subject_followup_does_not_clarify():
    state = build_conversation_state(
        history=[
            {"role": "user", "content": "RAG là gì?"},
            {"role": "assistant", "content": "RAG là..."},
        ]
    )

    standalone, confidence, clarify, question = (
        ContextResolver().resolve_question(
            ai_input=NormalizedAiInput(
                text="Thế còn vector database?"
            ),
            state=state,
        )
    )

    assert standalone == "Thế còn vector database?"
    assert confidence >= 0.90
    assert clarify is False
    assert question is None
