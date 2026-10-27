from context.context_resolver import ContextResolver
from core.enums import InputModality
from core.models import ConversationState, NormalizedAiInput


def test_employee_code_follow_up_uses_isolated_attachment_context():
    state = ConversationState(
        attachment_context={
            "attachment_count": 1,
            "items": [{"file_name": "screen.png", "extracted_text": "Employee NV002\nStatus OK"}],
        }
    )
    value = NormalizedAiInput(
        text=(
            "Thế NV002 thì sao?\n\n[PREVIOUS_ATTACHMENT_CONTEXT]\n"
            "[SOURCE screen.png]\nEmployee NV002\nStatus OK"
        ),
        modality=InputModality.TEXT,
        metadata={"user_text": "Thế NV002 thì sao?"},
    )
    question, confidence, clarify, clarification = ContextResolver().resolve_question(value, state)
    assert "NV002" in question
    assert confidence >= 0.90
    assert clarify is False
    assert clarification is None
