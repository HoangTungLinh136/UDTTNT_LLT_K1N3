from __future__ import annotations

import re

from datetime import datetime

from core.enums import (
    AiRoute,
)

from core.models import (
    RouteResult,
    SourceRef,
)

from database.live_queries import (
    LiveQueries,
    LiveQueryError,
)


class LiveSqlService:

    def __init__(self):

        self.queries = (
            LiveQueries()
        )

    # =====================================================
    # NORMALIZE
    # =====================================================

    def _normalize(
        self,
        text: str,
    ) -> str:

        return (
            text
            .strip()
        )

    # =====================================================
    # EXTRACT DEPARTMENT
    # =====================================================

    def _extract_department(
        self,
        question: str,
    ) -> str | None:

        if not question:
            return None

        text = (
            self._normalize(
                question
            )
        )

        # =================================================
        # ContextResolver hiện rewrite về dạng:
        #
        # "Hiện có bao nhiêu nhân viên thuộc phòng IT?"
        #
        # nên pattern ưu tiên này xử lý chính xác nhất.
        # =================================================

        patterns = [
            (
                r"thuộc\s+phòng\s+"
                r"([A-Za-zÀ-ỹ0-9 _-]+)"
                r"(?:\?|$)"
            ),
            (
                r"thuộc\s+bộ\s+phận\s+"
                r"([A-Za-zÀ-ỹ0-9 _-]+)"
                r"(?:\?|$)"
            ),
            (
                r"\bphòng\s+"
                r"([A-Za-zÀ-ỹ0-9 _-]+)"
                r"(?:\?|$)"
            ),
            (
                r"\bbộ\s+phận\s+"
                r"([A-Za-zÀ-ỹ0-9 _-]+)"
                r"(?:\?|$)"
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
                .rstrip(
                    "?.!,"
                )
            )

            # -----------------------------------------
            # Xóa phần diễn đạt dư nếu có.
            # -----------------------------------------

            department = re.sub(
                (
                    r"\s+"
                    r"(thì sao|"
                    r"thế nào|"
                    r"có bao nhiêu.*|"
                    r"bao nhiêu.*)"
                    r"$"
                ),
                "",
                department,
                flags=re.IGNORECASE,
            ).strip()

            invalid = {
                "",
                "đó",
                "ấy",
                "này",
                "kia",
            }

            if (
                department.lower()
                in invalid
            ):
                return None

            return department

        return None

    # =====================================================
    # SOURCE
    # =====================================================

    def _build_source(
        self,
        result: dict,
        operation: str,
        queried_at: str,
    ) -> SourceRef:

        metadata = {
            "table": result.get(
                "table"
            ),
            "operation": operation,
        }

        department = (
            result.get(
                "department"
            )
        )

        if department:

            metadata[
                "department"
            ] = department

            metadata[
                "department_column"
            ] = result.get(
                "department_column"
            )

        if result.get(
            "date"
        ):

            metadata[
                "date"
            ] = result.get(
                "date"
            )

        return SourceRef(
            source_type="LIVE_SQL",
            source_name=(
                f"HRMS SQL / {operation}"
            ),
            queried_at=queried_at,
            metadata=metadata,
        )

    # =====================================================
    # ANSWER
    # =====================================================

    def answer(
        self,
        question: str,
    ) -> RouteResult:

        q = (
            question
            .lower()
            .strip()
        )

        queried_at = (
            datetime.now()
            .isoformat()
        )

        try:

            # =================================================
            # 1. EMPLOYEE COUNT
            # =================================================

            if any(
                phrase in q
                for phrase in [
                    "bao nhiêu nhân viên",
                    "số nhân viên",
                    "tổng nhân viên",
                    "hiện có bao nhiêu nhân viên",
                ]
            ):

                department = (
                    self._extract_department(
                        question
                    )
                )

                # ---------------------------------------------
                # COUNT BY DEPARTMENT
                # ---------------------------------------------

                if department:

                    result = (
                        self.queries
                        .get_employee_count_by_department(
                            department
                        )
                    )

                    value = (
                        result["value"]
                    )

                    answer = (
                        f"Hiện hệ thống có "
                        f"{value} nhân viên "
                        f"thuộc phòng "
                        f"{department}."
                    )

                    operation = (
                        "GetEmployeeCountByDepartment"
                    )

                # ---------------------------------------------
                # TOTAL COUNT
                # ---------------------------------------------

                else:

                    result = (
                        self.queries
                        .get_employee_count()
                    )

                    value = (
                        result["value"]
                    )

                    answer = (
                        f"Hiện hệ thống có "
                        f"{value} nhân viên."
                    )

                    operation = (
                        "GetEmployeeCount"
                    )

            # =================================================
            # 2. CONTRACT COUNT
            # =================================================

            elif any(
                phrase in q
                for phrase in [
                    "bao nhiêu hợp đồng",
                    "số hợp đồng",
                    "tổng hợp đồng",
                ]
            ):

                # Chưa whitelist hợp đồng theo phòng
                # vì schema Contracts hiện không có
                # EmployeeId/Department đáng tin cậy.

                department = (
                    self._extract_department(
                        question
                    )
                )

                if department:

                    return RouteResult(
                        route=(
                            AiRoute.LIVE_SQL
                        ),
                        answer_draft=(
                            "Hiện tại hệ thống chưa "
                            "hỗ trợ truy vấn số hợp đồng "
                            "theo phòng ban."
                        ),
                        data={
                            "department": (
                                department
                            ),
                            "operation": (
                                "GetContractCountByDepartment"
                            ),
                        },
                        warning=(
                            "LIVE_QUERY_NOT_WHITELISTED"
                        ),
                        confidence=0.0,
                    )

                result = (
                    self.queries
                    .get_contract_count()
                )

                value = (
                    result["value"]
                )

                answer = (
                    f"Hiện hệ thống có "
                    f"{value} hợp đồng."
                )

                operation = (
                    "GetContractCount"
                )

            # =================================================
            # 3. LEAVE COUNT
            # =================================================

            elif any(
                phrase in q
                for phrase in [
                    "bao nhiêu đơn nghỉ",
                    "bao nhiêu nghỉ phép",
                    "số đơn nghỉ",
                    "số đơn nghỉ phép",
                ]
            ):

                result = (
                    self.queries
                    .get_leave_count()
                )

                value = (
                    result["value"]
                )

                answer = (
                    f"Hiện hệ thống có "
                    f"{value} bản ghi nghỉ phép."
                )

                operation = (
                    "GetLeaveCount"
                )

            # =================================================
            # 4. ATTENDANCE TODAY
            # =================================================

            elif any(
                phrase in q
                for phrase in [
                    "chấm công hôm nay",
                    "lượt chấm công hôm nay",
                    (
                        "bao nhiêu người "
                        "chấm công hôm nay"
                    ),
                    (
                        "bao nhiêu lượt "
                        "chấm công hôm nay"
                    ),
                ]
            ):

                result = (
                    self.queries
                    .get_attendance_count_today()
                )

                value = (
                    result["value"]
                )

                answer = (
                    f"Hôm nay hệ thống ghi nhận "
                    f"{value} bản ghi chấm công."
                )

                operation = (
                    "GetAttendanceCountToday"
                )

            # =================================================
            # NOT WHITELISTED
            # =================================================

            else:

                return RouteResult(
                    route=(
                        AiRoute.LIVE_SQL
                    ),
                    answer_draft=(
                        "Yêu cầu này chưa có "
                        "truy vấn Live SQL "
                        "được phép."
                    ),
                    data=None,
                    warning=(
                        "LIVE_QUERY_NOT_WHITELISTED"
                    ),
                    confidence=0.0,
                )

            # =================================================
            # STRUCTURED RESULT
            # =================================================

            structured_data = {
                "operation": operation,
                "value": value,
                "query_result": result,
                "queried_at": queried_at,
            }

            source = (
                self._build_source(
                    result=result,
                    operation=operation,
                    queried_at=queried_at,
                )
            )

            return RouteResult(
                route=(
                    AiRoute.LIVE_SQL
                ),
                answer_draft=answer,
                data=structured_data,
                sources=[
                    source
                ],
                confidence=1.0,
            )

        # =====================================================
        # SCHEMA ERROR
        # =====================================================

        except LiveQueryError as exc:

            return RouteResult(
                route=(
                    AiRoute.LIVE_SQL
                ),
                answer_draft=(
                    "Hệ thống chưa xác định "
                    "được bảng hoặc cột phù hợp "
                    "để lấy dữ liệu."
                ),
                data={
                    "error_type": (
                        "SCHEMA_NOT_SUPPORTED"
                    ),
                    "detail": str(
                        exc
                    ),
                },
                warning=(
                    "LIVE_SQL_SCHEMA_NOT_SUPPORTED"
                ),
                confidence=0.0,
            )

        # =====================================================
        # OTHER ERROR
        # =====================================================

        except Exception as exc:

            return RouteResult(
                route=(
                    AiRoute.LIVE_SQL
                ),
                answer_draft=(
                    "Hiện tại không thể "
                    "lấy dữ liệu trực tiếp "
                    "từ hệ thống HRMS."
                ),
                data={
                    "error_type": (
                        type(exc).__name__
                    ),
                },
                warning=(
                    "LIVE_SQL_FAILED"
                ),
                confidence=0.0,
            )