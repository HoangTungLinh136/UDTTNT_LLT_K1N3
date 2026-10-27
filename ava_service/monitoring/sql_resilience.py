from __future__ import annotations

import time
from dataclasses import dataclass
from typing import Any, Callable, Iterable, TypeVar

import pyodbc

from config.settings import settings
from database.connection import build_sql_connection_string
from monitoring.circuit_breaker import (
    CircuitBreaker,
    CircuitOpenError,
)


T = TypeVar("T")


class SqlUnavailableError(RuntimeError):
    """
    Public-safe dependency failure.
    Không chứa connection string, SQL text hay DB payload.
    """


@dataclass
class SqlResilienceStats:
    connect_attempts: int = 0
    operation_attempts: int = 0
    retries: int = 0
    failures: int = 0


class SqlConnectionFactory:
    """
    Phase 20D.

    Chính sách:
    - connection timeout ngắn;
    - circuit breaker cho lỗi dependency liên tiếp;
    - retry CHỈ cho operation read-only do caller khai báo;
    - không retry write/transaction tự động;
    - không log SQL text / params / secrets.
    """

    CONNECTION_TIMEOUT_SECONDS = 3
    READ_QUERY_TIMEOUT_SECONDS = 5
    READ_RETRIES = 1

    def __init__(
        self,
        *,
        connector: Callable[..., Any] | None = None,
    ) -> None:

        self.connector = (
            connector
            or pyodbc.connect
        )

        self.circuit_breaker = (
            CircuitBreaker[
                Any
            ](
                failure_threshold=3,
                recovery_timeout_seconds=15.0,
            )
        )

        self.stats = SqlResilienceStats()

    @staticmethod
    def build_connection_string() -> str:
        return build_sql_connection_string(
            timeout=SqlConnectionFactory.CONNECTION_TIMEOUT_SECONDS
        )

    def connect(
        self,
    ) -> Any:

        connection_string = (
            self.build_connection_string()
        )

        def invoke():

            self.stats.connect_attempts += 1

            return self.connector(
                connection_string
            )

        try:
            return self.circuit_breaker.call(
                invoke
            )

        except CircuitOpenError as exc:
            self.stats.failures += 1

            raise SqlUnavailableError(
                "SQL dependency is temporarily unavailable."
            ) from exc

        except Exception as exc:
            self.stats.failures += 1

            raise SqlUnavailableError(
                "SQL dependency is unavailable."
            ) from exc

    def execute_read(
        self,
        operation: Callable[
            [Any],
            T,
        ],
        *,
        retries: int | None = None,
    ) -> T:
        """
        Wrapper cho read-only business operation.

        operation nhận connection đã mở.

        Chỉ retry read operation; caller phải đảm bảo operation
        không ghi dữ liệu và không có side effect.
        """

        retry_count = (
            self.READ_RETRIES
            if retries is None
            else max(
                0,
                int(retries),
            )
        )

        last_error: Exception | None = None

        for attempt in range(
            retry_count + 1
        ):

            conn = None

            try:

                self.stats.operation_attempts += 1

                conn = self.connect()

                result = operation(
                    conn
                )

                return result

            except SqlUnavailableError as exc:

                last_error = exc

            except Exception as exc:

                last_error = exc

            finally:

                if conn is not None:
                    try:
                        conn.close()
                    except Exception:
                        pass

            if attempt < retry_count:

                self.stats.retries += 1

                # Nhỏ, bounded, chỉ read.
                time.sleep(
                    0.05
                    * (
                        2 ** attempt
                    )
                )

        self.stats.failures += 1

        raise SqlUnavailableError(
            "SQL read operation failed."
        ) from last_error

    def execute_write(
        self,
        operation: Callable[
            [Any],
            T,
        ],
    ) -> T:
        """
        Write operation: KHÔNG auto retry.

        Tránh duplicate insert/update khi không biết transaction
        phía server đã commit hay chưa.
        """

        conn = None

        try:

            self.stats.operation_attempts += 1

            conn = self.connect()

            return operation(
                conn
            )

        except SqlUnavailableError:
            raise

        except Exception as exc:

            self.stats.failures += 1

            raise SqlUnavailableError(
                "SQL write operation failed."
            ) from exc

        finally:

            if conn is not None:
                try:
                    conn.close()
                except Exception:
                    pass


def execute_scalar_read(
    factory: SqlConnectionFactory,
    sql: str,
    params: Iterable[Any] = (),
) -> Any:
    """
    Helper low-level cho SQL read parameterized.

    Không concatenate user input ở đây.
    """

    def operation(
        conn: Any,
    ) -> Any:

        # pyodbc.Cursor không có thuộc tính `timeout`.
        # Query timeout của pyodbc được đặt ở Connection.
        try:
            conn.timeout = (
                factory
                .READ_QUERY_TIMEOUT_SECONDS
            )
        except Exception:
            # Một số driver có thể không cho set query timeout.
            # Connection timeout vẫn được enforce ở lúc connect.
            pass

        cursor = conn.cursor()

        try:

            cursor.execute(
                sql,
                tuple(params),
            )

            row = cursor.fetchone()

            if row is None:
                return None

            return row[0]

        finally:
            try:
                cursor.close()
            except Exception:
                pass

    return factory.execute_read(
        operation
    )
