"""Exercise the real API -> resolver -> intent -> router boundary; mock only route I/O."""
import base64
from unittest.mock import Mock

import pytest
from api import main
from api.schemas import ChatRequest
from core.enums import AiRoute
from core.models import RouteResult
from core.orchestrator import AiOrchestrator
from rag import attachment_document_rag


@pytest.fixture
def engine(monkeypatch):
    # Avoid model downloads; keep the real chunk/retrieval and routing code.
    embedder = Mock()
    embedder.embed_query.return_value = [1.0]
    embedder.embed_documents.side_effect = lambda docs: [[1.0] for _ in docs]
    monkeypatch.setattr(attachment_document_rag, "_embedder", lambda: embedder)
    engine = AiOrchestrator()
    engine.telemetry = Mock()
    calls = []

    def answer(route):
        def run(*args, **kwargs):
            calls.append((route, args))
            return RouteResult(route=route, answer_draft="Nội dung kiểm thử từ nguồn đã chọn.", data={"test": True}, confidence=1.0)
        return run

    rag = Mock()
    rag.answer_from_attachment.side_effect = answer(AiRoute.RAG)
    rag.answer.side_effect = answer(AiRoute.RAG)
    engine._get_rag_service = lambda: rag
    engine.general_llm_service.answer_with_context = answer(AiRoute.GENERAL_LLM)
    engine.personal_data_service.answer = answer(AiRoute.PERSONAL_DATA)
    engine.live_sql_service.answer = answer(AiRoute.LIVE_SQL)
    monkeypatch.setattr(main, "get_orchestrator", lambda: engine)
    return engine, calls


def request(text, **kwargs):
    return ChatRequest(text=text, userContext={"userId": "integration-user", "employeeId": "1", "employeeCode": "NV001", "isAuthenticated": True, "roles": ["HR"]}, **kwargs)


def test_contract_document_then_followup_then_personal_then_new_conversation(engine):
    _, calls = engine
    document = "Hợp đồng của tôi. Nhân viên NV002. Điều 1: thời hạn 12 tháng."
    first = main._process(request("phân tích tài liệu này", modality="DOCUMENT", attachments=[{
        "fileName": "contract.txt", "mimeType": "text/plain",
        "contentBase64": base64.b64encode(document.encode()).decode(),
    }]))
    assert calls[-1][0] == AiRoute.RAG
    context = first["attachmentContext"]
    assert context["items"][0]["extracted_text"] == document
    history = [{"role": "user", "content": "phân tích tài liệu này", "attachmentContext": context}]
    followup = main._process(request("trong tài liệu đó có gì", history=history))
    assert followup["route"] == "RAG", "Attachment text must not select personal-data route"
    assert any("12 tháng" in str(arg) for arg in calls[-1][1]), "Follow-up must receive the previous attachment"
    personal = main._process(request("hợp đồng của tôi", history=history))
    assert personal["route"] == "PERSONAL_DATA"
    assert calls[-1][0] == AiRoute.PERSONAL_DATA
    main._process(request("Xin chào", history=[]))
    assert "12 tháng" not in str(calls[-1][1]), "New conversation must not reuse attachment"


def test_exact_four_routes():
    assert {route.value for route in AiRoute} == {"RAG", "LIVE_SQL", "PERSONAL_DATA", "GENERAL_LLM"}


def test_http_response_preserves_context_for_next_turn(engine):
    from fastapi.testclient import TestClient
    from api.security import require_internal_service_key

    main.app.dependency_overrides[require_internal_service_key] = lambda: None
    try:
        client = TestClient(main.app)
        first = client.post("/api/v1/chat", json={
            "text": "phân tích tài liệu này", "modality": "DOCUMENT",
            "userContext": {"userId": "integration-user", "isAuthenticated": True, "roles": ["HR"]},
            "attachments": [{"fileName": "contract.txt", "mimeType": "text/plain",
                             "contentBase64": base64.b64encode("Hợp đồng thời hạn 12 tháng.".encode()).decode()}],
        })
        assert first.status_code == 200
        context = first.json()["attachmentContext"]
        assert "12 tháng" in context["items"][0]["extracted_text"]
        followup = client.post("/api/v1/chat", json={
            "text": "trong tài liệu đó có gì",
            "userContext": {"userId": "integration-user", "isAuthenticated": True, "roles": ["HR"]},
            "history": [{"role": "user", "content": "phân tích tài liệu này", "attachmentContext": context}],
        })
        assert followup.status_code == 200
        assert followup.json()["route"] == "RAG"
        assert "12 tháng" in str(engine[1][-1][1])
    finally:
        main.app.dependency_overrides.pop(require_internal_service_key, None)
