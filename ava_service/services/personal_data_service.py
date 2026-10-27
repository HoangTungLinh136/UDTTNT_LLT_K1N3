from __future__ import annotations

import re

from datetime import (
    date,
    datetime,
    timedelta,
)

from decimal import Decimal
from typing import Any

from core.enums import (
    AiRoute,
)

from core.models import (
    RouteResult,
    SourceRef,
    UserContext,
)

from database.personal_queries import (
    PersonalQueries,
    PersonalQueryError,
)


class PersonalDataService:

    def __init__(self):

        self.queries = (
            PersonalQueries()
        )

    # =====================================================
    # DISPLAY
    # =====================================================

    def _display(
        self,
        value: Any,
    ) -> str:

        if value is None:
            return "chưa có dữ liệu"

        if isinstance(
            value,
            Decimal,
        ):
            return str(value)

        if isinstance(
            value,
            datetime,
        ):
            return value.strftime(
                "%d/%m/%Y %H:%M"
            )

        if isinstance(
            value,
            date,
        ):
            return value.strftime(
                "%d/%m/%Y"
            )

        return str(value)

    # =====================================================
    # MONEY
    # =====================================================

    def _money(
        self,
        value: Any,
    ) -> str:

        if value is None:
            return "chưa có dữ liệu"

        try:

            number = float(
                value
            )

            return (
                f"{number:,.0f} VNĐ"
                .replace(
                    ",",
                    ".",
                )
            )

        except (
            TypeError,
            ValueError,
        ):

            return str(value)

    # =====================================================
    # PERMISSIONS
    # =====================================================

    def _permissions(
        self,
        user: UserContext,
    ) -> set[str]:

        return {
            str(permission)
            .strip()
            .lower()
            for permission
            in user.permissions
            if str(
                permission
            ).strip()
        }

    # =====================================================
    # EXTRACT EMPLOYEE CODE
    # =====================================================

    def _extract_employee_code(
        self,
        question: str,
    ) -> str | None:

        if not question:
            return None

        match = re.search(
            r"\bNV[\s-]*\d{3,}\b",
            question,
            re.IGNORECASE,
        )

        if not match:
            return None

        return re.sub(
            r"[\s-]+",
            "",
            match.group(0),
        ).upper()

    # =====================================================
    # RESOLVE TARGET EMPLOYEE
    #
    # Mặc định = chính user.
    #
    # Nếu câu hỏi có NVxxx:
    # - nếu đúng mã của user → own data
    # - nếu khác user → cần ai.personal.read.other
    #
    # Service kiểm tra lại để chống bypass Authorization.
    # =====================================================

    def _resolve_target_employee(
        self,
        question: str,
        user: UserContext,
        own_employee: dict[str, Any],
    ) -> tuple[
        dict[str, Any] | None,
        RouteResult | None,
        bool,
    ]:

        requested_code = (
            self._extract_employee_code(
                question
            )
        )

        # Không có mã NV cụ thể:
        # mặc định là chính user.
        if not requested_code:

            return (
                own_employee,
                None,
                True,
            )

        own_code = (
            str(
                own_employee.get(
                    "employee_code"
                )
                or
                getattr(
                    user,
                    "employee_code",
                    None,
                )
                or
                ""
            )
            .strip()
            .upper()
        )

        # User nhắc đúng mã của chính mình.
        if (
            own_code
            and
            requested_code
            == own_code
        ):

            return (
                own_employee,
                None,
                True,
            )

        permissions = (
            self._permissions(
                user
            )
        )

        # Không có quyền đọc người khác.
        if (
            "ai.personal.read.other"
            not in permissions
        ):

            return (
                None,
                RouteResult(
                    route=(
                        AiRoute.PERSONAL_DATA
                    ),
                    answer_draft=(
                        "Bạn không có quyền xem "
                        "dữ liệu cá nhân của "
                        "nhân viên khác."
                    ),
                    data={
                        "requested_employee_code":
                            requested_code,
                    },
                    warning=(
                        "CROSS_EMPLOYEE_ACCESS_DENIED"
                    ),
                    confidence=0.0,
                ),
                False,
            )

        # Có permission.
        # Resolve employee bằng DB phía server.
        target_employee = (
            self.queries
            .get_employee_by_code(
                requested_code
            )
        )

        if target_employee is None:

            return (
                None,
                RouteResult(
                    route=(
                        AiRoute.PERSONAL_DATA
                    ),
                    answer_draft=(
                        "Không tìm thấy nhân viên "
                        f"có mã {requested_code}."
                    ),
                    data={
                        "requested_employee_code":
                            requested_code,
                    },
                    warning=(
                        "TARGET_EMPLOYEE_NOT_FOUND"
                    ),
                    confidence=0.0,
                ),
                False,
            )

        return (
            target_employee,
            None,
            False,
        )

    # =====================================================
    # PREVIOUS MONTH
    # =====================================================

    def _previous_month(
        self,
        now: datetime,
    ) -> tuple[int, int]:

        first_day = datetime(
            now.year,
            now.month,
            1,
        )

        previous_day = (
            first_day
            - timedelta(
                days=1
            )
        )

        return (
            previous_day.year,
            previous_day.month,
        )

    # =====================================================
    # RESOLVE SALARY PERIOD
    # =====================================================

    def _resolve_salary_period(
        self,
        question: str,
    ) -> tuple[
        int,
        int,
        str,
    ] | None:

        if not question:
            return None

        q = (
            question
            .lower()
            .strip()
        )

        now = datetime.now()

        # -------------------------------------------------
        # CURRENT MONTH
        # -------------------------------------------------

        if any(
            phrase in q
            for phrase in [
                "tháng này",
                "tháng hiện tại",
                "tháng hiện nay",
            ]
        ):

            return (
                now.year,
                now.month,
                (
                    f"tháng "
                    f"{now.month:02d}/"
                    f"{now.year}"
                ),
            )

        # -------------------------------------------------
        # PREVIOUS MONTH
        # -------------------------------------------------

        if any(
            phrase in q
            for phrase in [
                "tháng trước",
                "tháng vừa rồi",
            ]
        ):

            year, month = (
                self._previous_month(
                    now
                )
            )

            return (
                year,
                month,
                (
                    f"tháng "
                    f"{month:02d}/"
                    f"{year}"
                ),
            )

        # -------------------------------------------------
        # MONTH + YEAR
        #
        # tháng 8/2026
        # tháng 8 năm 2026
        # -------------------------------------------------

        explicit = re.search(
            (
                r"\btháng\s*"
                r"(1[0-2]|0?[1-9])"
                r"(?:\s*(?:/|năm\s+)\s*"
                r"(20\d{2}))"
            ),
            q,
            re.IGNORECASE,
        )

        if explicit:

            month = int(
                explicit.group(1)
            )

            year = int(
                explicit.group(2)
            )

            return (
                year,
                month,
                (
                    f"tháng "
                    f"{month:02d}/"
                    f"{year}"
                ),
            )

        # -------------------------------------------------
        # MONTH ONLY
        # -------------------------------------------------

        month_only = re.search(
            (
                r"\btháng\s*"
                r"(1[0-2]|0?[1-9])\b"
            ),
            q,
            re.IGNORECASE,
        )

        if month_only:

            month = int(
                month_only.group(1)
            )

            year = now.year

            return (
                year,
                month,
                (
                    f"tháng "
                    f"{month:02d}/"
                    f"{year}"
                ),
            )

        return None

    # =====================================================
    # SALARY ANSWER
    # =====================================================

    def _build_salary_answer(
        self,
        employee_name: str,
        salary: dict[str, Any],
        period_label: str | None,
        is_own_data: bool,
    ) -> str:

        if is_own_data:

            owner = (
                "của bạn"
            )

        else:

            owner = (
                f"của "
                f"{employee_name}"
            )

        if period_label:

            title = (
                f"Thông tin lương "
                f"{period_label} "
                f"{owner}:"
            )

        else:

            title = (
                f"Thông tin lương gần nhất "
                f"{owner}:"
            )

        return (
            f"{title}\n"
            f"- Tháng lương: "
            f"{self._display(salary['salary_month'])}\n"
            f"- Lương cơ bản: "
            f"{self._money(salary['basic_salary'])}\n"
            f"- Tổng lương: "
            f"{self._money(salary['total_salary'])}\n"
            f"- Bảo hiểm: "
            f"{self._money(salary['insurance'])}\n"
            f"- Thuế TNCN: "
            f"{self._money(salary['tax'])}\n"
            f"- Lương thực nhận: "
            f"{self._money(salary['net_salary'])}"
        )

    # =====================================================
    # INTENT HELPERS INSIDE PERSONAL ROUTE
    # =====================================================

    def _is_salary_question(
        self,
        q: str,
    ) -> bool:

        return any(
            term in q
            for term in [
                "lương",
                "tiền lương",
                "thu nhập",
            ]
        )

    def _is_contract_question(
        self,
        q: str,
    ) -> bool:

        return any(
            term in q
            for term in [
                "hợp đồng",
                "hợp đồng lao động",
            ]
        )

    def _is_leave_question(
        self,
        q: str,
    ) -> bool:

        return any(
            term in q
            for term in [
                "nghỉ phép",
                "đơn nghỉ",
                "đơn nghỉ phép",
                "lịch sử nghỉ phép",
                "trạng thái đơn nghỉ",
            ]
        )

    def _is_profile_question(
        self,
        q: str,
    ) -> bool:

        return any(
            term in q
            for term in [
                "hồ sơ",
                "thông tin",
                "thông tin cá nhân",
            ]
        )

    @staticmethod
    def _is_late_count_question(q: str) -> bool:
        return (
            any(term in q for term in ("đi muộn", "đến muộn", "đi trễ"))
            and any(term in q for term in ("mấy lần", "bao nhiêu lần", "số lần"))
        )

    @staticmethod
    def _late_date_range(question: str) -> tuple[date, date]:
        today = date.today()
        q = question.lower()
        if "tháng trước" in q:
            first_this_month = today.replace(day=1)
            last_previous_month = first_this_month - timedelta(days=1)
            return last_previous_month.replace(day=1), last_previous_month
        if any(term in q for term in ("hôm qua", "ngày hôm qua")):
            yesterday = today - timedelta(days=1)
            return yesterday, yesterday
        if any(term in q for term in ("hôm nay", "ngày hôm nay")):
            return today, today
        return today.replace(day=1), today

    # =====================================================
    # ANSWER
    # =====================================================

    def answer(
        self,
        question: str,
        user: UserContext,
    ) -> RouteResult:

        # =================================================
        # 1. AUTHENTICATION
        # =================================================

        if not user.is_authenticated:

            return RouteResult(
                route=(
                    AiRoute.PERSONAL_DATA
                ),
                answer_draft=(
                    "Bạn cần đăng nhập để xem "
                    "dữ liệu cá nhân."
                ),
                warning=(
                    "AUTH_REQUIRED"
                ),
                confidence=0.0,
            )

        # =================================================
        # 2. BACKEND USER EMPLOYEE
        # =================================================

        if not user.employee_id:

            return RouteResult(
                route=(
                    AiRoute.PERSONAL_DATA
                ),
                answer_draft=(
                    "Không xác định được hồ sơ "
                    "nhân viên của tài khoản "
                    "hiện tại."
                ),
                warning=(
                    "EMPLOYEE_CONTEXT_MISSING"
                ),
                confidence=0.0,
            )

        try:

            own_employee = (
                self.queries
                .get_employee(
                    user.employee_id
                )
            )

            if own_employee is None:

                return RouteResult(
                    route=(
                        AiRoute.PERSONAL_DATA
                    ),
                    answer_draft=(
                        "Không tìm thấy hồ sơ nhân viên "
                        "gắn với tài khoản hiện tại."
                    ),
                    warning=(
                        "EMPLOYEE_NOT_FOUND"
                    ),
                    confidence=0.0,
                )

            # =================================================
            # 3. RESOLVE TARGET EMPLOYEE
            # =================================================

            (
                target_employee,
                target_error,
                is_own_data,
            ) = (
                self._resolve_target_employee(
                    question=question,
                    user=user,
                    own_employee=(
                        own_employee
                    ),
                )
            )

            if target_error is not None:
                return target_error

            if target_employee is None:

                return RouteResult(
                    route=(
                        AiRoute.PERSONAL_DATA
                    ),
                    answer_draft=(
                        "Không xác định được "
                        "nhân viên cần truy vấn."
                    ),
                    warning=(
                        "TARGET_EMPLOYEE_CONTEXT_MISSING"
                    ),
                    confidence=0.0,
                )

            target_employee_id = str(
                target_employee["id"]
            )

            employee_name = (
                target_employee["name"]
            )

            q = (
                question
                .lower()
                .strip()
            )

            queried_at = (
                datetime.now()
                .isoformat()
            )

            if self._is_late_count_question(q):
                start_date, end_date = self._late_date_range(question)
                late_count = self.queries.get_late_count(
                    str(target_employee["id"]),
                    start_date,
                    end_date,
                )
                return RouteResult(
                    route=AiRoute.PERSONAL_DATA,
                    answer_draft=f"Trong khoảng {start_date:%d/%m/%Y}–{end_date:%d/%m/%Y}, bạn đi muộn {late_count} lần.",
                    data={
                        "operation": "get_my_late_count" if is_own_data else "get_employee_late_count",
                        "tool": "get_my_late_count" if is_own_data else "get_employee_late_count",
                        "start_date": start_date.isoformat(),
                        "end_date": end_date.isoformat(),
                        "count": late_count,
                    },
                    confidence=1.0,
                )

            # =================================================
            # 4. SALARY
            # =================================================

            # P1-A2 SPECIFIC PERSONAL FIELDS

            # ---------------------------------------------------------
            # DEPARTMENT
            # ---------------------------------------------------------
            if any(
                marker in q
                for marker in (
                    "phòng ban",
                    "thuộc phòng",
                    "phòng nào",
                    "bộ phận nào",
                    "thuộc bộ phận",
                )
            ):
                department = target_employee.get(
                    "department"
                )

                if is_own_data:
                    answer = (
                        "Phòng ban hiện tại của bạn: "
                        f"{self._display(department)}."
                    )
                else:
                    answer = (
                        f"Phòng ban hiện tại của {employee_name}: "
                        f"{self._display(department)}."
                    )

                return RouteResult(
                    route=AiRoute.PERSONAL_DATA,
                    answer_draft=answer,
                    data={
                        "department": department,
                        "employee_id": target_employee_id,
                        "employee_code": target_employee.get(
                            "employee_code"
                        ),
                        "own_data": is_own_data,
                    },
                    confidence=1.0,
                )


            # ---------------------------------------------------------
            # POSITION / JOB TITLE
            # ---------------------------------------------------------
            if any(
                marker in q
                for marker in (
                    "chức vụ",
                    "chức danh",
                    "vị trí công việc",
                    "vị trí hiện tại",
                )
            ):
                position = target_employee.get(
                    "position"
                )

                job_title = target_employee.get(
                    "job_title"
                )

                if is_own_data:
                    answer = (
                        "Thông tin vị trí hiện tại của bạn:\n"
                        f"- Chức vụ: {self._display(position)}\n"
                        f"- Chức danh: {self._display(job_title)}"
                    )
                else:
                    answer = (
                        f"Thông tin vị trí hiện tại của {employee_name}:\n"
                        f"- Chức vụ: {self._display(position)}\n"
                        f"- Chức danh: {self._display(job_title)}"
                    )

                return RouteResult(
                    route=AiRoute.PERSONAL_DATA,
                    answer_draft=answer,
                    data={
                        "position": position,
                        "job_title": job_title,
                        "employee_id": target_employee_id,
                        "employee_code": target_employee.get(
                            "employee_code"
                        ),
                        "own_data": is_own_data,
                    },
                    confidence=1.0,
                )


            # ---------------------------------------------------------
            # OWN CITIZEN ID / CCCD
            #
            # Không mở field-level access cho employee khác ở đây.
            # P0-C vẫn chịu trách nhiệm permission.
            # ---------------------------------------------------------
            if any(
                marker in q
                for marker in (
                    "cccd",
                    "cmnd",
                    "căn cước",
                    "số căn cước",
                    "citizen id",
                )
            ):
                if not is_own_data:
                    return RouteResult(
                        route=AiRoute.PERSONAL_DATA,
                        answer_draft=(
                            "AVA chưa hỗ trợ truy xuất CCCD "
                            "của nhân viên khác qua kênh này."
                        ),
                        data={
                            "employee_id": target_employee_id,
                            "own_data": False,
                        },
                        warning="PERSONAL_FIELD_ACCESS_NOT_SUPPORTED",
                        confidence=0.0,
                    )

                citizen_id = target_employee.get(
                    "citizen_id"
                )

                if citizen_id:
                    answer = (
                        "CCCD của bạn: "
                        f"{citizen_id}."
                    )
                else:
                    answer = (
                        "Hiện chưa có dữ liệu CCCD "
                        "của bạn trong hệ thống."
                    )

                return RouteResult(
                    route=AiRoute.PERSONAL_DATA,
                    answer_draft=answer,
                    data={
                        "citizen_id": citizen_id,
                        "employee_id": target_employee_id,
                        "employee_code": target_employee.get(
                            "employee_code"
                        ),
                        "own_data": True,
                    },
                    confidence=(
                        1.0
                        if citizen_id
                        else 0.0
                    ),
                    warning=(
                        None
                        if citizen_id
                        else "PERSONAL_CITIZEN_ID_NOT_FOUND"
                    ),
                )


            # ---------------------------------------------------------
            # LEAVE BALANCE
            #
            # Schema hiện có Leaves (đơn nghỉ), nhưng không có
            # LeaveBalance / AnnualLeaves / quota phép năm.
            #
            # Vì vậy tuyệt đối không tự tính hoặc bịa số dư phép.
            # ---------------------------------------------------------
            if any(
                marker in q
                for marker in (
                    "còn bao nhiêu ngày phép",
                    "ngày phép còn lại",
                    "số ngày phép còn lại",
                    "phép còn lại",
                    "phép còn bao nhiêu",
                )
            ):
                return RouteResult(
                    route=AiRoute.PERSONAL_DATA,
                    answer_draft=(
                        "Hệ thống hiện có dữ liệu các đơn nghỉ phép, "
                        "nhưng chưa có dữ liệu hạn mức phép năm "
                        "để tính chính xác số ngày phép còn lại."
                    ),
                    data={
                        "leave_balance": None,
                        "balance_available": False,
                        "reason": "LEAVE_QUOTA_NOT_AVAILABLE",
                        "employee_id": target_employee_id,
                        "employee_code": target_employee.get(
                            "employee_code"
                        ),
                        "own_data": is_own_data,
                    },
                    warning="PERSONAL_LEAVE_BALANCE_UNAVAILABLE",
                    confidence=1.0,
                )

            if self._is_salary_question(
                q
            ):

                period = (
                    self._resolve_salary_period(
                        question
                    )
                )

                # ---------------------------------------------
                # SPECIFIC PERIOD
                # ---------------------------------------------

                if period:

                    (
                        salary_year,
                        salary_month,
                        period_label,
                    ) = period

                    salary = (
                        self.queries
                        .get_salary_by_period(
                            employee_id=(
                                target_employee_id
                            ),
                            year=(
                                salary_year
                            ),
                            month=(
                                salary_month
                            ),
                        )
                    )

                    operation = (
                        "GetMySalaryByPeriod"
                        if is_own_data
                        else
                        "GetEmployeeSalaryByPeriod"
                    )

                    if salary is None:

                        if is_own_data:

                            message = (
                                "Hiện chưa có dữ liệu "
                                f"lương {period_label} "
                                "của bạn trong hệ thống."
                            )

                        else:

                            message = (
                                "Hiện chưa có dữ liệu "
                                f"lương {period_label} "
                                f"của {employee_name} "
                                "trong hệ thống."
                            )

                        return RouteResult(
                            route=(
                                AiRoute.PERSONAL_DATA
                            ),
                            answer_draft=(
                                message
                            ),
                            data={
                                "employee_id": (
                                    target_employee_id
                                ),
                                "employee_code": (
                                    target_employee.get(
                                        "employee_code"
                                    )
                                ),
                                "year": (
                                    salary_year
                                ),
                                "month": (
                                    salary_month
                                ),
                                "period_label": (
                                    period_label
                                ),
                                "own_data": (
                                    is_own_data
                                ),
                            },
                            warning=(
                                "PERSONAL_SALARY_PERIOD_NOT_FOUND"
                            ),
                            confidence=0.0,
                        )

                    answer = (
                        self._build_salary_answer(
                            employee_name=(
                                employee_name
                            ),
                            salary=salary,
                            period_label=(
                                period_label
                            ),
                            is_own_data=(
                                is_own_data
                            ),
                        )
                    )

                    data = dict(
                        salary
                    )

                    data[
                        "requested_year"
                    ] = salary_year

                    data[
                        "requested_month"
                    ] = salary_month

                    data[
                        "period_label"
                    ] = period_label

                    data[
                        "own_data"
                    ] = is_own_data

                    data[
                        "target_employee_code"
                    ] = (
                        target_employee.get(
                            "employee_code"
                        )
                    )

                # ---------------------------------------------
                # LATEST SALARY
                # ---------------------------------------------

                else:

                    salary = (
                        self.queries
                        .get_latest_salary(
                            target_employee_id
                        )
                    )

                    if salary is None:

                        if is_own_data:

                            message = (
                                "Hiện chưa có dữ liệu "
                                "lương của bạn trong "
                                "hệ thống."
                            )

                        else:

                            message = (
                                "Hiện chưa có dữ liệu "
                                f"lương của "
                                f"{employee_name} "
                                "trong hệ thống."
                            )

                        return RouteResult(
                            route=(
                                AiRoute.PERSONAL_DATA
                            ),
                            answer_draft=(
                                message
                            ),
                            data={
                                "employee_id": (
                                    target_employee_id
                                ),
                                "employee_code": (
                                    target_employee.get(
                                        "employee_code"
                                    )
                                ),
                                "own_data": (
                                    is_own_data
                                ),
                            },
                            warning=(
                                "PERSONAL_SALARY_NOT_FOUND"
                            ),
                            confidence=0.0,
                        )

                    answer = (
                        self._build_salary_answer(
                            employee_name=(
                                employee_name
                            ),
                            salary=salary,
                            period_label=None,
                            is_own_data=(
                                is_own_data
                            ),
                        )
                    )

                    data = dict(
                        salary
                    )

                    data[
                        "own_data"
                    ] = is_own_data

                    data[
                        "target_employee_code"
                    ] = (
                        target_employee.get(
                            "employee_code"
                        )
                    )

                    operation = (
                        "GetMyLatestSalary"
                        if is_own_data
                        else
                        "GetEmployeeLatestSalary"
                    )

            # =================================================
            # 5. CONTRACT
            # =================================================

            elif self._is_contract_question(
                q
            ):

                contract = (
                    self.queries
                    .get_latest_contract(
                        target_employee_id
                    )
                )

                if contract is None:

                    if is_own_data:

                        message = (
                            "Hiện chưa tìm thấy "
                            "hợp đồng của bạn "
                            "trong hệ thống."
                        )

                    else:

                        message = (
                            "Hiện chưa tìm thấy "
                            f"hợp đồng của "
                            f"{employee_name} "
                            "trong hệ thống."
                        )

                    return RouteResult(
                        route=(
                            AiRoute.PERSONAL_DATA
                        ),
                        answer_draft=(
                            message
                        ),
                        data={
                            "employee_id": (
                                target_employee_id
                            ),
                            "employee_code": (
                                target_employee.get(
                                    "employee_code"
                                )
                            ),
                            "own_data": (
                                is_own_data
                            ),
                        },
                        warning=(
                            "PERSONAL_CONTRACT_NOT_FOUND"
                        ),
                        confidence=0.0,
                    )

                if is_own_data:

                    title = (
                        "Hợp đồng gần nhất "
                        "của bạn:"
                    )

                else:

                    title = (
                        "Hợp đồng gần nhất "
                        f"của {employee_name}:"
                    )

                answer = (
                    f"{title}\n"
                    f"- Trạng thái: "
                    f"{self._display(contract['status'])}\n"
                    f"- Thời hạn: "
                    f"{self._display(contract['duration'])}\n"
                    f"- Ngày bắt đầu: "
                    f"{self._display(contract['start_date'])}\n"
                    f"- Ngày kết thúc: "
                    f"{self._display(contract['end_date'])}"
                )

                data = dict(
                    contract
                )

                data[
                    "target_employee_id"
                ] = target_employee_id

                data[
                    "target_employee_code"
                ] = (
                    target_employee.get(
                        "employee_code"
                    )
                )

                data[
                    "own_data"
                ] = is_own_data

                operation = (
                    "GetMyLatestContract"
                    if is_own_data
                    else
                    "GetEmployeeLatestContract"
                )

            # =================================================
            # 6. LEAVE
            # =================================================

            elif self._is_leave_question(
                q
            ):

                leaves = (
                    self.queries
                    .get_leave_requests(
                        target_employee_id
                    )
                )

                if not leaves:

                    if is_own_data:

                        message = (
                            "Hiện bạn chưa có "
                            "đơn nghỉ phép nào "
                            "trong hệ thống."
                        )

                    else:

                        message = (
                            f"{employee_name} "
                            "hiện chưa có đơn "
                            "nghỉ phép nào trong "
                            "hệ thống."
                        )

                    return RouteResult(
                        route=(
                            AiRoute.PERSONAL_DATA
                        ),
                        answer_draft=(
                            message
                        ),
                        data={
                            "items": [],
                            "employee_id": (
                                target_employee_id
                            ),
                            "employee_code": (
                                target_employee.get(
                                    "employee_code"
                                )
                            ),
                            "own_data": (
                                is_own_data
                            ),
                        },
                        confidence=1.0,
                    )

                lines = [
                    (
                        f"- "
                        f"{self._display(item['from_date'])}"
                        f" → "
                        f"{self._display(item['to_date'])}: "
                        f"{self._display(item['status'])}"
                    )
                    for item
                    in leaves
                ]

                if is_own_data:

                    title = (
                        "Các đơn nghỉ gần nhất "
                        "của bạn:"
                    )

                else:

                    title = (
                        "Các đơn nghỉ gần nhất "
                        f"của {employee_name}:"
                    )

                answer = (
                    f"{title}\n"
                    + "\n".join(
                        lines
                    )
                )

                data = {
                    "items": leaves,
                    "target_employee_id": (
                        target_employee_id
                    ),
                    "target_employee_code": (
                        target_employee.get(
                            "employee_code"
                        )
                    ),
                    "own_data": (
                        is_own_data
                    ),
                }

                operation = (
                    "GetMyLeaveRequests"
                    if is_own_data
                    else
                    "GetEmployeeLeaveRequests"
                )

            # =================================================
            # 7. PROFILE
            # =================================================

            elif self._is_profile_question(
                q
            ):

                if is_own_data:

                    profile_title = (
                        "Thông tin hồ sơ "
                        "của bạn:"
                    )

                else:

                    profile_title = (
                        "Thông tin hồ sơ của "
                        f"{employee_name}:"
                    )

                answer = (
                    f"{profile_title}\n"
                    f"- Mã nhân viên: "
                    f"{self._display(target_employee['employee_code'])}\n"
                    f"- Họ tên: "
                    f"{self._display(target_employee['name'])}\n"
                    f"- Phòng ban: "
                    f"{self._display(target_employee['department'])}\n"
                    f"- Chức vụ: "
                    f"{self._display(target_employee['position'])}\n"
                    f"- Chức danh: "
                    f"{self._display(target_employee['job_title'])}\n"
                    f"- Email: "
                    f"{self._display(target_employee['email'])}"
                )

                data = dict(
                    target_employee
                )

                # P1-A GENERIC PROFILE PRIVACY
                # CCCD chỉ được trả ở nhánh hỏi trực tiếp.
                data.pop("citizen_id", None)

                data[
                    "own_data"
                ] = is_own_data

                operation = (
                    "GetMyProfile"
                    if is_own_data
                    else
                    "GetEmployeeProfile"
                )

            # =================================================
            # 8. NOT WHITELISTED
            # =================================================

            else:

                return RouteResult(
                    route=(
                        AiRoute.PERSONAL_DATA
                    ),
                    answer_draft=(
                        "Yêu cầu dữ liệu cá nhân "
                        "này chưa được hệ thống "
                        "hỗ trợ."
                    ),
                    warning=(
                        "PERSONAL_QUERY_NOT_WHITELISTED"
                    ),
                    confidence=0.0,
                )

            # =================================================
            # 9. SOURCE
            # =================================================

            source = SourceRef(
                source_type=(
                    "PERSONAL_DATA"
                ),
                source_name=(
                    f"HRMS Personal / "
                    f"{operation}"
                ),
                queried_at=(
                    queried_at
                ),
                metadata={
                    "operation": (
                        operation
                    ),
                    "target_employee_id": (
                        target_employee_id
                    ),
                    "target_employee_code": (
                        target_employee.get(
                            "employee_code"
                        )
                    ),
                    "own_data": (
                        is_own_data
                    ),
                },
            )

            return RouteResult(
                route=(
                    AiRoute.PERSONAL_DATA
                ),
                answer_draft=(
                    answer
                ),
                data=data,
                sources=[
                    source
                ],
                confidence=1.0,
            )

        # =====================================================
        # PERSONAL QUERY ERROR
        # =====================================================

        except PersonalQueryError as exc:

            return RouteResult(
                route=(
                    AiRoute.PERSONAL_DATA
                ),
                answer_draft=(
                    "Không thể xác định "
                    "dữ liệu cá nhân được "
                    "yêu cầu."
                ),
                data={
                    "error_type": (
                        "PERSONAL_QUERY_ERROR"
                    ),
                    "detail": str(
                        exc
                    ),
                },
                warning=(
                    "PERSONAL_QUERY_INVALID"
                ),
                confidence=0.0,
            )

        # =====================================================
        # OTHER ERROR
        # =====================================================

        except Exception as exc:

            return RouteResult(
                route=(
                    AiRoute.PERSONAL_DATA
                ),
                answer_draft=(
                    "Hiện tại không thể lấy "
                    "dữ liệu cá nhân từ "
                    "hệ thống HRMS."
                ),
                data={
                    "error_type": (
                        type(exc).__name__
                    ),
                },
                warning=(
                    "PERSONAL_DATA_FAILED"
                ),
                confidence=0.0,
            )
