from __future__ import annotations

import re
from datetime import date, datetime, timedelta
from typing import Any

import pyodbc

from config.settings import settings
from core.enums import AiRoute
from core.models import RouteResult, SourceRef
from database.connection import build_sql_connection_string

from services.live_sql_service_v17_base import (
    LiveSqlService as BaseLiveSqlService,
)


class LiveSqlService(BaseLiveSqlService):
    """
    V18 extension.

    Giữ toàn bộ LiveSqlService cũ và chỉ thêm:
      - GetEmployeeList
      - GetEmployeeListByDepartment

    Các query khác vẫn delegate về service gốc.
    """

    @staticmethod
    def _connection_string() -> str:
        return build_sql_connection_string(timeout=3)

    @staticmethod
    def _is_employee_list_question(
        question: str,
    ) -> bool:
        q = re.sub(
            r"\s+",
            " ",
            str(question or "").strip().lower(),
        )

        return (
            "nhân viên" in q
            and any(
                phrase in q
                for phrase in (
                    "danh sách nhân viên",
                    "liệt kê nhân viên",
                    "gồm những ai",
                    "những nhân viên nào",
                    "kể tên nhân viên",
                )
            )
        )

    @staticmethod
    def _extract_department(
        question: str,
    ) -> str | None:

        text = str(
            question or ""
        ).strip()

        if not text:
            return None

        text = re.sub(
            r"\s+",
            " ",
            text,
        )

        patterns = [
            # "thuộc phòng IT"
            (
                r"\bthuộc\s+"
                r"(?:phòng|bộ phận)\s+"
                r"(.+?)"
                r"(?=\s+(?:"
                r"có|bao nhiêu|mấy|"
                r"đi muộn|đến muộn|"
                r"đúng không|có đúng không|"
                r"phải không|có phải"
                r")|[?.!,]|$)"
            ),

            # "Phòng IT có bao nhiêu..."
            # "Phòng IT có 20 người đúng không?"
            (
                r"\b(?:phòng|bộ phận)\s+"
                r"(.+?)"
                r"(?=\s+(?:"
                r"có|bao nhiêu|mấy|"
                r"đi muộn|đến muộn|"
                r"đúng không|có đúng không|"
                r"phải không|có phải"
                r")|[?.!,]|$)"
            ),
        ]

        for pattern in patterns:

            match = re.search(
                pattern,
                text,
                re.IGNORECASE,
            )

            if not match:
                continue

            department = (
                match.group(1)
                .strip()
                .rstrip("?.!,")
            )

            department = re.sub(
                r"\s+",
                " ",
                department,
            ).strip()

            if (
                not department
                or department.lower()
                in {
                    "đó",
                    "này",
                    "kia",
                }
            ):
                return None

            return department

        return None

    def _employee_list(
        self,
        question: str,
    ) -> RouteResult:
        department = self._extract_department(
            question
        )

        conn = None
        cursor = None
        queried_at = datetime.now().isoformat()

        try:
            conn = pyodbc.connect(
                self._connection_string()
            )

            cursor = conn.cursor()

            if department:
                cursor.execute(
                    """
                    SELECT TOP (100)
                        EmployeeCode,
                        Name,
                        Department,
                        JobTitle
                    FROM Employees
                    WHERE
                        Department = ?
                        OR Department LIKE ?
                    ORDER BY EmployeeCode, Name
                    """,
                    department,
                    f"%{department}%",
                )

                operation = (
                    "GetEmployeeListByDepartment"
                )
            else:
                cursor.execute(
                    """
                    SELECT TOP (100)
                        EmployeeCode,
                        Name,
                        Department,
                        JobTitle
                    FROM Employees
                    ORDER BY EmployeeCode, Name
                    """
                )

                operation = "GetEmployeeList"

            rows = cursor.fetchall()

            items: list[dict[str, Any]] = []

            for row in rows:
                items.append(
                    {
                        "employee_code": (
                            str(row[0]).strip()
                            if row[0] is not None
                            else None
                        ),
                        "name": (
                            str(row[1]).strip()
                            if row[1] is not None
                            else None
                        ),
                        "department": (
                            str(row[2]).strip()
                            if row[2] is not None
                            else None
                        ),
                        "job_title": (
                            str(row[3]).strip()
                            if row[3] is not None
                            else None
                        ),
                    }
                )

            if department:
                heading = (
                    "Danh sách nhân viên "
                    f"thuộc phòng {department}"
                )
            else:
                heading = (
                    "Danh sách nhân viên "
                    "hiện tại"
                )

            if not items:
                answer = (
                    f"{heading}: hiện không có "
                    "nhân viên phù hợp."
                )
            else:
                lines = []

                for item in items:
                    code = (
                        item["employee_code"]
                        or "—"
                    )
                    name = (
                        item["name"]
                        or "Chưa có tên"
                    )

                    extra = []

                    if item["department"]:
                        extra.append(
                            item["department"]
                        )

                    if item["job_title"]:
                        extra.append(
                            item["job_title"]
                        )

                    suffix = (
                        " — " + " — ".join(extra)
                        if extra
                        else ""
                    )

                    lines.append(
                        f"- {code} — {name}{suffix}"
                    )

                answer = (
                    f"{heading} ({len(items)} người):\n"
                    + "\n".join(lines)
                )

            source = SourceRef(
                source_type="LIVE_SQL",
                source_name=(
                    f"HRMS SQL / {operation}"
                ),
                queried_at=queried_at,
                metadata={
                    "operation": operation,
                    "department": department,
                },
            )

            return RouteResult(
                route=AiRoute.LIVE_SQL,
                answer_draft=answer,
                data={
                    "operation": operation,
                    "count": len(items),
                    "items": items,
                    "department": department,
                    "queried_at": queried_at,
                },
                sources=[source],
                confidence=1.0,
            )

        except Exception as exc:
            return RouteResult(
                route=AiRoute.LIVE_SQL,
                answer_draft=(
                    "Hiện tại không thể lấy "
                    "danh sách nhân viên từ HRMS."
                ),
                data={
                    "operation": (
                        "GetEmployeeList"
                    ),
                    "error_type": (
                        type(exc).__name__
                    ),
                },
                warning="LIVE_SQL_FAILED",
                confidence=0.0,
            )

        finally:
            if cursor:
                try:
                    cursor.close()
                except Exception:
                    pass

            if conn:
                try:
                    conn.close()
                except Exception:
                    pass

    def answer(
        self,
        question: str,
    ) -> RouteResult:
        normalized = re.sub(r"\s+", " ", str(question or "").strip().lower())

        # P1-B3 PENDING LEAVE THIS MONTH
        is_leave_count = any(
            marker in normalized
            for marker in (
                "bao nhiêu đơn nghỉ",
                "bao nhiêu nghỉ phép",
                "số đơn nghỉ",
                "số đơn nghỉ phép",
            )
        )

        is_pending_leave = any(
            marker in normalized
            for marker in (
                "chờ duyệt",
                "pending",
            )
        )

        is_current_month = any(
            marker in normalized
            for marker in (
                "tháng này",
                "tháng hiện tại",
                "tháng hiện nay",
            )
        )

        if (
            is_leave_count
            and is_pending_leave
            and is_current_month
        ):
            from database.connection import (
                get_sql_connection,
            )

            today = date.today()

            month_start = today.replace(
                day=1
            )

            if month_start.month == 12:
                next_month = date(
                    month_start.year + 1,
                    1,
                    1,
                )
            else:
                next_month = date(
                    month_start.year,
                    month_start.month + 1,
                    1,
                )

            with get_sql_connection() as conn:
                cursor = conn.cursor()

                cursor.execute(
                    """
                    SELECT COUNT(*)
                    FROM Leaves
                    WHERE
                        CreatedDate >= ?
                        AND CreatedDate < ?
                        AND (
                            Status = ?
                            OR Status = ?
                        )
                    """,
                    month_start,
                    next_month,
                    "Chờ duyệt",
                    "PENDING",
                )

                row = cursor.fetchone()

            count = (
                int(row[0] or 0)
                if row
                else 0
            )

            queried_at = (
                datetime.now()
                .isoformat()
            )

            query_result = {
                "value": count,
                "table": "Leaves",
                "filter": {
                    "period": "current_month",
                    "start_date": (
                        month_start.isoformat()
                    ),
                    "end_date_exclusive": (
                        next_month.isoformat()
                    ),
                    "statuses": [
                        "Chờ duyệt",
                        "PENDING",
                    ],
                },
            }

            return RouteResult(
                route=AiRoute.LIVE_SQL,
                answer_draft=(
                    f"Tháng này có {count} "
                    "đơn nghỉ phép đang chờ duyệt."
                ),
                data={
                    "operation": (
                        "GetPendingLeaveCountThisMonth"
                    ),
                    "value": count,
                    "query_result": query_result,
                    "queried_at": queried_at,
                },
                confidence=1.0,
            )


        # P1-B2 VERIFY EMPLOYEE COUNT
        verify_match = re.search(
            (
                r"\b(?:phòng|bộ phận)\s+"
                r"(.+?)\s+có\s+"
                r"(\d+)\s+"
                r"(?:người|nhân viên)"
                r"\s*(?:"
                r"đúng không|"
                r"có đúng không|"
                r"phải không|"
                r"có phải không"
                r")"
                r"[?.!]*$"
            ),
            normalized,
            re.IGNORECASE,
        )

        if verify_match:
            department = self._extract_department(
                question
            )

            expected_value = int(
                verify_match.group(2)
            )

            if not department:
                return RouteResult(
                    route=AiRoute.LIVE_SQL,
                    answer_draft=(
                        "Không xác định được phòng ban "
                        "cần kiểm tra."
                    ),
                    warning=(
                        "LIVE_QUERY_DEPARTMENT_REQUIRED"
                    ),
                    confidence=0.0,
                )

            base_result = super().answer(
                (
                    "Hiện có bao nhiêu nhân viên "
                    f"thuộc phòng {department}?"
                )
            )

            if (
                base_result.warning
                or not isinstance(
                    base_result.data,
                    dict,
                )
                or "value"
                not in base_result.data
            ):
                return base_result

            actual_value = int(
                base_result.data["value"]
            )

            matches = (
                actual_value
                == expected_value
            )

            if matches:
                answer = (
                    f"Đúng. Hiện phòng {department} "
                    f"có {actual_value} nhân viên."
                )
            else:
                answer = (
                    f"Không. Hiện phòng {department} "
                    f"có {actual_value} nhân viên, "
                    f"không phải {expected_value}."
                )

            data = dict(
                base_result.data
            )

            data.update(
                {
                    "operation": (
                        "VerifyEmployeeCountByDepartment"
                    ),
                    "department": department,
                    "expected_value": expected_value,
                    "actual_value": actual_value,
                    "matches": matches,
                }
            )

            return RouteResult(
                route=AiRoute.LIVE_SQL,
                answer_draft=answer,
                data=data,
                sources=base_result.sources,
                confidence=1.0,
            )

        if "đi muộn" in normalized and any(
            marker in normalized
            for marker in ("bao nhiêu nhân viên", "số nhân viên", "bao nhiêu người")
        ):
            if "hôm qua" in normalized:
                start_date = end_date = date.today() - timedelta(days=1)
                period_label = "hôm qua"
            elif "hôm nay" in normalized:
                start_date = end_date = date.today()
                period_label = "hôm nay"
            else:
                return RouteResult(
                    route=AiRoute.LIVE_SQL,
                    answer_draft="Vui lòng nêu rõ khoảng thời gian cần thống kê.",
                    warning="LIVE_QUERY_DATE_REQUIRED",
                    confidence=0.0,
                )

            department_match = re.search(
                r"\b(?:phòng|bộ phận)\s+(.+?)(?=\s+(?:có|bao nhiêu|đi muộn|đến muộn)|[?.!,]|$)",
                str(question or ""),
                re.IGNORECASE,
            )
            department = department_match.group(1).strip() if department_match else self._extract_department(question)
            result = self.queries.count_late_employees(
                department=department,
                start_date=start_date,
                end_date=end_date,
            )
            count = int(result.get("value", 0))
            scope = f"phòng {department}" if department else "toàn công ty"
            return RouteResult(
                route=AiRoute.LIVE_SQL,
                answer_draft=f"{period_label.capitalize()} {scope} có {count} nhân viên đi muộn.",
                data={
                    **result,
                    "operation": "count_late_employees",
                    "tool": "count_late_employees",
                },
                sources=[
                    self._build_source(
                        result=result,
                        operation="count_late_employees",
                        queried_at=datetime.now().isoformat(),
                    )
                ],
                confidence=1.0,
            )

        if self._is_employee_list_question(
            question
        ):
            return self._employee_list(
                question
            )

        return super().answer(
            question
        )
