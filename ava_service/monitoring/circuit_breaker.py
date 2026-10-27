from __future__ import annotations

import threading
import time
from dataclasses import dataclass
from enum import Enum
from typing import Callable, Generic, TypeVar


T = TypeVar("T")


class CircuitState(str, Enum):
    CLOSED = "CLOSED"
    OPEN = "OPEN"
    HALF_OPEN = "HALF_OPEN"


class CircuitOpenError(RuntimeError):
    pass


@dataclass
class CircuitStats:
    calls: int = 0
    successes: int = 0
    failures: int = 0
    rejected: int = 0
    opened: int = 0


class CircuitBreaker(Generic[T]):
    """
    Thread-safe circuit breaker.

    CLOSED:
        cho phép request.
    OPEN:
        reject nhanh trong recovery window.
    HALF_OPEN:
        cho phép 1 probe request.

    Không log payload hay exception message.
    """

    def __init__(
        self,
        *,
        failure_threshold: int = 3,
        recovery_timeout_seconds: float = 20.0,
    ) -> None:

        if failure_threshold <= 0:
            raise ValueError(
                "failure_threshold must be > 0"
            )

        if recovery_timeout_seconds <= 0:
            raise ValueError(
                "recovery_timeout_seconds must be > 0"
            )

        self.failure_threshold = int(
            failure_threshold
        )
        self.recovery_timeout_seconds = float(
            recovery_timeout_seconds
        )

        self._state = CircuitState.CLOSED
        self._consecutive_failures = 0
        self._opened_at: float | None = None
        self._half_open_probe_in_flight = False

        self._lock = threading.Lock()
        self.stats = CircuitStats()

    @property
    def state(
        self,
    ) -> CircuitState:

        with self._lock:
            self._refresh_state_locked()
            return self._state

    def _refresh_state_locked(
        self,
    ) -> None:

        if (
            self._state
            == CircuitState.OPEN
            and self._opened_at
            is not None
            and (
                time.monotonic()
                - self._opened_at
                >= self.recovery_timeout_seconds
            )
        ):
            self._state = (
                CircuitState.HALF_OPEN
            )
            self._half_open_probe_in_flight = False

    def _before_call(
        self,
    ) -> None:

        with self._lock:

            self._refresh_state_locked()

            if self._state == CircuitState.OPEN:
                self.stats.rejected += 1
                raise CircuitOpenError(
                    "Provider circuit is open."
                )

            if (
                self._state
                == CircuitState.HALF_OPEN
            ):
                if self._half_open_probe_in_flight:
                    self.stats.rejected += 1
                    raise CircuitOpenError(
                        "Provider recovery probe already running."
                    )

                self._half_open_probe_in_flight = True

            self.stats.calls += 1

    def _on_success(
        self,
    ) -> None:

        with self._lock:
            self._state = CircuitState.CLOSED
            self._consecutive_failures = 0
            self._opened_at = None
            self._half_open_probe_in_flight = False
            self.stats.successes += 1

    def _on_failure(
        self,
    ) -> None:

        with self._lock:

            self.stats.failures += 1
            self._consecutive_failures += 1
            self._half_open_probe_in_flight = False

            if (
                self._state
                == CircuitState.HALF_OPEN
                or self._consecutive_failures
                >= self.failure_threshold
            ):
                self._state = CircuitState.OPEN
                self._opened_at = time.monotonic()
                self.stats.opened += 1

    def call(
        self,
        func: Callable[[], T],
    ) -> T:

        self._before_call()

        try:
            result = func()

        except Exception:
            self._on_failure()
            raise

        self._on_success()
        return result
