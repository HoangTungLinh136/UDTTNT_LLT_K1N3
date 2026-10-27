from __future__ import annotations

from core.models import UserContext

from services.workflow_advisory_service import (
    WorkflowAdvisoryService,
)


service = WorkflowAdvisoryService()


def user(role: str) -> UserContext:
    return UserContext(
        user_id="u1",
        employee_id="1",
        employee_code="NV001",
        is_authenticated=True,
        roles=[role],
        permissions=[],
    )


def test_employee_cannot_approve_leave_via_ava():
    result = service.analyze(
        "Duyệt đơn nghỉ phép này giúp tôi",
        user("Employee"),
    )

    assert result is not None
    assert result.intent == "WorkflowActionBlocked"
    assert (
        result.warning
        ==
        "WORKFLOW_ACTION_REQUIRES_HRMS_BACKEND"
    )


def test_manager_leave_guidance():
    result = service.analyze(
        "Ai được duyệt đơn nghỉ phép?",
        user("Manager"),
    )

    assert result is not None
    assert result.intent == "WorkflowGuidance"
    assert "Manager" in result.answer


def test_hr_cannot_override_normal_leave():
    result = service.analyze(
        "HR có quyền duyệt đơn nghỉ phép không?",
        user("HR"),
    )

    assert result is not None
    assert "không cho Leader, HR hoặc Admin" in (
        result.answer
    )


def test_leader_appointment_guidance():
    result = service.analyze(
        "Tôi có quyền phê duyệt bổ nhiệm không?",
        user("Leader"),
    )

    assert result is not None
    assert "Leader" in result.answer
    assert "phạm vi" in result.answer


def test_manager_appointment_action_is_not_executed():
    result = service.analyze(
        "Phê duyệt bổ nhiệm này",
        user("Manager"),
    )

    assert result is not None
    assert result.intent == "WorkflowActionBlocked"


def test_attendance_adjustment_guard():
    result = service.analyze(
        "Reject yêu cầu điều chỉnh công này",
        user("Manager"),
    )

    assert result is not None
    assert result.intent == "WorkflowActionBlocked"
    assert "backend" in result.answer


def test_non_workflow_question_passes_through():
    result = service.analyze(
        "Python là gì?",
        user("Employee"),
    )

    assert result is None


def test_workflow_information_is_read_only():
    result = service.analyze(
        "Quy trình duyệt đơn như thế nào?",
        user("Employee"),
    )

    assert result is not None
    assert result.intent == "WorkflowGuidance"
    assert result.warning is None
