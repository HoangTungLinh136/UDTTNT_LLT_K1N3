from __future__ import annotations

from dataclasses import asdict
from typing import Any

from groq import Groq

from config.settings import settings
from monitoring.llm_usage import (
    LlmUsage,
    LlmUsageExtractor,
)
from monitoring.circuit_breaker import (
    CircuitBreaker,
)
from monitoring.rate_limiter import (
    TokenBucketRateLimiter,
)


class GroqProvider:

    # Phase 20B production defaults.
    # Groq SDK officially supports timeout/max_retries.
    REQUEST_TIMEOUT_SECONDS = 20.0
    SDK_MAX_RETRIES = 1

    """
    Adapter Groq.

    `chat()` được giữ nguyên contract cũ -> str
    để không làm vỡ RAG/General code hiện tại.

    `chat_with_usage()` là contract mới Phase 19C:
    trả text + usage kỹ thuật từ provider.
    """

    def __init__(self):

        if not settings.GROQ_API_KEY:
            raise RuntimeError(
                "Chưa cấu hình GROQ_API_KEY trong file .env"
            )

        self.client = Groq(
            api_key=settings.GROQ_API_KEY,
            timeout=(
                self.REQUEST_TIMEOUT_SECONDS
            ),
            max_retries=(
                self.SDK_MAX_RETRIES
            ),
        )

        # Phase 20B:
        # local burst protection, independent of provider quota.
        self.rate_limiter = (
            TokenBucketRateLimiter(
                capacity=10.0,
                refill_per_second=2.0,
                max_wait_seconds=1.5,
            )
        )

        # Phase 20A:
        # fail-fast sau nhiều provider failures liên tiếp.
        # Không thay đổi source-of-truth/fallback semantics.
        self.circuit_breaker = CircuitBreaker[
            Any
        ](
            failure_threshold=3,
            recovery_timeout_seconds=20.0,
        )

    def _create_completion(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float,
        max_tokens: int,
    ) -> Any:

        def invoke():
            return (
                self.client
                .chat
                .completions
                .create(
                    model=settings.LLM_MODEL,
                    messages=[
                        {
                            "role": "system",
                            "content": system_prompt,
                        },
                        {
                            "role": "user",
                            "content": user_prompt,
                        },
                    ],
                    temperature=temperature,
                    max_tokens=max_tokens,
                )
            )

        limiter = getattr(
            self,
            "rate_limiter",
            None,
        )

        if limiter is not None:
            limiter.acquire()

        breaker = getattr(
            self,
            "circuit_breaker",
            None,
        )

        if breaker is None:
            # Giữ tương thích với unit tests tạo provider bằng __new__.
            return invoke()

        return breaker.call(
            invoke
        )

    @staticmethod
    def _extract_text(
        response: Any,
    ) -> str:

        choices = getattr(
            response,
            "choices",
            None,
        )

        if not choices:
            return ""

        content = (
            choices[0]
            .message
            .content
        )

        return (
            content.strip()
            if content
            else ""
        )

    def chat(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.2,
        max_tokens: int = 1000,
    ) -> str:
        """
        Backward-compatible API.

        Không đổi kiểu trả về để các route cũ
        vẫn hoạt động bình thường.
        """

        response = self._create_completion(
            system_prompt=system_prompt,
            user_prompt=user_prompt,
            temperature=temperature,
            max_tokens=max_tokens,
        )

        return self._extract_text(
            response
        )

    def chat_with_usage(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.2,
        max_tokens: int = 1000,
    ) -> tuple[str, LlmUsage]:
        """
        Phase 19C.

        Usage được lấy trực tiếp từ response provider.
        Không tự đoán token.
        Không tự gắn pricing/cost.
        """

        response = self._create_completion(
            system_prompt=system_prompt,
            user_prompt=user_prompt,
            temperature=temperature,
            max_tokens=max_tokens,
        )

        text = self._extract_text(
            response
        )

        usage = (
            LlmUsageExtractor
            .extract(
                response
            )
        )

        return (
            text,
            usage,
        )

    @staticmethod
    def usage_to_dict(
        usage: LlmUsage,
    ) -> dict[str, int | float | None]:
        """
        Chỉ scalar kỹ thuật an toàn để đưa vào RouteResult.data.
        """

        return asdict(
            usage
        )
