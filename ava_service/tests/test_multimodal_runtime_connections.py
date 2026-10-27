from types import SimpleNamespace

import pytest

from config.settings import settings
from preprocessing import local_multimodal
from preprocessing.multimodal import MultimodalPreprocessor, PreprocessingError, RawAttachment


@pytest.mark.parametrize(
    ("reported", "canonical"),
    [
        ("audio/wave", "audio/wav"),
        ("audio/x-m4a", "audio/mp4"),
        ("application/ogg", "audio/ogg"),
        ("video/webm", "audio/webm"),
        ("application/vnd.ms-excel", "text/csv"),
        ("image/jpg", "image/jpeg"),
        ("text/csv; charset=utf-8", "text/csv"),
    ],
)
def test_browser_mime_aliases_are_canonicalized(reported, canonical):
    attachment = RawAttachment(file_name="upload.bin", mime_type=reported)
    assert MultimodalPreprocessor._resolve_mime(attachment) == canonical


def test_browser_audio_container_signature_overrides_incorrect_audio_subtype():
    attachment = RawAttachment(
        file_name="browser-recording.webm",
        mime_type="audio/webm;codecs=opus",
        content_bytes=b"OggS" + (b"\0" * 32),
    )

    assert MultimodalPreprocessor._validate_attachment(attachment) == "audio/ogg"


def test_unknown_audio_signature_is_still_rejected():
    attachment = RawAttachment(
        file_name="browser-recording.webm",
        mime_type="audio/webm",
        content_bytes=b"not an audio container",
    )

    with pytest.raises(PreprocessingError, match="ATTACHMENT_SIGNATURE_MISMATCH"):
        MultimodalPreprocessor._validate_attachment(attachment)


def test_capability_probe_does_not_treat_text_only_model_as_vision(monkeypatch):
    class Response:
        ok = True

        def __init__(self, capabilities, family="qwen3"):
            self._capabilities = capabilities
            self._family = family

        def json(self):
            return {
                "capabilities": self._capabilities,
                "details": {"family": self._family},
            }

    class Session:
        trust_env = True

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def post(self, _url, *, json, timeout):
            assert timeout == 3
            if json["model"] == settings.OLLAMA_TEXT_MODEL:
                return Response(["completion"], "qwen3")
            return Response(["completion", "tools"])

    monkeypatch.setattr(local_multimodal.requests, "Session", Session)
    monkeypatch.setattr(local_multimodal, "_find_media_binary", lambda _name: "tool.exe")
    monkeypatch.setattr(
        local_multimodal,
        "_resolve_stt_model_path",
        lambda: SimpleNamespace(is_file=lambda: True),
    )

    result = local_multimodal.local_capability_status()

    assert result["text"]["status"] == "ready"
    assert result["vision"]["status"] == "unavailable"
    assert result["ocr"]["status"] == "unavailable"
    assert result["speechToText"]["status"] == "ready"


def test_vision_retries_one_transient_empty_response(monkeypatch):
    responses = ["", '{"text":"Đăng nhập","uncertainty":""}']

    class Response:
        def raise_for_status(self):
            return None

        def json(self):
            return {"message": {"content": responses.pop(0)}}

    class Session:
        trust_env = True

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def post(self, *_args, **_kwargs):
            return Response()

    monkeypatch.setattr(local_multimodal.requests, "Session", Session)
    monkeypatch.setattr(local_multimodal.time, "sleep", lambda _seconds: None)
    attachment = RawAttachment(
        file_name="screen.png",
        mime_type="image/png",
        content_bytes=b"image-bytes",
    )

    result = local_multimodal._ollama_image_json(attachment, "read")

    assert result["text"] == "Đăng nhập"
    assert not responses
