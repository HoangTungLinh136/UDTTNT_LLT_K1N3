import pyodbc

from config.settings import settings


def get_sql_driver() -> str:
    """Select an installed SQL Server ODBC driver without weakening auth."""

    available = set(pyodbc.drivers())
    preferred = settings.SQL_DRIVER.strip()
    candidates = ([preferred] if preferred else []) + [
        "ODBC Driver 18 for SQL Server",
        "ODBC Driver 17 for SQL Server",
        "SQL Server",
    ]
    for candidate in candidates:
        if candidate and candidate in available:
            return candidate
    raise RuntimeError("SQL_DRIVER_NOT_AVAILABLE")


def build_sql_connection_string(timeout: int = 5) -> str:
    return (
        f"DRIVER={{{get_sql_driver()}}};"
        f"SERVER={settings.SQL_SERVER};"
        f"DATABASE={settings.SQL_DATABASE};"
        "Trusted_Connection=yes;"
        "Encrypt=no;"
        "TrustServerCertificate=yes;"
        f"Connection Timeout={timeout};"
    )


def get_sql_connection() -> pyodbc.Connection:
    return pyodbc.connect(
        build_sql_connection_string(),
        timeout=5,
    )
