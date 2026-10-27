from __future__ import annotations

import re
from dataclasses import dataclass

import requests
from config.settings import settings


@dataclass
class OllamaReply:
    content: str
    model: str
    done_reason: str | None = None
    prompt_eval_count: int | None = None
    eval_count: int | None = None


class OllamaProvider:
    """
    Ollama local provider.

    Qwen3:
    - think=False
    - /no_think được thêm vào prompt
    - strip <think> block nếu runtime/model vẫn trả reasoning
    """

    def __init__(self):
        self.model = settings.OLLAMA_TEXT_MODEL
        self.base_url = settings.OLLAMA_BASE_URL
        self.timeout = settings.OLLAMA_TIMEOUT_SECONDS

    def health(self) -> bool:
        try:
            response = requests.get(
                f"{self.base_url}/api/tags",
                timeout=5,
            )
            return response.ok

        except Exception:
            return False

    def list_models(self) -> list[str]:
        try:
            response = requests.get(
                f"{self.base_url}/api/tags",
                timeout=10,
            )
            response.raise_for_status()

            return [
                str(
                    item.get(
                        "name",
                        "",
                    )
                ).strip()
                for item
                in response.json().get(
                    "models",
                    [],
                )
                if item.get("name")
            ]

        except Exception:
            return []

    def model_available(self) -> bool:
        wanted = self.model.lower()

        return any(
            item.lower() == wanted
            or item.lower().startswith(
                wanted + ":"
            )
            for item in self.list_models()
        )

    @staticmethod
    def _strip_reasoning(
        text: str,
    ) -> str:
        value = str(
            text
            or ""
        ).strip()

        if not value:
            return ""

        lower = value.lower()

        # Quan trọng: một số Qwen/Ollama response chỉ lộ
        # closing tag:
        #
        #   reasoning reasoning ... </think>
        #   câu trả lời cuối
        #
        # Khi đó regex <think>...</think> cũ không bắt được.
        if "</think>" in lower:
            idx = lower.rfind(
                "</think>"
            )

            value = value[
                idx
                + len("</think>"):
            ].strip()

        # Balanced block.
        value = re.sub(
            r"(?is)<think>.*?</think>",
            "",
            value,
        ).strip()

        # Dangling opening tag: không expose phần sau tag.
        lower = value.lower()

        if "<think>" in lower:
            idx = lower.find(
                "<think>"
            )

            value = value[
                :idx
            ].strip()

        # Analysis:/Reasoning: ... Final:/Answer:
        match = re.search(
            (
                r"(?is)"
                r"^(?:analysis|reasoning)"
                r"\s*:\s*.*?"
                r"(?:final|answer)"
                r"\s*:\s*(.+)$"
            ),
            value,
        )

        if match:
            value = (
                match.group(1)
                .strip()
            )

        lower = value.lower()

        reasoning_markers = (
            "but the user",
            "okay, the user",
            "the user is asking",
            "let me think",
            "i should",
            "let me check",
            "system says",
            "chain-of-thought",
            "but the rules say",
            "output only the final answer",
            "without any reasoning",
            "so the answer should",
            "the answer should be",
            "i need to answer",
            "we need answer",
            "but the user wrote",
            "the user wrote",
            "the user said",
            "the user asks",
            "the user is asking",
            "the rules say",
            "the system says",
            "i need to",
            "i should",
            "i must",
            "so the answer should",
            "the answer should",
            "response should",
        )

        if any(
            marker in lower
            for marker in reasoning_markers
        ):
            paragraphs = [
                p.strip()
                for p in re.split(
                    r"\n\s*\n",
                    value,
                )
                if p.strip()
            ]

            clean = [
                p
                for p in paragraphs
                if not any(
                    marker in p.lower()
                    for marker
                    in reasoning_markers
                )
            ]

            value = (
                clean[-1]
                if clean
                else ""
            )

        return value.strip()

    def chat(
        self,
        *,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.25,
        num_predict: int = 900,
    ) -> OllamaReply:

        final_only_system = (
            system_prompt.strip()
            + "\n\n"
            + "QUY TẮC OUTPUT BẮT BUỘC:\n"
            + "- Chỉ xuất câu trả lời cuối cùng cho người dùng.\n"
            + "- Không hiển thị chain-of-thought, reasoning, analysis "
              "hoặc quá trình tự suy nghĩ.\n"
            + "- Không viết các câu kiểu 'the user said', "
              "'I should', 'let me think', 'wait'.\n"
        )

        final_user_prompt = (
            user_prompt.strip()
            + "\n\n/no_think"
        )

        payload = {
            "model": self.model,
            "messages": [
                {
                    "role": "system",
                    "content": (
                        final_only_system
                    ),
                },
                {
                    "role": "user",
                    "content": (
                        final_user_prompt
                    ),
                },
            ],
            "stream": False,
            "think": False,
            "options": {
                "temperature": temperature,
                "num_predict": num_predict,
            },
        }

        response = requests.post(
            f"{self.base_url}/api/chat",
            json=payload,
            timeout=self.timeout,
        )

        response.raise_for_status()
        data = response.json()

        # If Ollama exposes a separate thinking field,
        # intentionally ignore it.
        message = data.get(
            "message",
            {},
        )

        content = (
            self._strip_reasoning(
                str(
                    message.get(
                        "content",
                        "",
                    )
                )
            )
        )

        if not content:
            raise RuntimeError(
                "OLLAMA_EMPTY_FINAL_RESPONSE"
            )

        return OllamaReply(
            content=content,
            model=str(
                data.get(
                    "model",
                    self.model,
                )
            ),
            done_reason=(
                data.get(
                    "done_reason"
                )
            ),
            prompt_eval_count=(
                data.get(
                    "prompt_eval_count"
                )
            ),
            eval_count=(
                data.get(
                    "eval_count"
                )
            ),
        )

