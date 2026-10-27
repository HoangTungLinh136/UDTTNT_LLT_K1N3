from __future__ import annotations

import re
import unicodedata

from datetime import date
from typing import Any

from database.connection import (
    get_sql_connection,
)


class LiveQueryError(Exception):
    pass


class LiveQueries:

    # =====================================================
    # WHITELIST TABLE CANDIDATES
    # =====================================================

    EMPLOYEE_TABLES = [
        "Employees",
        "Employee",
    ]

    CONTRACT_TABLES = [
        "Contracts",
        "EmployeeContracts",
        "LaborContracts",
    ]

    LEAVE_TABLES = [
        "LeaveRequests",
        "Leaves",
        "LeaveApplications",
    ]

    ATTENDANCE_TABLES = [
        "Attendances",
        "AttendanceRecords",
        "TimeAttendances",
    ]

    # =====================================================
    # WHITELIST COLUMN CANDIDATES
    # =====================================================

    EMPLOYEE_DEPARTMENT_COLUMNS = [
        "Department",
        "DepartmentName",
        "DepartmentCode",
    ]

    ATTENDANCE_DATE_COLUMNS = [
        "CheckIn",
        "AttendanceDate",
        "WorkDate",
        "Date",
        "CheckInTime",
        "CreatedAt",
    ]

    # =====================================================
    # NORMALIZE TEXT
    # =====================================================

    def _normalize_text(
        self,
        value: str,
    ) -> str:

        value = unicodedata.normalize(
            "NFC",
            str(value or ""),
        )

        value = value.strip().lower()

        value = re.sub(
            r"\s+",
            " ",
            value,
        )

        return value

    # =====================================================
    # NORMALIZE DEPARTMENT KEY
    #
    # "Phòng IT"  -> "it"
    # "IT"        -> "it"
    #
    # "Phòng nhân sự" -> "nhân sự"
    # "Nhân sự"       -> "nhân sự"
    # =====================================================

    def _normalize_department_key(
        self,
        value: str,
    ) -> str:

        q = self._normalize_text(
            value
        )

        prefixes = [
            "phòng ban ",
            "phòng ",
            "bộ phận ",
            "department ",
        ]

        for prefix in prefixes:

            if q.startswith(
                prefix
            ):

                q = q[
                    len(prefix):
                ].strip()

                break

        return q

    # =====================================================
    # SCHEMA HELPERS
    # =====================================================

    def _get_existing_tables(
        self,
    ) -> set[str]:

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                """
            )

            return {
                str(row[0])
                for row
                in cursor.fetchall()
            }

    def _resolve_table(
        self,
        candidates: list[str],
    ) -> str:

        existing = (
            self._get_existing_tables()
        )

        for candidate in candidates:

            if candidate in existing:
                return candidate

        raise LiveQueryError(
            "Không tìm thấy bảng phù hợp "
            "trong whitelist."
        )

    def _get_columns(
        self,
        table_name: str,
    ) -> set[str]:

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = ?
                """,
                table_name,
            )

            return {
                str(row[0])
                for row
                in cursor.fetchall()
            }

    def _resolve_column(
        self,
        table_name: str,
        candidates: list[str],
    ) -> str:

        columns = (
            self._get_columns(
                table_name
            )
        )

        for candidate in candidates:

            if candidate in columns:
                return candidate

        raise LiveQueryError(
            f"Không tìm thấy cột phù hợp "
            f"trong bảng {table_name}."
        )

    # =====================================================
    # SAFE SCALAR
    # =====================================================

    def _scalar(
        self,
        sql: str,
        params: tuple[Any, ...] = (),
    ) -> int:

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                sql,
                params,
            )

            row = cursor.fetchone()

            if row is None:
                return 0

            value = row[0]

            if value is None:
                return 0

            return int(value)

    # =====================================================
    # GET ACTUAL DEPARTMENTS
    # =====================================================

    def get_departments(
        self,
    ) -> list[str]:

        table = (
            self._resolve_table(
                self.EMPLOYEE_TABLES
            )
        )

        department_column = (
            self._resolve_column(
                table,
                self.EMPLOYEE_DEPARTMENT_COLUMNS,
            )
        )

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                f"""
                SELECT DISTINCT
                    [{department_column}]
                FROM [{table}]
                WHERE
                    [{department_column}]
                    IS NOT NULL
                """
            )

            departments: list[str] = []

            for row in cursor.fetchall():

                value = row[0]

                if value is None:
                    continue

                value = str(
                    value
                ).strip()

                if value:
                    departments.append(
                        value
                    )

            return departments

    # =====================================================
    # RESOLVE DEPARTMENT
    # =====================================================

    def resolve_department(
        self,
        department: str,
    ) -> str | None:

        requested = str(
            department or ""
        ).strip()

        if not requested:
            return None

        departments = (
            self.get_departments()
        )

        # -------------------------------------------------
        # 1. EXACT NORMALIZED NAME
        #
        # "Phòng IT" → "Phòng IT"
        # -------------------------------------------------

        requested_full = (
            self._normalize_text(
                requested
            )
        )

        for actual in departments:

            if (
                self._normalize_text(
                    actual
                )
                == requested_full
            ):

                return actual

        # -------------------------------------------------
        # 2. CANONICAL KEY
        #
        # "IT" ↔ "Phòng IT"
        # "Nhân sự" ↔ "Phòng nhân sự"
        # -------------------------------------------------

        requested_key = (
            self._normalize_department_key(
                requested
            )
        )

        matches = [
            actual
            for actual in departments
            if (
                self._normalize_department_key(
                    actual
                )
                == requested_key
            )
        ]

        if len(
            matches
        ) == 1:

            return matches[0]

        # Nếu có >1 kết quả trùng canonical key
        # thì không tự đoán.
        if len(
            matches
        ) > 1:

            raise LiveQueryError(
                "Tên phòng ban không đủ "
                "để xác định duy nhất."
            )

        return None

    # =====================================================
    # EMPLOYEE COUNT
    # =====================================================

    def get_employee_count(
        self,
    ) -> dict[str, Any]:

        table = (
            self._resolve_table(
                self.EMPLOYEE_TABLES
            )
        )

        value = self._scalar(
            f"""
            SELECT COUNT(*)
            FROM [{table}]
            """
        )

        return {
            "value": value,
            "table": table,
            "filter": None,
        }

    # =====================================================
    # EMPLOYEE COUNT BY DEPARTMENT
    # =====================================================

    def get_employee_count_by_department(
        self,
        department: str,
    ) -> dict[str, Any]:

        requested_department = (
            str(
                department or ""
            ).strip()
        )

        if not requested_department:

            raise LiveQueryError(
                "Tên phòng ban không hợp lệ."
            )

        table = (
            self._resolve_table(
                self.EMPLOYEE_TABLES
            )
        )

        department_column = (
            self._resolve_column(
                table,
                self.EMPLOYEE_DEPARTMENT_COLUMNS,
            )
        )

        resolved_department = (
            self.resolve_department(
                requested_department
            )
        )

        # -------------------------------------------------
        # Không tìm thấy department tương ứng.
        # Trả 0 thay vì tự đoán bằng LIKE.
        # -------------------------------------------------

        if (
            resolved_department
            is None
        ):

            return {
                "value": 0,
                "table": table,
                "department_column": (
                    department_column
                ),
                "requested_department": (
                    requested_department
                ),
                "department": (
                    requested_department
                ),
                "resolved": False,
                "filter": {
                    "type": "department",
                    "value": (
                        requested_department
                    ),
                },
            }

        # -------------------------------------------------
        # Parameterized query.
        # User input không được nối trực tiếp vào SQL.
        # -------------------------------------------------

        value = self._scalar(
            f"""
            SELECT COUNT(*)
            FROM [{table}]
            WHERE
                LTRIM(
                    RTRIM(
                        [{department_column}]
                    )
                ) = ?
            """,
            (
                resolved_department,
            ),
        )

        return {
            "value": value,
            "table": table,
            "department_column": (
                department_column
            ),
            "requested_department": (
                requested_department
            ),
            "department": (
                resolved_department
            ),
            "resolved": True,
            "filter": {
                "type": "department",
                "value": (
                    resolved_department
                ),
            },
        }

    # =====================================================
    # CONTRACT COUNT
    # =====================================================

    def get_contract_count(
        self,
    ) -> dict[str, Any]:

        table = (
            self._resolve_table(
                self.CONTRACT_TABLES
            )
        )

        value = self._scalar(
            f"""
            SELECT COUNT(*)
            FROM [{table}]
            """
        )

        return {
            "value": value,
            "table": table,
            "filter": None,
        }

    # =====================================================
    # LEAVE COUNT
    # =====================================================

    def get_leave_count(
        self,
    ) -> dict[str, Any]:

        table = (
            self._resolve_table(
                self.LEAVE_TABLES
            )
        )

        value = self._scalar(
            f"""
            SELECT COUNT(*)
            FROM [{table}]
            """
        )

        return {
            "value": value,
            "table": table,
            "filter": None,
        }

    # =====================================================
    # ATTENDANCE TODAY
    # =====================================================

    def get_attendance_count_today(
        self,
    ) -> dict[str, Any]:

        table = (
            self._resolve_table(
                self.ATTENDANCE_TABLES
            )
        )

        date_column = (
            self._resolve_column(
                table,
                self.ATTENDANCE_DATE_COLUMNS,
            )
        )

        today = date.today()

        value = self._scalar(
            f"""
            SELECT COUNT(*)
            FROM [{table}]
            WHERE
                CAST(
                    [{date_column}]
                    AS date
                ) = ?
            """,
            (
                today,
            ),
        )

        return {
            "value": value,
            "table": table,
            "date_column": (
                date_column
            ),
            "date": (
                today.isoformat()
            ),
            "filter": {
                "type": "date",
                "value": (
                    today.isoformat()
                ),
            },
        }

    def count_late_employees(
        self,
        department: str | None,
        start_date: date,
        end_date: date,
    ) -> dict[str, Any]:
        """Count distinct late employees using a fixed, parameterized query."""
        if start_date > end_date:
            raise LiveQueryError("Khoảng ngày không hợp lệ.")

        attendance_table = self._resolve_table(self.ATTENDANCE_TABLES)
        employee_table = self._resolve_table(self.EMPLOYEE_TABLES)
        department_column = self._resolve_column(
            employee_table, self.EMPLOYEE_DEPARTMENT_COLUMNS
        )
        attendance_date_column = self._resolve_column(
            attendance_table, ["CheckIn", "CheckInTime", "AttendanceDate", "WorkDate"]
        )
        late_minutes_column = self._resolve_column(
            attendance_table, ["LateMinutes"]
        )
        attendance_employee_column = self._resolve_column(
            attendance_table, ["EmployeeId"]
        )
        employee_id_column = self._resolve_column(
            employee_table, ["Id"]
        )

        resolved_department = None
        if department:
            resolved_department = self.resolve_department(department)
            if resolved_department is None:
                return {
                    "value": 0,
                    "tool": "count_late_employees",
                    "requested_department": department,
                    "resolved": False,
                    "start_date": start_date.isoformat(),
                    "end_date": end_date.isoformat(),
                }

        sql = f"""
            SELECT COUNT(DISTINCT a.[{attendance_employee_column}])
            FROM [{attendance_table}] AS a
            INNER JOIN [{employee_table}] AS e
                ON e.[{employee_id_column}] = a.[{attendance_employee_column}]
            WHERE a.[{late_minutes_column}] > ?
              AND CAST(a.[{attendance_date_column}] AS date) BETWEEN ? AND ?
        """
        params: tuple[Any, ...] = (0, start_date, end_date)
        if resolved_department:
            sql += f" AND LTRIM(RTRIM(e.[{department_column}])) = ?"
            params += (resolved_department,)

        value = self._scalar(sql, params)
        return {
            "value": value,
            "tool": "count_late_employees",
            "department": resolved_department,
            "start_date": start_date.isoformat(),
            "end_date": end_date.isoformat(),
            "table": attendance_table,
            "date_column": attendance_date_column,
            "resolved": True,
        }
