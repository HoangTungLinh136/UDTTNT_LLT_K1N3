from __future__ import annotations

import os
from dataclasses import dataclass

from openai import OpenAI

from monitoring.llm_usage import LlmUsage


@dataclass(frozen=True)
class OpenAiReply:
    content: str
    usage: LlmUsage


class OpenAiProvider:
    """OpenAI Responses adapter matching the existing AVA provider contract."""

    def __init__(self) -> None:
        api_key = os.getenv("OPENAI_API_KEY", "").strip()
        if not api_key:
            raise RuntimeError("OPENAI_API_KEY_NOT_CONFIGURED")
        self.model = os.getenv("OPENAI_MODEL", "gpt-4o-mini").strip()
        self.client = OpenAI(api_key=api_key, timeout=20.0, max_retries=1)

    def chat_with_usage(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.2,
        max_tokens: int = 800,
    ) -> tuple[str, LlmUsage]:
        response = self.client.responses.create(
            model=self.model,
            instructions=system_prompt,
            input=user_prompt,
            temperature=temperature,
            max_output_tokens=max_tokens,
        )
        usage = getattr(response, "usage", None)
        return (
            (getattr(response, "output_text", "") or "").strip(),
            LlmUsage(
                prompt_tokens=getattr(usage, "input_tokens", None),
                completion_tokens=getattr(usage, "output_tokens", None),
                total_tokens=getattr(usage, "total_tokens", None),
            ),
        )

    def chat(self, system_prompt: str, user_prompt: str, **kwargs) -> str:
        return self.chat_with_usage(system_prompt, user_prompt, **kwargs)[0]
