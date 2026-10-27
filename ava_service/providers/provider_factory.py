from __future__ import annotations

from typing import Any

from config.settings import settings


class ProviderFactory:
    """
    Central provider registry.

    The runtime deliberately exposes only the local Ollama provider.

    Import lazy để:
    - không yêu cầu API key của provider không dùng;
    - tránh khởi tạo SDK không cần thiết;
    - giảm coupling giữa service và provider.
    """

    SUPPORTED = {"ollama"}

    ALIASES = {
        "local": "ollama",
    }

    @classmethod
    def normalize(
        cls,
        provider_name: str | None,
    ) -> str:

        value = str(
            provider_name
            or ""
        ).strip().lower()

        value = (
            cls.ALIASES.get(
                value,
                value,
            )
        )

        return value

    @classmethod
    def create(
        cls,
        provider_name: str | None = None,
    ) -> Any:

        name = cls.normalize(
            provider_name
            or settings.GENERAL_LLM_PROVIDER
        )

        if name not in cls.SUPPORTED:

            raise ValueError("Unsupported LLM provider; only local Ollama is enabled")

        if name == "ollama":

            from providers.ollama_text_provider import (
                OllamaTextProvider,
            )

            return OllamaTextProvider()

        raise ValueError("Unsupported LLM provider; only local Ollama is enabled")

    @classmethod
    def model_name(
        cls,
        provider_name: str | None,
    ) -> str | None:

        name = cls.normalize(
            provider_name
        )

        if name == "ollama":
            return settings.OLLAMA_TEXT_MODEL

        return None

    @classmethod
    def describe(
        cls,
        provider_name: str | None,
    ) -> tuple[
        str | None,
        str | None,
    ]:

        name = cls.normalize(
            provider_name
        )

        if name not in cls.SUPPORTED:
            return (
                None,
                None,
            )

        return (
            name,
            cls.model_name(
                name
            ),
        )
