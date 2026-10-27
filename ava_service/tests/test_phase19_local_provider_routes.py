from datetime import date, timedelta
from types import SimpleNamespace

from context.intent_classifier import IntentClassifier
from core.enums import AiRoute
from core.models import ContextIntentResult, RouteResult, UserContext
from core.orchestrator import AiOrchestrator
from routing.router import AiRouter
from services.general_agent_service import GeneralAgentService, GeneralTool
from services.live_sql_service import LiveSqlService
from services.personal_data_service import PersonalDataService
from services.rag_service import RagService
from config.settings import settings
from database.live_queries import LiveQueries
from database import personal_queries as personal_queries_module
from api.main import _response_payload
from core.models import AiResponse


def _classify(question: str, user: UserContext) -> AiRoute:
    context = IntentClassifier().classify(
        original_question=question,
        standalone_question=question,
        resolution_confidence=1.0,
        need_clarification=False,
        clarification_question=None,
    )
    return AiRouter().route(context, user).route


def _employee_user() -> UserContext:
    return UserContext(
        user_id="test-user",
        employee_id="41",
        employee_code="NV041",
        is_authenticated=True,
        roles=["Employee"],
    )


def test_acceptance_scrum_routes_to_local_ollama(monkeypatch):
    service = GeneralAgentService()
    monkeypatch.setattr(
        service.ollama,
        "chat",
        lambda **_kwargs: SimpleNamespace(content="Scrum là một framework Agile."),
    )
    assert _classify("Scrum là gì?", _employee_user()) == AiRoute.GENERAL_LLM
    answer = service.answer("Scrum là gì?")
    assert answer.tool == GeneralTool.OLLAMA
    assert answer.provider == "ollama"
    assert answer.model == "qwen3:4b-instruct"


def test_general_route_fails_closed_when_ollama_is_unavailable(monkeypatch):
    service = GeneralAgentService()

    def unavailable(**_kwargs):
        raise ConnectionError("local model unavailable")

    monkeypatch.setattr(service.ollama, "chat", unavailable)
    result = service.answer("Scrum là gì?")
    assert result.warning == "LOCAL_LLM_UNAVAILABLE"
    assert result.provider == "ollama"
    assert result.tool == GeneralTool.OLLAMA


def test_web_request_returns_explicit_disabled_warning_without_search():
    result = GeneralAgentService().answer("Thời tiết mới nhất ở Hà Nội thế nào?")
    assert result.warning == "WEB_SEARCH_NOT_CONFIGURED"
    assert result.web_grounded is False


def test_acceptance_policy_question_stays_on_rag_and_generates_locally():
    question = "Quy định nghỉ phép công ty là gì?"
    assert _classify(question, _employee_user()) == AiRoute.RAG
    service = RagService.__new__(RagService)
    service.retrieve = lambda *_args, **_kwargs: RouteResult(
        route=AiRoute.RAG,
        answer_draft="",
        data={"knowledge_context": "Nhân viên được nghỉ phép theo quy định."},
        confidence=1.0,
    )

    class LocalModel:
        def chat(self, **_kwargs):
            return "Theo nguồn nội bộ, nhân viên được nghỉ phép theo quy định."

    service.llm = LocalModel()
    result = service.answer(question, _employee_user())
    assert result.route == AiRoute.RAG
    assert result.data["provider"] == "rag_local"
    assert result.data["generation_provider"] == "ollama"


def test_acceptance_department_late_count_uses_typed_tool(monkeypatch):
    question = "Hôm qua phòng IT có bao nhiêu nhân viên đi muộn?"
    assert _classify(question, _employee_user()) == AiRoute.LIVE_SQL
    service = LiveSqlService()
    calls = []

    def typed_tool(**kwargs):
        calls.append(kwargs)
        return {
            "value": 3,
            "tool": "count_late_employees",
            "department": "Phòng IT",
            "table": "Attendances",
            "start_date": kwargs["start_date"].isoformat(),
        }

    monkeypatch.setattr(service.queries, "count_late_employees", typed_tool)
    result = service.answer(question)
    yesterday = date.today() - timedelta(days=1)
    assert result.route == AiRoute.LIVE_SQL
    assert result.data["tool"] == "count_late_employees"
    assert "3 nhân viên đi muộn" in result.answer_draft
    assert calls[0]["start_date"] == yesterday
    assert calls[0]["end_date"] == yesterday
    assert calls[0]["department"] == "IT"


def test_acceptance_personal_late_count_uses_trusted_employee_id(monkeypatch):
    question = "Tháng này tôi đi muộn mấy lần?"
    user = _employee_user()
    assert _classify(question, user) == AiRoute.PERSONAL_DATA
    service = PersonalDataService()
    query_calls = []
    monkeypatch.setattr(
        service.queries,
        "get_employee",
        lambda employee_id: {"id": employee_id, "name": "Test User", "employee_code": "NV041"},
    )

    def late_count(employee_id, start_date, end_date):
        query_calls.append((employee_id, start_date, end_date))
        return 2

    monkeypatch.setattr(service.queries, "get_late_count", late_count)
    result = service.answer(question, user)
    assert result.route == AiRoute.PERSONAL_DATA
    assert result.data["tool"] == "get_my_late_count"
    assert query_calls[0][0] == "41"
    assert query_calls[0][1] == date.today().replace(day=1)
    assert "đi muộn 2 lần" in result.answer_draft


def test_route_provider_diagnostics_never_report_cloud_provider():
    orchestrator = AiOrchestrator.__new__(AiOrchestrator)
    provider, model = orchestrator._telemetry_provider_model(
        AiRoute.RAG,
        RouteResult(route=AiRoute.RAG, answer_draft="", data={}),
    )
    assert (provider, model) == ("rag_local", "qwen3:4b-instruct")


def test_late_employee_sql_uses_only_bound_values(monkeypatch):
    service = LiveQueries()
    monkeypatch.setattr(
        service,
        "_resolve_table",
        lambda candidates: "Attendances" if "Attendances" in candidates else "Employees",
    )
    monkeypatch.setattr(service, "_resolve_column", lambda _table, candidates: candidates[0])
    monkeypatch.setattr(service, "resolve_department", lambda _value: "Phòng IT")
    captured = {}

    def scalar(sql, params=()):
        captured["sql"] = sql
        captured["params"] = params
        return 3

    monkeypatch.setattr(service, "_scalar", scalar)
    day = date(2026, 9, 19)
    result = service.count_late_employees("IT", day, day)
    assert result["value"] == 3
    assert captured["sql"].count("?") == 4
    assert captured["params"] == (0, day, day, "Phòng IT")
    assert "IT" not in captured["sql"]


def test_personal_late_count_is_scoped_and_parameterized(monkeypatch):
    captured = {}

    class Cursor:
        def execute(self, sql, *params):
            captured["sql"] = sql
            captured["params"] = params

        def fetchone(self):
            return (2,)

    class Connection:
        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def cursor(self):
            return Cursor()

    monkeypatch.setattr(personal_queries_module, "get_sql_connection", lambda: Connection())
    result = personal_queries_module.PersonalQueries().get_late_count(
        "41", date(2026, 9, 1), date(2026, 9, 20)
    )
    assert result == 2
    assert captured["sql"].count("?") == 4
    assert captured["params"] == (41, 0, date(2026, 9, 1), date(2026, 9, 20))
    assert "EmployeeId = ?" in captured["sql"]


def test_response_diagnostics_are_development_only(monkeypatch):
    monkeypatch.setattr(settings, "DIAGNOSTICS_ENABLED", True)
    response = AiResponse(
        answer="Local answer",
        route=AiRoute.GENERAL_LLM,
        intent="General",
        diagnostics={"provider": "ollama", "model": "qwen3:4b-instruct"},
    )
    monkeypatch.setenv("AVA_ENVIRONMENT", "production")
    assert "diagnostics" not in _response_payload(response, None)
    monkeypatch.setenv("AVA_ENVIRONMENT", "development")
    assert _response_payload(response, None)["diagnostics"]["provider"] == "ollama"
