from __future__ import annotations

from dataclasses import asdict

from config.settings import settings

from monitoring.llm_usage import (
    LlmUsage,
)

from services.ollama_provider import (
    OllamaProvider,
)


class OllamaTextProvider:
    """
    Adapter biến OllamaProvider hiện tại thành
    provider-neutral contract.

    Ollama hiện không trả token usage chuẩn,
    vì vậy usage để None thay vì tự đoán.
    """

    provider_name = "ollama"

    def __init__(
        self,
        model_name: str | None = None,
    ):

        self.model_name = (
            model_name
            or settings.OLLAMA_TEXT_MODEL
        )

        self.client = (
            OllamaProvider()
        )

    def chat(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.2,
        max_tokens: int = 1000,
    ) -> str:

        response = (
            self.client.chat(
                system_prompt=system_prompt,
                user_prompt=user_prompt,
                temperature=temperature,
                num_predict=max_tokens,
            )
        )

        content = getattr(
            response,
            "content",
            response,
        )

        return str(
            content
            or ""
        ).strip()

    def chat_with_usage(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.2,
        max_tokens: int = 1000,
    ) -> tuple[str, LlmUsage]:

        return (
            self.chat(
                system_prompt=system_prompt,
                user_prompt=user_prompt,
                temperature=temperature,
                max_tokens=max_tokens,
            ),
            LlmUsage(),
        )

    @staticmethod
    def usage_to_dict(
        usage: LlmUsage,
    ) -> dict:

        return asdict(
            usage
        )
