from __future__ import annotations

import re
import unicodedata

from core.enums import (
    AiRoute,
    GuardrailDecision,
)

from core.models import (
    SafetyResult,
    UserContext,
)


class AuthorizationService:

    # =====================================================
    # NORMALIZATION
    # =====================================================

    def _normalized_roles(
        self,
        user: UserContext,
    ) -> set[str]:

        return {
            str(role)
            .strip()
            .upper()
            for role in user.roles
            if str(role).strip()
        }

    def _normalized_permissions(
        self,
        user: UserContext,
    ) -> set[str]:

        return {
            str(permission)
            .strip()
            .lower()
            for permission in user.permissions
            if str(permission).strip()
        }

    def _normalize_text(
        self,
        value: str | None,
    ) -> str:

        text = unicodedata.normalize(
            "NFC",
            str(value or ""),
        )

        text = text.strip().lower()

        text = re.sub(
            r"\s+",
            " ",
            text,
        )

        return text

    # =====================================================
    # DEPARTMENT NORMALIZATION
    #
    # "Phòng IT" → "it"
    # "IT"        → "it"
    #
    # "Phòng nhân sự" → "nhân sự"
    # =====================================================

    def _normalize_department(
        self,
        value: str | None,
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
    # EMPLOYEE CODE
    #
    # NV001
    # NV123
    # NV-001
    # =====================================================

    def _extract_employee_codes(
        self,
        question: str | None,
    ) -> set[str]:

        if not question:
            return set()

        matches = re.findall(
            r"\bNV[\s-]*\d{3,}\b",
            question,
            re.IGNORECASE,
        )

        result: set[str] = set()

        for value in matches:

            normalized = re.sub(
                r"[\s-]+",
                "",
                value,
            ).upper()

            result.add(
                normalized
            )

        return result

    # =====================================================
    # DEPARTMENT FROM STANDALONE QUESTION
    #
    # ContextResolver hiện rewrite:
    #
    # Hiện có bao nhiêu nhân viên thuộc phòng IT?
    # =====================================================

    def _extract_department(
        self,
        question: str | None,
    ) -> str | None:

        if not question:
            return None

        text = str(
            question
        ).strip()

        patterns = [
            (
                r"thuộc\s+phòng\s+"
                r"([A-Za-zÀ-ỹ0-9 _-]+?)"
                r"(?:\?|\.|,|$)"
            ),
            (
                r"thuộc\s+bộ\s+phận\s+"
                r"([A-Za-zÀ-ỹ0-9 _-]+?)"
                r"(?:\?|\.|,|$)"
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

            value = (
                match.group(1)
                .strip()
            )

            value = re.sub(
                (
                    r"\s+"
                    r"(thì sao|"
                    r"thế nào|"
                    r"có bao nhiêu.*)"
                    r"$"
                ),
                "",
                value,
                flags=re.IGNORECASE,
            ).strip()

            if value:
                return value

        return None

    # =====================================================
    # FULL LIVE ACCESS
    # =====================================================

    def _has_full_live_access(
        self,
        user: UserContext,
    ) -> bool:

        roles = (
            self._normalized_roles(
                user
            )
        )

        permissions = (
            self._normalized_permissions(
                user
            )
        )

        # ai.live.read được giữ lại để tương thích
        # với hệ thống hiện tại.
        if (
            "ai.live.read"
            in permissions
        ):
            return True

        if (
            "ai.live.read.all"
            in permissions
        ):
            return True

        # Baseline:
        # HR/Admin có thể đọc aggregate toàn hệ thống.
        if roles.intersection(
            {
                "ADMIN",
                "HR",
                "BAN_LANH_DAO",
            }
        ):
            return True

        return False

    # =====================================================
    # DEPARTMENT-SCOPED LIVE ACCESS
    # =====================================================

    def _has_department_live_access(
        self,
        user: UserContext,
    ) -> bool:

        roles = (
            self._normalized_roles(
                user
            )
        )

        permissions = (
            self._normalized_permissions(
                user
            )
        )

        if (
            "ai.live.read.department"
            in permissions
        ):
            return True

        if roles.intersection(
            {
                "LEADER",
                "MANAGER",
            }
        ):
            return True

        return False

    # =====================================================
    # CHECK DEPARTMENT OWNERSHIP
    # =====================================================

    def _department_allowed(
        self,
        user: UserContext,
        requested_department: str,
    ) -> bool:

        requested_key = (
            self._normalize_department(
                requested_department
            )
        )

        if not requested_key:
            return False

        allowed_keys = {
            self._normalize_department(
                department
            )
            for department
            in user.department_names
            if self._normalize_department(
                department
            )
        }

        return (
            requested_key
            in allowed_keys
        )

    # =====================================================
    # PROTECTED PERSONAL-DATA SCOPE
    # =====================================================

    def _is_bulk_personal_request(
        self,
        question: str | None,
    ) -> bool:

        q = self._normalize_text(
            question
        )

        return any(
            marker in q
            for marker in (
                "tất cả nhân viên",
                "toàn bộ nhân viên",
                "mọi nhân viên",
                "danh sách nhân viên",
                "danh sách lương",
            )
        )

    def _has_personal_subject(
        self,
        question: str | None,
    ) -> bool:

        q = self._normalize_text(
            question
        )

        return any(
            marker in q
            for marker in (
                "lương",
                "tiền lương",
                "thu nhập",
                "cccd",
                "cmnd",
                "căn cước",
                "hợp đồng",
                "ngày phép",
                "hồ sơ",
                "thông tin",
            )
        )

    def _looks_like_named_other_target(
        self,
        question: str | None,
    ) -> bool:

        q = self._normalize_text(
            question
        )

        if not q:
            return False

        if not self._has_personal_subject(
            question
        ):
            return False

        # Own-data references are not cross-employee.
        if any(
            marker in q
            for marker in (
                "của tôi",
                "của mình",
            )
        ):
            return False

        # Explicit employee codes are handled by the existing
        # trusted employee-code authorization path below.
        if self._extract_employee_codes(
            question
        ):
            return False

        if self._is_bulk_personal_request(
            question
        ):
            return False

        # At this point the classifier has already selected
        # PERSONAL_DATA. A "của <target>" reference which is
        # not self / code / bulk must not silently fall back
        # to the authenticated user's own EmployeeId.
        return (
            "của " in q
            or "nhân viên " in q
        )

    def _is_scoped_personal_request(
        self,
        question: str | None,
    ) -> bool:

        if not self._has_personal_subject(
            question
        ):
            return False

        q = self._normalize_text(
            question
        )

        if any(
            marker in q
            for marker in (
                "của tôi",
                "của mình",
            )
        ):
            return True

        if self._extract_employee_codes(
            question
        ):
            return True

        if self._is_bulk_personal_request(
            question
        ):
            return True

        # Proper-name reference, used only as a defensive
        # GENERAL_LLM mismatch detector.
        raw = str(
            question
            or ""
        )

        return (
            re.search(
                (
                    r"\b(?:của|nhân viên)\s+"
                    r"[A-ZÀ-ỴĐ]"
                    r"[A-Za-zÀ-ỹĐđ'’-]+"
                    r"(?:\s+[A-ZÀ-ỴĐ]"
                    r"[A-Za-zÀ-ỹĐđ'’-]+){1,4}\b"
                ),
                raw,
            )
            is not None
        )

    # =====================================================
    # CROSS-EMPLOYEE ACCESS
    # =====================================================

    def _authorize_employee_reference(
        self,
        user: UserContext,
        question: str | None,
    ) -> SafetyResult | None:

        employee_codes = (
            self._extract_employee_codes(
                question
            )
        )

        # Không có explicit employee code.
        if not employee_codes:

            return None

        permissions = (
            self._normalized_permissions(
                user
            )
        )

        # Permission rõ ràng cho dữ liệu người khác.
        if (
            "ai.personal.read.other"
            in permissions
        ):

            return None

        own_code = None

        if user.employee_code:

            own_code = re.sub(
                r"[\s-]+",
                "",
                str(
                    user.employee_code
                ),
            ).upper()

        # Nếu request chỉ nhắc chính employee code
        # của user hiện tại thì không coi là cross-access.
        if (
            own_code
            and employee_codes
            == {
                own_code
            }
        ):

            return None

        # Không có trusted employee_code để đối chiếu,
        # hoặc có code khác → fail closed.
        return SafetyResult(
            decision=(
                GuardrailDecision.BLOCK
            ),
            reason_code=(
                "CROSS_EMPLOYEE_ACCESS_DENIED"
            ),
            public_message=(
                "Bạn không có quyền xem "
                "dữ liệu cá nhân của nhân viên khác."
            ),
        )

    # =====================================================
    # AUTHORIZE
    #
    # question là standalone_question đã được
    # ContextResolver resolve.
    #
    # Optional để không làm vỡ code/test cũ.
    # =====================================================

    def authorize(
        self,
        route: AiRoute,
        user: UserContext,
        question: str | None = None,
    ) -> SafetyResult:

        roles = (
            self._normalized_roles(
                user
            )
        )

        permissions = (
            self._normalized_permissions(
                user
            )
        )

        # =================================================
        # DEFENSE IN DEPTH: PERSONAL DATA ROUTE MISMATCH
        # =================================================

        if (
            route == AiRoute.GENERAL_LLM
            and self._is_scoped_personal_request(
                question
            )
        ):
            return SafetyResult(
                decision=GuardrailDecision.BLOCK,
                reason_code=(
                    "SENSITIVE_HR_ROUTE_MISMATCH"
                ),
                public_message=(
                    "Yêu cầu dữ liệu nhân sự cá nhân "
                    "không được xử lý bởi General LLM."
                ),
            )

        # =================================================
        # GENERAL LLM
        # =================================================

        if (
            route
            == AiRoute.GENERAL_LLM
        ):

            return SafetyResult(
                decision=(
                    GuardrailDecision.ALLOW
                )
            )

        # =================================================
        # INTERNAL ROUTES REQUIRE AUTH
        # =================================================

        if not user.is_authenticated:

            return SafetyResult(
                decision=(
                    GuardrailDecision.REQUIRE_AUTH
                ),
                reason_code=(
                    "AUTH_REQUIRED"
                ),
                public_message=(
                    "Bạn cần đăng nhập để sử dụng "
                    "chức năng này."
                ),
            )

        # =================================================
        # EXPLICIT CROSS-EMPLOYEE REFERENCE
        #
        # Kiểm tra trước route-specific authorization.
        #
        # Ví dụ:
        # "Cho tôi xem lương NV002"
        # =================================================

        if (
            route == AiRoute.PERSONAL_DATA
            and self._is_bulk_personal_request(
                question
            )
        ):
            return SafetyResult(
                decision=GuardrailDecision.BLOCK,
                reason_code=(
                    "BULK_PERSONAL_DATA_ACCESS_DENIED"
                ),
                public_message=(
                    "AVA không cho phép truy xuất hàng loạt "
                    "dữ liệu cá nhân qua kênh Personal Data. "
                    "Hãy sử dụng chức năng báo cáo HRMS "
                    "được phân quyền."
                ),
            )

        if (
            route == AiRoute.PERSONAL_DATA
            and self._looks_like_named_other_target(
                question
            )
        ):
            return SafetyResult(
                decision=GuardrailDecision.BLOCK,
                reason_code=(
                    "NAMED_EMPLOYEE_TARGET_UNVERIFIED"
                ),
                public_message=(
                    "Không thể truy xuất dữ liệu cá nhân "
                    "của nhân viên khác chỉ bằng tên. "
                    "Cần mã nhân viên và quyền phù hợp."
                ),
            )

        employee_scope_result = (
            self._authorize_employee_reference(
                user=user,
                question=question,
            )
        )

        if employee_scope_result:

            return employee_scope_result

        # =================================================
        # RAG
        # =================================================

        if route == AiRoute.RAG:

            return SafetyResult(
                decision=(
                    GuardrailDecision.ALLOW
                )
            )

        # =================================================
        # PERSONAL DATA
        #
        # Route này mặc định chỉ dữ liệu của chính user.
        # PersonalDataService vẫn sử dụng trusted
        # user.employee_id.
        # =================================================

        if (
            route
            == AiRoute.PERSONAL_DATA
        ):

            if not user.employee_id:

                return SafetyResult(
                    decision=(
                        GuardrailDecision.REQUIRE_AUTH
                    ),
                    reason_code=(
                        "EMPLOYEE_CONTEXT_MISSING"
                    ),
                    public_message=(
                        "Không xác định được hồ sơ "
                        "nhân viên của tài khoản "
                        "hiện tại."
                    ),
                )

            return SafetyResult(
                decision=(
                    GuardrailDecision.ALLOW
                )
            )

        # =================================================
        # LIVE SQL
        # =================================================

        if (
            route
            == AiRoute.LIVE_SQL
        ):

            # ---------------------------------------------
            # 1. FULL ACCESS
            # ---------------------------------------------

            if (
                self._has_full_live_access(
                    user
                )
            ):

                return SafetyResult(
                    decision=(
                        GuardrailDecision.ALLOW
                    )
                )

            # ---------------------------------------------
            # 2. DEPARTMENT-SCOPED ACCESS
            # ---------------------------------------------

            if (
                self._has_department_live_access(
                    user
                )
            ):

                requested_department = (
                    self._extract_department(
                        question
                    )
                )

                # Leader/Manager hỏi aggregate toàn công ty.
                if not requested_department:

                    return SafetyResult(
                        decision=(
                            GuardrailDecision.BLOCK
                        ),
                        reason_code=(
                            "LIVE_DATA_SCOPE_REQUIRED"
                        ),
                        public_message=(
                            "Bạn chỉ có quyền xem dữ liệu "
                            "thuộc phòng ban được phân công."
                        ),
                    )

                # Backend chưa cấp department scope.
                if not user.department_names:

                    return SafetyResult(
                        decision=(
                            GuardrailDecision.BLOCK
                        ),
                        reason_code=(
                            "DEPARTMENT_SCOPE_MISSING"
                        ),
                        public_message=(
                            "Không xác định được phạm vi "
                            "phòng ban được phép truy cập."
                        ),
                    )

                if not self._department_allowed(
                    user=user,
                    requested_department=(
                        requested_department
                    ),
                ):

                    return SafetyResult(
                        decision=(
                            GuardrailDecision.BLOCK
                        ),
                        reason_code=(
                            "DEPARTMENT_ACCESS_DENIED"
                        ),
                        public_message=(
                            "Bạn không có quyền xem "
                            "dữ liệu của phòng ban này."
                        ),
                    )

                return SafetyResult(
                    decision=(
                        GuardrailDecision.ALLOW
                    )
                )

            # ---------------------------------------------
            # 3. NO LIVE ACCESS
            # ---------------------------------------------

            return SafetyResult(
                decision=(
                    GuardrailDecision.BLOCK
                ),
                reason_code=(
                    "LIVE_DATA_ACCESS_DENIED"
                ),
                public_message=(
                    "Bạn không có quyền xem "
                    "dữ liệu tổng hợp này."
                ),
            )

        # =================================================
        # UNKNOWN ROUTE
        # =================================================

        return SafetyResult(
            decision=(
                GuardrailDecision.BLOCK
            ),
            reason_code=(
                "ROUTE_ACCESS_DENIED"
            ),
            public_message=(
                "Bạn không có quyền thực hiện "
                "yêu cầu này."
            ),
        )