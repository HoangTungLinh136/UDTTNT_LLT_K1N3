import pytest
import base64
import io
import wave
from fastapi import HTTPException

from api.main import _build_normalized_input
from api.schemas import ChatRequest
from preprocessing import local_multimodal


def _request(modality: str, mime: str) -> ChatRequest:
    return ChatRequest(
        text="Hãy phân tích file này",
        modality=modality,
        userContext={
            "userId": "test-user",
            "isAuthenticated": True,
        },
        attachments=[
            {
                "fileName": "input.bin",
                "mimeType": mime,
                "extractedText": "Nội dung đã được parser xử lý.",
            }
        ],
    )


@pytest.mark.parametrize(
    "mime",
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    ],
)
def test_document_attachment_becomes_context_before_router(mime: str):
    normalized = _build_normalized_input(
        _request("DOCUMENT", mime)
    )
    assert "[ATTACHMENT_DATA]" in normalized.text
    assert "Nội dung đã được parser xử lý." in normalized.text
    assert normalized.metadata["attachment_count"] == 1


def test_image_and_voice_are_modalities_not_routes():
    image = _build_normalized_input(_request("IMAGE", "image/png"))
    voice = _build_normalized_input(_request("VOICE", "audio/wav"))
    assert image.modality.value == "IMAGE"
    assert voice.modality.value == "VOICE"
    assert "[ATTACHMENT_DATA]" in image.text
    assert "[ATTACHMENT_DATA]" in voice.text


def test_binary_txt_is_parsed_locally_before_context_routing():
    request = ChatRequest(
        text="Tóm tắt file",
        modality="DOCUMENT",
        userContext={"userId": "test-user", "isAuthenticated": True},
        attachments=[
            {
                "fileName": "policy.txt",
                "mimeType": "text/plain",
                "contentBase64": base64.b64encode("Ngày nghỉ phép: 12".encode()).decode(),
            }
        ],
    )
    normalized = _build_normalized_input(request)
    assert normalized.text == "Tóm tắt file"
    assert normalized.metadata["document_rag_evidence"][0]["text"] == "Ngày nghỉ phép: 12"
    assert normalized.attachments[0].extracted_text == "Ngày nghỉ phép: 12"
    assert normalized.metadata["document_rag_evidence"]
    assert normalized.metadata["document_rag_evidence"][0]["file_name"] == "policy.txt"


def test_multiple_binary_text_attachments_keep_separate_sources():
    request = ChatRequest(
        text="So sánh hai tài liệu",
        modality="DOCUMENT",
        userContext={"userId": "test-user", "isAuthenticated": True},
        attachments=[
            {"fileName": "one.txt", "mimeType": "text/plain", "contentBase64": base64.b64encode(b"Chinh sach nghi phep 12 ngay").decode()},
            {"fileName": "two.csv", "mimeType": "text/csv", "contentBase64": base64.b64encode(b"Ten,Ngay\nAn,2026-09-20").decode()},
        ],
    )
    normalized = _build_normalized_input(request)
    evidence_files = {item["file_name"] for item in normalized.metadata["document_rag_evidence"]}
    assert evidence_files <= {"one.txt", "two.csv"}
    assert len(normalized.attachments) == 2


def test_invalid_binary_attachment_fails_closed():
    request = ChatRequest(
        text="Đọc file",
        modality="DOCUMENT",
        userContext={"userId": "test-user", "isAuthenticated": True},
        attachments=[
            {"fileName": "note.txt", "mimeType": "text/plain", "contentBase64": "%%%"}
        ],
    )
    with pytest.raises(HTTPException) as error:
        _build_normalized_input(request)
    assert error.value.status_code == 422
    assert error.value.detail == "ATTACHMENT_BASE64_INVALID"


def test_browser_audio_alias_is_inferred_as_voice_and_keeps_transcript(monkeypatch):
    audio = io.BytesIO()
    with wave.open(audio, "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(16_000)
        output.writeframes(b"\0\0" * 1_600)
    monkeypatch.setattr(local_multimodal, "local_transcribe", lambda _item: "Xin chào AVA")
    request = ChatRequest(
        text="Chuyển lời nói thành văn bản và xử lý nội dung.",
        modality="DOCUMENT",
        userContext={"userId": "test-user", "isAuthenticated": True},
        attachments=[{
            "fileName": "recording.wav",
            "mimeType": "audio/wave; codecs=pcm",
            "contentBase64": base64.b64encode(audio.getvalue()).decode(),
        }],
    )

    normalized = _build_normalized_input(request)

    assert normalized.modality.value == "VOICE"
    assert "Xin chào AVA" in normalized.text
    assert normalized.metadata["modalities_present"] == ["VOICE"]
