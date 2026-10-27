from __future__ import annotations

from dataclasses import dataclass
from typing import Callable


class TtsUnavailableError(RuntimeError):
    pass


@dataclass(frozen=True)
class TtsResult:
    audio_bytes: bytes
    mime_type: str = "audio/mpeg"


TtsEngine = Callable[
    [str],
    bytes,
]


class OptionalTtsAdapter:
    """
    TTS là output adapter tùy chọn, không phải route.

    Không tự gọi engine khi can_speak=False.
    """

    def __init__(
        self,
        engine: TtsEngine | None = None,
    ) -> None:

        self.engine = engine

    def synthesize(
        self,
        text: str,
        *,
        can_speak: bool,
    ) -> TtsResult | None:

        if not can_speak:
            return None

        if not text.strip():
            return None

        if self.engine is None:
            raise TtsUnavailableError(
                "TTS_NOT_CONFIGURED"
            )

        audio = self.engine(
            text
        )

        if not audio:
            raise TtsUnavailableError(
                "TTS_EMPTY_AUDIO"
            )

        return TtsResult(
            audio_bytes=audio
        )
