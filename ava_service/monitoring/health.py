from __future__ import annotations

import time
from dataclasses import asdict, dataclass
from enum import Enum
from typing import Callable

from config.settings import settings
from monitoring.circuit_breaker import (
    CircuitState,
)
from monitoring.sql_resilience import (
    SqlConnectionFactory,
    execute_scalar_read,
)
from providers.provider_factory import ProviderFactory
from rag.vector_store import (
    VectorStore,
)


class DependencyStatus(str, Enum):
    UP = "UP"
    DOWN = "DOWN"
    DEGRADED = "DEGRADED"


class ReadinessStatus(str, Enum):
    READY = "READY"
    DEGRADED = "DEGRADED"
    NOT_READY = "NOT_READY"


@dataclass(frozen=True)
class DependencyHealth:
    name: str
    status: DependencyStatus
    latency_ms: float
    critical: bool
    detail: str | None = None

    def to_dict(
        self,
    ) -> dict:

        result = asdict(
            self
        )

        result["status"] = (
            self.status.value
        )

        return result


@dataclass(frozen=True)
class HealthReport:
    status: ReadinessStatus
    dependencies: list[DependencyHealth]

    def to_dict(
        self,
    ) -> dict:

        return {
            "status": (
                self.status.value
            ),
            "dependencies": [
                item.to_dict()
                for item
                in self.dependencies
            ],
        }


Probe = Callable[
    [],
    DependencyHealth,
]


class HealthService:
    """
    Phase 20C health/readiness.

    Quy tắc:
    - liveness: process Python đang chạy -> UP.
    - readiness:
        * SQL + Vector DB là critical.
        * General LLM provider là capability dependency:
          DOWN -> DEGRADED, không giả thành SQL/RAG data.
    - Không log secret/connection string/payload.
    """

    def __init__(
        self,
        probes: list[Probe] | None = None,
    ) -> None:

        self.probes = (
            probes
            if probes is not None
            else [
                self.probe_sql,
                self.probe_vector_db,
                self.probe_general_provider,
            ]
        )

    @staticmethod
    def liveness(
    ) -> dict[str, str]:

        return {
            "status": "UP",
        }

    def readiness(
        self,
    ) -> HealthReport:

        dependencies: list[
            DependencyHealth
        ] = []

        for probe in self.probes:

            try:
                result = probe()

            except Exception:
                # Health endpoint không được leak stack/error message.
                result = DependencyHealth(
                    name="unknown",
                    status=DependencyStatus.DOWN,
                    latency_ms=0.0,
                    critical=True,
                    detail="PROBE_FAILED",
                )

            dependencies.append(
                result
            )

        critical_down = any(
            item.critical
            and item.status
            == DependencyStatus.DOWN
            for item
            in dependencies
        )

        any_non_up = any(
            item.status
            != DependencyStatus.UP
            for item
            in dependencies
        )

        if critical_down:
            status = (
                ReadinessStatus.NOT_READY
            )

        elif any_non_up:
            status = (
                ReadinessStatus.DEGRADED
            )

        else:
            status = (
                ReadinessStatus.READY
            )

        return HealthReport(
            status=status,
            dependencies=dependencies,
        )

    @staticmethod
    def _elapsed_ms(
        started_at: float,
    ) -> float:

        return round(
            (
                time.perf_counter()
                - started_at
            )
            * 1000,
            2,
        )

    @classmethod
    def probe_sql(
        cls,
    ) -> DependencyHealth:

        started = time.perf_counter()

        try:

            factory = (
                SqlConnectionFactory()
            )

            value = execute_scalar_read(
                factory,
                "SELECT 1",
            )

            if int(value) != 1:
                raise RuntimeError(
                    "SQL health query failed."
                )

            return DependencyHealth(
                name="sqlserver",
                status=DependencyStatus.UP,
                latency_ms=cls._elapsed_ms(
                    started
                ),
                critical=True,
                detail="SELECT_1_OK",
            )

        except Exception:

            return DependencyHealth(
                name="sqlserver",
                status=DependencyStatus.DOWN,
                latency_ms=cls._elapsed_ms(
                    started
                ),
                critical=True,
                detail="SQL_UNAVAILABLE",
            )

    @classmethod
    def probe_vector_db(
        cls,
    ) -> DependencyHealth:

        started = time.perf_counter()

        try:

            store = VectorStore()

            collection = (
                store.get_collection()
            )

            if collection is None:
                raise RuntimeError(
                    "RAG collection missing."
                )

            count = int(
                collection.count()
            )

            if count <= 0:
                return DependencyHealth(
                    name="vector_db",
                    status=(
                        DependencyStatus
                        .DEGRADED
                    ),
                    latency_ms=(
                        cls._elapsed_ms(
                            started
                        )
                    ),
                    critical=True,
                    detail="COLLECTION_EMPTY",
                )

            return DependencyHealth(
                name="vector_db",
                status=DependencyStatus.UP,
                latency_ms=cls._elapsed_ms(
                    started
                ),
                critical=True,
                detail="COLLECTION_OK",
            )

        except Exception:

            return DependencyHealth(
                name="vector_db",
                status=DependencyStatus.DOWN,
                latency_ms=cls._elapsed_ms(
                    started
                ),
                critical=True,
                detail="VECTOR_DB_UNAVAILABLE",
            )

    @classmethod
    def probe_general_provider(
        cls,
    ) -> DependencyHealth:

        started = time.perf_counter()

        try:

            provider_name = settings.GENERAL_LLM_PROVIDER
            provider = ProviderFactory.create(provider_name)

            breaker = getattr(
                provider,
                "circuit_breaker",
                None,
            )

            if (
                breaker is not None
                and breaker.state
                == CircuitState.OPEN
            ):

                return DependencyHealth(
                    name=provider_name,
                    status=(
                        DependencyStatus
                        .DOWN
                    ),
                    latency_ms=(
                        cls._elapsed_ms(
                            started
                        )
                    ),
                    critical=False,
                    detail="CIRCUIT_OPEN",
                )

            # Không gọi completion ở health check:
            # tránh token/cost/rate-limit chỉ vì probe.
            return DependencyHealth(
                name=provider_name,
                status=DependencyStatus.UP,
                latency_ms=cls._elapsed_ms(
                    started
                ),
                critical=False,
                detail="CLIENT_CONFIGURED",
            )

        except Exception:

            return DependencyHealth(
                name=settings.GENERAL_LLM_PROVIDER,
                status=DependencyStatus.DOWN,
                latency_ms=cls._elapsed_ms(
                    started
                ),
                critical=False,
                detail="PROVIDER_UNAVAILABLE",
            )
