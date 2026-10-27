from __future__ import annotations

import hashlib
import threading
import time
from collections import OrderedDict
from dataclasses import dataclass
from typing import Generic, TypeVar


T = TypeVar("T")


@dataclass
class CacheStats:
    hits: int = 0
    misses: int = 0
    evictions: int = 0
    expirations: int = 0


class SafeTtlCache(Generic[T]):
    """
    In-memory TTL + LRU cache.

    Security properties:
    - raw keys are never stored;
    - keys are SHA-256 hashed;
    - values are process-memory only;
    - bounded size;
    - TTL expiry;
    - no persistence/logging of payloads.

    Intended Phase 19D use:
    cache deterministic, non-authoritative computations such as
    query embeddings.

    Do NOT use this as a source-of-truth cache for:
    - Personal Data;
    - salary/payroll;
    - Live SQL/current HRMS state;
    - authorization decisions.
    """

    def __init__(
        self,
        *,
        ttl_seconds: float = 300.0,
        max_entries: int = 256,
    ) -> None:

        if ttl_seconds <= 0:
            raise ValueError(
                "ttl_seconds must be > 0"
            )

        if max_entries <= 0:
            raise ValueError(
                "max_entries must be > 0"
            )

        self.ttl_seconds = float(
            ttl_seconds
        )
        self.max_entries = int(
            max_entries
        )

        self._items: OrderedDict[
            str,
            tuple[float, T],
        ] = OrderedDict()

        self._lock = threading.Lock()
        self.stats = CacheStats()

    @staticmethod
    def _hash_key(
        key: str,
    ) -> str:

        return hashlib.sha256(
            key.encode(
                "utf-8"
            )
        ).hexdigest()

    def get(
        self,
        key: str,
    ) -> T | None:

        hashed = self._hash_key(
            key
        )

        now = time.monotonic()

        with self._lock:

            item = self._items.get(
                hashed
            )

            if item is None:
                self.stats.misses += 1
                return None

            expires_at, value = item

            if now >= expires_at:

                self._items.pop(
                    hashed,
                    None,
                )

                self.stats.expirations += 1
                self.stats.misses += 1

                return None

            self._items.move_to_end(
                hashed
            )

            self.stats.hits += 1

            return value

    def set(
        self,
        key: str,
        value: T,
    ) -> None:

        hashed = self._hash_key(
            key
        )

        expires_at = (
            time.monotonic()
            + self.ttl_seconds
        )

        with self._lock:

            if hashed in self._items:
                self._items.pop(
                    hashed
                )

            self._items[
                hashed
            ] = (
                expires_at,
                value,
            )

            while (
                len(self._items)
                > self.max_entries
            ):

                self._items.popitem(
                    last=False
                )

                self.stats.evictions += 1

    def clear(
        self,
    ) -> None:

        with self._lock:
            self._items.clear()

    def __len__(
        self,
    ) -> int:

        with self._lock:
            return len(
                self._items
            )
