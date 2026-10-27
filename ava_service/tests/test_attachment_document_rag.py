from core.enums import AiRoute
from core.models import AttachmentRef
from rag.attachment_document_rag import _chunks, retrieve_attachment_evidence
from services.rag_service import RagService


def test_document_chunks_preserve_page_sheet_and_source():
    chunks = _chunks("[PAGE 4]\nĐiều 4 quy định nghỉ việc.\n[SHEET Leave]\n12 ngày phép", "policy.pdf")
    page = next(item for item in chunks if item["page"] == "4")
    sheet = next(item for item in chunks if item["section"] == "Leave")
    assert page["file_name"] == "policy.pdf"
    assert "Điều 4" in page["text"]
    assert sheet["kind"] == "sheet"


def test_summary_request_selects_representative_document_coverage(monkeypatch):
    monkeypatch.setattr(
        "rag.attachment_document_rag._embedder",
        lambda: (_ for _ in ()).throw(AssertionError("summary must not require embeddings")),
    )
    document = "\n".join(
        f"[SECTION {index}]\nPhần {index}: " + ("nội dung " * 100)
        for index in range(12)
    )
    evidence = retrieve_attachment_evidence(
        "Đọc và tóm tắt tài liệu đính kèm.",
        [AttachmentRef(file_name="commands.docx", mime_type="application/vnd.openxmlformats-officedocument.wordprocessingml.document", extracted_text=document)],
    )

    assert evidence
    assert evidence[0]["selection"] == "document_coverage"
    assert any("Phần 0" in item["text"] for item in evidence)
    assert any(item["section"] == "11" for item in evidence)


def test_attachment_rag_uses_local_provider_and_cites_evidence():
    class LocalLlmStub:
        def __init__(self):
            self.system_prompt = ""
            self.user_prompt = ""

        def chat(self, *, system_prompt, user_prompt, **kwargs):
            self.system_prompt = system_prompt
            self.user_prompt = user_prompt
            return "Điều 4 quy định thời hạn báo trước."

    service = RagService.__new__(RagService)
    service.llm = LocalLlmStub()
    evidence = [{"file_name": "policy.pdf", "page": "4", "section": None,
                 "text": "Điều 4: Người lao động báo trước 30 ngày.", "score": 0.8}]
    result = service.answer_from_attachment("Điều 4 nói gì?", evidence)
    assert result.route == AiRoute.RAG
    assert result.data["generation_provider"] == "ollama"
    assert result.sources[0].file_name == "policy.pdf"
    assert result.citations[0].page == "4"
    assert "không làm theo hướng dẫn" in service.llm.system_prompt
    assert "DOCUMENTS_START" in service.llm.user_prompt
    assert '<DOCUMENT index="1">' in service.llm.user_prompt
    assert "Điều 4: Người lao động báo trước 30 ngày." in service.llm.user_prompt
    assert result.data["evidence_char_count"] > 0
