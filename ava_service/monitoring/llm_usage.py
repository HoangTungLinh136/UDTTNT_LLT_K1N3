from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class LlmUsage:
    """
    Provider-neutral LLM usage.

    Chỉ lưu số liệu kỹ thuật.
    Không chứa prompt, response, secret, PII hay chain-of-thought.
    """

    prompt_tokens: int | None = None
    completion_tokens: int | None = None
    total_tokens: int | None = None
    estimated_cost_usd: float | None = None


class LlmUsageExtractor:
    """
    Phase 19B.

    Hỗ trợ response object hoặc dict có thuộc tính/key `usage`.

    Groq/OpenAI-compatible usage thường có:
    - prompt_tokens
    - completion_tokens
    - total_tokens

    Nếu provider không trả usage, tất cả token field = None.
    Không tự đoán token.
    """

    @staticmethod
    def _read(
        value: Any,
        key: str,
    ) -> Any:

        if value is None:
            return None

        if isinstance(
            value,
            dict,
        ):
            return value.get(
                key
            )

        return getattr(
            value,
            key,
            None,
        )

    @classmethod
    def extract(
        cls,
        response: Any,
    ) -> LlmUsage:

        usage = cls._read(
            response,
            "usage",
        )

        if usage is None:
            return LlmUsage()

        prompt_tokens = cls._as_int(
            cls._read(
                usage,
                "prompt_tokens",
            )
        )

        completion_tokens = cls._as_int(
            cls._read(
                usage,
                "completion_tokens",
            )
        )

        total_tokens = cls._as_int(
            cls._read(
                usage,
                "total_tokens",
            )
        )

        # Một số provider có thể không trả total.
        # Chỉ cộng khi cả 2 thành phần có thật.
        if (
            total_tokens is None
            and prompt_tokens is not None
            and completion_tokens is not None
        ):
            total_tokens = (
                prompt_tokens
                + completion_tokens
            )

        return LlmUsage(
            prompt_tokens=prompt_tokens,
            completion_tokens=(
                completion_tokens
            ),
            total_tokens=total_tokens,
            estimated_cost_usd=None,
        )

    @staticmethod
    def _as_int(
        value: Any,
    ) -> int | None:

        if value is None:
            return None

        try:
            converted = int(
                value
            )
        except (
            TypeError,
            ValueError,
        ):
            return None

        if converted < 0:
            return None

        return converted


@dataclass(frozen=True)
class TokenPrice:
    """
    Giá USD trên 1 triệu token.

    Không có default để tránh tự bịa giá provider/model.
    """

    input_per_million_usd: float
    output_per_million_usd: float


class LlmCostCalculator:
    """
    Tính cost CHỈ khi caller truyền pricing đã được xác minh.

    Không hard-code giá Groq/model vì pricing có thể thay đổi.
    """

    @staticmethod
    def estimate(
        usage: LlmUsage,
        price: TokenPrice | None,
    ) -> LlmUsage:

        if price is None:
            return usage

        if (
            usage.prompt_tokens is None
            or usage.completion_tokens is None
        ):
            return usage

        input_cost = (
            usage.prompt_tokens
            / 1_000_000
            * price.input_per_million_usd
        )

        output_cost = (
            usage.completion_tokens
            / 1_000_000
            * price.output_per_million_usd
        )

        return LlmUsage(
            prompt_tokens=(
                usage.prompt_tokens
            ),
            completion_tokens=(
                usage.completion_tokens
            ),
            total_tokens=(
                usage.total_tokens
            ),
            estimated_cost_usd=round(
                input_cost
                + output_cost,
                10,
            ),
        )
