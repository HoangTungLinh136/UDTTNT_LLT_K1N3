from core.enums import AiRoute, GuardrailDecision, VerificationStatus
from core.models import RouteResult, UserContext
from rag.reranker import RagReranker
from response.verifier import AnswerVerifier
from safety.authorization import AuthorizationService
from safety.guardrails import GuardrailService
from services.general_agent_service import GeneralAgentService
from services.live_sql_service import LiveSqlService
from services.rag_service import RagService


def _user(role: str, permissions: list[str] | None = None) -> UserContext:
    return UserContext(
        user_id="u1",
        employee_id="1",
        employee_code="NV001",
        is_authenticated=True,
        roles=[role],
        permissions=permissions or [],
        department_names=["Phòng IT"],
    )


def test_live_sql_employee_denied_and_hr_allowed():
    auth = AuthorizationService()
    question = "Hôm nay phòng IT có bao nhiêu nhân viên đi muộn?"
    denied = auth.authorize(AiRoute.LIVE_SQL, _user("Employee"), question)
    allowed = auth.authorize(AiRoute.LIVE_SQL, _user("HR"), question)
    assert denied.decision == GuardrailDecision.BLOCK
    assert denied.reason_code == "LIVE_DATA_ACCESS_DENIED"
    assert allowed.decision == GuardrailDecision.ALLOW


def test_personal_route_requires_trusted_employee_context():
    user = _user("Employee")
    user.employee_id = None
    result = AuthorizationService().authorize(
        AiRoute.PERSONAL_DATA,
        user,
        "Lương tháng này của tôi?",
    )
    assert result.decision == GuardrailDecision.REQUIRE_AUTH
    assert result.reason_code == "EMPLOYEE_CONTEXT_MISSING"


def test_live_sql_failure_is_safe_and_does_not_hallucinate(monkeypatch):
    service = LiveSqlService()

    def fail():
        raise RuntimeError("database unavailable")

    monkeypatch.setattr(service.queries, "get_employee_count", fail)
    result = service.answer("Hiện có bao nhiêu nhân viên?")
    assert result.route == AiRoute.LIVE_SQL
    assert result.warning == "LIVE_SQL_FAILED"
    assert "không thể" in result.answer_draft.lower()


def test_rag_no_evidence_never_calls_generation_model():
    service = RagService.__new__(RagService)
    service.retrieve = lambda *_args, **_kwargs: RouteResult(
        route=AiRoute.RAG,
        answer_draft="Không có bằng chứng nội bộ đủ tin cậy.",
        warning="NO_INTERNAL_EVIDENCE",
        confidence=0.0,
    )

    class MustNotRun:
        def chat(self, **_kwargs):
            raise AssertionError("LLM must not run without evidence")

    service.llm = MustNotRun()
    result = service.answer("Chính sách không tồn tại?", _user("Employee"))
    assert result.warning == "NO_INTERNAL_EVIDENCE"
    assert result.route == AiRoute.RAG


def test_personal_period_not_found_is_valid_verifier_warning():
    result = RouteResult(
        route=AiRoute.PERSONAL_DATA,
        answer_draft="Hiện chưa có dữ liệu lương trong kỳ này.",
        data={"year": 2026, "month": 9},
        warning="PERSONAL_SALARY_PERIOD_NOT_FOUND",
    )
    verified = AnswerVerifier().verify(result)
    assert verified.status == VerificationStatus.WARNING
    assert "PERSONAL_RESULT_EXISTS" in verified.checks


def test_output_secret_redaction():
    masked, safety = GuardrailService().mask_output(
        "token=abc123 password=hunter2"
    )
    assert "abc123" not in masked
    assert "hunter2" not in masked
    assert safety.decision == GuardrailDecision.MASK


def test_context_labels_do_not_trigger_fake_web():
    prompt = (
        "RECENT HISTORY:\nUSER: Công ty ABC hiện nay?\n\n"
        "PERSONAL MEMORY:\nENTITY XYZ\n\n"
        "CURRENT QUESTION:\nRAG là gì?"
    )
    assert GeneralAgentService._requires_web(prompt) is False


def test_reranker_rejects_unrelated_internal_policy():
    candidates = [
        {
            "distance": 0.70,
            "document": "Quy định đi muộn và chấm công.",
            "metadata": {
                "title": "Quy định đi muộn",
                "section": "late",
                "category": "AttendanceLate",
            },
        }
    ]
    result = RagReranker().rerank(
        candidates,
        "Quy định nội bộ về dự báo thời tiết ngày mai là gì?",
    )
    assert result == []
