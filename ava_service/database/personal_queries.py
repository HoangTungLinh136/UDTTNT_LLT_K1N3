from __future__ import annotations

from typing import Any

from database.connection import (
    get_sql_connection,
)


class PersonalQueryError(Exception):
    pass


class PersonalQueries:

    def get_late_count(
        self,
        employee_id: str,
        start_date,
        end_date,
    ) -> int:
        """Count late attendance rows scoped to the authenticated Employee.Id."""
        try:
            trusted_employee_id = int(employee_id)
        except (TypeError, ValueError) as exc:
            raise PersonalQueryError("Employee context is invalid.") from exc

        if start_date > end_date:
            raise PersonalQueryError("Date range is invalid.")

        with get_sql_connection() as conn:
            cursor = conn.cursor()
            cursor.execute(
                """
                SELECT COUNT(*)
                FROM Attendances
                WHERE EmployeeId = ?
                  AND LateMinutes > ?
                  AND CAST(CheckIn AS date) BETWEEN ? AND ?
                """,
                trusted_employee_id,
                0,
                start_date,
                end_date,
            )
            row = cursor.fetchone()
            return int(row[0] or 0) if row else 0

    # =====================================================
    # EMPLOYEE BY ID
    # =====================================================

    def get_employee(
        self,
        employee_id: str,
    ) -> dict[str, Any] | None:

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT TOP 1
                    Id,
                    EmployeeCode,
                    Name,
                    Department,
                    Position,
                    JobTitle,
                    Email,
                    CitizenId
                FROM Employees
                WHERE Id = ?
                """,
                employee_id,
            )

            row = cursor.fetchone()

            if row is None:
                return None

            return {
                "id": row.Id,
                "employee_code": (
                    row.EmployeeCode
                ),
                "name": row.Name,
                "department": (
                    row.Department
                ),
                "position": (
                    row.Position
                ),
                "job_title": (
                    row.JobTitle
                ),
                "email": row.Email,
                "citizen_id": row.CitizenId,
            }

    # =====================================================
    # EMPLOYEE BY CODE
    #
    # NV001 → employee record
    #
    # Chỉ dùng sau khi Authorization / Service Enforcement
    # đã kiểm tra quyền.
    # =====================================================

    def get_employee_by_code(
        self,
        employee_code: str,
    ) -> dict[str, Any] | None:

        employee_code = (
            str(
                employee_code
                or ""
            )
            .strip()
            .upper()
        )

        if not employee_code:

            raise PersonalQueryError(
                "Mã nhân viên không hợp lệ."
            )

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT TOP 1
                    Id,
                    EmployeeCode,
                    Name,
                    Department,
                    Position,
                    JobTitle,
                    Email,
                    CitizenId
                FROM Employees
                WHERE
                    UPPER(
                        LTRIM(
                            RTRIM(
                                EmployeeCode
                            )
                        )
                    ) = ?
                """,
                employee_code,
            )

            row = cursor.fetchone()

            if row is None:
                return None

            return {
                "id": row.Id,
                "employee_code": (
                    row.EmployeeCode
                ),
                "name": row.Name,
                "department": (
                    row.Department
                ),
                "position": (
                    row.Position
                ),
                "job_title": (
                    row.JobTitle
                ),
                "email": row.Email,
                "citizen_id": row.CitizenId,
            }

    # =====================================================
    # SALARY ROW MAPPER
    # =====================================================

    def _salary_to_dict(
        self,
        row,
    ) -> dict[str, Any]:

        return {
            "id": row.Id,
            "employee_id": (
                row.EmployeeId
            ),
            "employee_name": (
                row.EmployeeName
            ),
            "basic_salary": (
                row.BasicSalary
            ),
            "hours_worked": (
                row.HoursWorked
            ),
            "holiday_bonus": (
                row.HolidayBonus
            ),
            "diligence_bonus": (
                row.DiligenceBonus
            ),
            "reward": row.Reward,
            "discipline": (
                row.Discipline
            ),
            "total_salary": (
                row.TotalSalary
            ),
            "insurance": (
                row.Insurance
            ),
            "tax": row.TaxTNCN,
            "net_salary": (
                row.NetSalary
            ),
            "salary_month": (
                row.SalaryMonth
            ),
            "bonus_and_allowance": (
                row.BonusAndAllowance
            ),
        }

    # =====================================================
    # LATEST SALARY
    # =====================================================

    def get_latest_salary(
        self,
        employee_id: str,
    ) -> dict[str, Any] | None:

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT TOP 1
                    Id,
                    EmployeeId,
                    EmployeeNameSnapshot AS EmployeeName,
                    BasicSalary,
                    HoursWorked,
                    HolidayBonus,
                    DiligenceBonus,
                    Reward,
                    Discipline,
                    TotalSalary,
                    Insurance,
                    TaxTNCN,
                    NetSalary,
                    SalaryMonth,
                    BonusAndAllowance
                FROM Salaries
                WHERE EmployeeId = ?
                ORDER BY
                    SalaryMonth DESC,
                    Id DESC
                """,
                employee_id,
            )

            row = cursor.fetchone()

            if row is None:
                # PayrollPayments is the persisted payment snapshot used by
                # the HRMS employee payroll surface. Older databases may
                # contain payments before a Salaries snapshot was created.
                cursor.execute(
                    """
                    SELECT TOP 1
                        Id,
                        EmployeeId,
                        EmployeeName,
                        NetSalary,
                        PayrollPeriod,
                        CycleStartDate,
                        CreatedAt
                    FROM PayrollPayments
                    WHERE EmployeeId = ?
                    ORDER BY
                        CASE WHEN CycleStartDate > '1753-01-01'
                             THEN CycleStartDate
                             ELSE CreatedAt END DESC,
                        Id DESC
                    """,
                    employee_id,
                )
                payment = cursor.fetchone()
                if payment is None:
                    return None
                return self._payment_to_salary_dict(payment)

            return (
                self._salary_to_dict(
                    row
                )
            )

    # =====================================================
    # SALARY BY PERIOD
    # =====================================================

    def get_salary_by_period(
        self,
        employee_id: str,
        year: int,
        month: int,
    ) -> dict[str, Any] | None:

        if (
            month < 1
            or month > 12
        ):

            raise PersonalQueryError(
                "Tháng lương không hợp lệ."
            )

        if (
            year < 2000
            or year > 2100
        ):

            raise PersonalQueryError(
                "Năm lương không hợp lệ."
            )

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT TOP 1
                    Id,
                    EmployeeId,
                    EmployeeNameSnapshot AS EmployeeName,
                    BasicSalary,
                    HoursWorked,
                    HolidayBonus,
                    DiligenceBonus,
                    Reward,
                    Discipline,
                    TotalSalary,
                    Insurance,
                    TaxTNCN,
                    NetSalary,
                    SalaryMonth,
                    BonusAndAllowance
                FROM Salaries
                WHERE
                    EmployeeId = ?
                    AND YEAR(SalaryMonth) = ?
                    AND MONTH(SalaryMonth) = ?
                ORDER BY
                    SalaryMonth DESC,
                    Id DESC
                """,
                employee_id,
                year,
                month,
            )

            row = cursor.fetchone()

            if row is None:
                return None

            return (
                self._salary_to_dict(
                    row
                )
            )

    def _payment_to_salary_dict(self, row) -> dict[str, Any]:
        """Map a persisted PayrollPayment into the read-only salary shape.

        PayrollPayments intentionally stores the net-pay snapshot, while the
        optional Salaries row stores the detailed calculation. Missing detail
        remains ``None`` instead of being fabricated.
        """
        return {
            "id": row.Id,
            "employee_id": row.EmployeeId,
            "employee_name": row.EmployeeName,
            "basic_salary": None,
            "hours_worked": None,
            "holiday_bonus": None,
            "diligence_bonus": None,
            "reward": None,
            "discipline": None,
            "total_salary": None,
            "insurance": None,
            "tax": None,
            "net_salary": row.NetSalary,
            "salary_month": row.PayrollPeriod,
            "bonus_and_allowance": None,
            "record_type": "PayrollPayment",
            "created_at": row.CreatedAt,
        }

    # =====================================================
    # CONTRACT
    # =====================================================

    def get_latest_contract(
        self,
        employee_id: str,
    ) -> dict[str, Any] | None:

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            cursor.execute(
                """
                SELECT TOP 1
                    Id,
                    EmployeeNameSnapshot AS EmployeeName,
                    Duration,
                    StartDate,
                    EndDate,
                    Status,
                    ExtensionDate,
                    CreatedAt
                FROM Contracts
                WHERE EmployeeId = ?
                ORDER BY
                    CreatedAt DESC,
                    Id DESC
                """,
                employee_id,
            )

            row = cursor.fetchone()

            if row is None:
                return None

            return {
                "id": row.Id,
                "employee_name": (
                    row.EmployeeName
                ),
                "duration": (
                    row.Duration
                ),
                "start_date": (
                    row.StartDate
                ),
                "end_date": (
                    row.EndDate
                ),
                "status": row.Status,
                "extension_date": (
                    row.ExtensionDate
                ),
                "created_at": (
                    row.CreatedAt
                ),
            }

    # =====================================================
    # LEAVE REQUESTS
    # =====================================================

    def get_leave_requests(
        self,
        employee_id: str,
        limit: int = 10,
    ) -> list[dict[str, Any]]:

        limit = max(
            1,
            min(
                limit,
                50,
            ),
        )

        with get_sql_connection() as conn:

            cursor = conn.cursor()

            sql = f"""
                SELECT TOP {limit}
                    Id,
                    EmployeeNameSnapshot AS EmployeeName,
                    FromDate,
                    ToDate,
                    Reason,
                    Status,
                    CreatedDate
                FROM Leaves
                WHERE EmployeeId = ?
                ORDER BY
                    CreatedDate DESC,
                    Id DESC
            """

            cursor.execute(
                sql,
                employee_id,
            )

            rows = (
                cursor.fetchall()
            )

            return [
                {
                    "id": row.Id,
                    "employee_name": (
                        row.EmployeeName
                    ),
                    "from_date": (
                        row.FromDate
                    ),
                    "to_date": (
                        row.ToDate
                    ),
                    "reason": (
                        row.Reason
                    ),
                    "status": (
                        row.Status
                    ),
                    "created_date": (
                        row.CreatedDate
                    ),
                }
                for row
                in rows
            ]
