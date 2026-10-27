import pytest
from pydantic import ValidationError

from api.schemas import ChatRequest


def valid_user_context() -> dict:
    return {
        "userId": "user-001",
        "employeeId": "emp-001",
        "roles": [
            "Employee"
        ],
        "permissions": [],
        "departmentIds": [],
        "departmentNames": [],
    }


def test_valid_history_is_accepted():
    request = ChatRequest(
        text="Xin chào AVA",
        userContext=valid_user_context(),
        history=[
            {
                "role": "user",
                "content": "Xin chào",
            },
            {
                "role": "assistant",
                "content": "Xin chào bạn",
            },
        ],
    )

    assert len(
        request.history
    ) == 2

    assert (
        request.history[0].role
        == "user"
    )

    assert (
        request.history[1].role
        == "assistant"
    )


def test_system_role_is_accepted():
    request = ChatRequest(
        text="Test",
        userContext=valid_user_context(),
        history=[
            {
                "role": "system",
                "content": "System context",
            }
        ],
    )

    assert (
        request.history[0].role
        == "system"
    )


def test_invalid_role_is_rejected():
    with pytest.raises(
        ValidationError
    ):
        ChatRequest(
            text="Test",
            userContext=valid_user_context(),
            history=[
                {
                    "role": "hacker",
                    "content": "bad role",
                }
            ],
        )


def test_missing_content_is_rejected():
    with pytest.raises(
        ValidationError
    ):
        ChatRequest(
            text="Test",
            userContext=valid_user_context(),
            history=[
                {
                    "role": "user",
                }
            ],
        )


def test_empty_content_is_rejected():
    with pytest.raises(
        ValidationError
    ):
        ChatRequest(
            text="Test",
            userContext=valid_user_context(),
            history=[
                {
                    "role": "user",
                    "content": "",
                }
            ],
        )


def test_history_over_8_items_is_rejected():
    history = [
        {
            "role": "user",
            "content": f"message-{index}",
        }
        for index in range(9)
    ]

    with pytest.raises(
        ValidationError
    ):
        ChatRequest(
            text="Test",
            userContext=valid_user_context(),
            history=history,
        )


def test_history_exactly_8_items_is_accepted():
    history = [
        {
            "role": "user",
            "content": f"message-{index}",
        }
        for index in range(8)
    ]

    request = ChatRequest(
        text="Test",
        userContext=valid_user_context(),
        history=history,
    )

    assert len(
        request.history
    ) == 8


def test_history_content_too_long_is_rejected():
    with pytest.raises(
        ValidationError
    ):
        ChatRequest(
            text="Test",
            userContext=valid_user_context(),
            history=[
                {
                    "role": "user",
                    "content": "A" * 8_001,
                }
            ],
        )