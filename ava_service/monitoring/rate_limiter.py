from __future__ import annotations

import threading
import time
from dataclasses import dataclass


class RateLimitExceededError(RuntimeError):
    pass


@dataclass
class RateLimiterStats:
    allowed: int = 0
    rejected: int = 0
    waited: int = 0


class TokenBucketRateLimiter:
    """
    Lightweight in-process token bucket.

    Đây là burst protection cục bộ, KHÔNG thay thế quota/rate-limit
    chính thức của provider.

    Không log prompt, user data hay payload.
    """

    def __init__(
        self,
        *,
        capacity: float = 10.0,
        refill_per_second: float = 2.0,
        max_wait_seconds: float = 1.5,
    ) -> None:

        if capacity <= 0:
            raise ValueError(
                "capacity must be > 0"
            )

        if refill_per_second <= 0:
            raise ValueError(
                "refill_per_second must be > 0"
            )

        if max_wait_seconds < 0:
            raise ValueError(
                "max_wait_seconds must be >= 0"
            )

        self.capacity = float(
            capacity
        )
        self.refill_per_second = float(
            refill_per_second
        )
        self.max_wait_seconds = float(
            max_wait_seconds
        )

        self._tokens = float(
            capacity
        )

        self._updated_at = (
            time.monotonic()
        )

        self._lock = threading.Lock()
        self.stats = RateLimiterStats()

    def _refill_locked(
        self,
        now: float,
    ) -> None:

        elapsed = max(
            0.0,
            now - self._updated_at,
        )

        if elapsed <= 0:
            return

        self._tokens = min(
            self.capacity,
            self._tokens
            + elapsed
            * self.refill_per_second,
        )

        self._updated_at = now

    def acquire(
        self,
        tokens: float = 1.0,
    ) -> None:

        if tokens <= 0:
            raise ValueError(
                "tokens must be > 0"
            )

        if tokens > self.capacity:
            raise RateLimitExceededError(
                "Requested tokens exceed local limiter capacity."
            )

        started = time.monotonic()

        while True:

            with self._lock:

                now = time.monotonic()

                self._refill_locked(
                    now
                )

                if self._tokens >= tokens:

                    self._tokens -= tokens
                    self.stats.allowed += 1

                    return

                missing = (
                    tokens
                    - self._tokens
                )

                wait_seconds = (
                    missing
                    / self.refill_per_second
                )

            elapsed = (
                time.monotonic()
                - started
            )

            remaining = (
                self.max_wait_seconds
                - elapsed
            )

            if (
                remaining <= 0
                or wait_seconds
                > remaining
            ):

                with self._lock:
                    self.stats.rejected += 1

                raise RateLimitExceededError(
                    "Local provider rate limit exceeded."
                )

            with self._lock:
                self.stats.waited += 1

            time.sleep(
                min(
                    wait_seconds,
                    remaining,
                )
            )
