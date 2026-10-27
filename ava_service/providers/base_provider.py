from __future__ import annotations

from typing import Protocol, runtime_checkable

from monitoring.llm_usage import LlmUsage


@runtime_checkable
class LlmTextProvider(Protocol):
    """
    Provider-neutral text generation contract.

    Route/service chỉ biết contract này.
    Không cần biết Groq/OpenAI/Ollama cụ thể.
    """

    provider_name: str
    model_name: str | None

    def chat(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.2,
        max_tokens: int = 1000,
    ) -> str:
        ...

    def chat_with_usage(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.2,
        max_tokens: int = 1000,
    ) -> tuple[str, LlmUsage]:
        ...
