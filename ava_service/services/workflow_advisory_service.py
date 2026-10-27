from __future__ import annotations

import re
import unicodedata

from dataclasses import dataclass

from core.models import UserContext


@dataclass
class WorkflowAdvisoryResult:
    answer: str
    intent: str = "WorkflowGuidance"
    warning: str | None = None


class WorkflowAdvisoryService:
    """
    PHASE 08

    AVA chỉ:
    - nhận biết workflow command/question;
    - giải thích quyền và bước nghiệp vụ;
    - hướng dẫn user sang chức năng HRMS tương ứng.

    AVA KHÔNG:
    - approve;
    - reject;
    - ký duyệt;
    - thay đổi trạng thái DB;
    - bypass backend authorization;
    - giả lập quyền từ prompt.

    Source of truth cuối cùng:
    ASP.NET + ApprovalEngine + IWorkflowPolicy.
    """

    def _normalize(
        self,
        value: str | None,
    ) -> str:

        text = unicodedata.normalize(
            "NFC",
            str(value or ""),
        )

        text = re.sub(
            r"\s+",
            " ",
            text,
        )

        return text.strip().lower()

    def _roles(
        self,
        user: UserContext,
    ) -> set[str]:

        return {
            str(role)
            .strip()
            .upper()
            for role in (
                user.roles
                or []
            )
            if str(role).strip()
        }

    # =====================================================
    # WORKFLOW DETECTION
    # =====================================================

    def _looks_like_workflow(
        self,
        question: str,
    ) -> bool:

        q = self._normalize(
            question
        )

        # Data/COUNT/VERIFY questions must continue through
        # Context -> Intent -> Router instead of being consumed
        # by the workflow advisory guard.
        data_query_markers = (
            "bao nhiêu",
            "mấy đơn",
            "số lượng",
            "đếm",
            "đúng không",
            "có đúng không",
            "phải không",
            "có phải",
        )

        if any(
            marker in q
            for marker in data_query_markers
        ):
            return False

        markers = (
            "duyệt đơn",
            "phê duyệt",
            "từ chối đơn",
            "từ chối yêu cầu",
            "approve",
            "reject",
            "ký duyệt",
            "ký đơn",
            "xác nhận đơn",
            "xác nhận yêu cầu",
            "hủy đơn",
            "hủy yêu cầu",
            "chờ duyệt",
            "ai duyệt",
            "ai được duyệt",
            "quy trình duyệt",
            "bước duyệt",
            "cấp duyệt",
            "trình duyệt",
        )

        return any(
            marker in q
            for marker in markers
        )

    # =====================================================
    # MUTATION DETECTION
    # =====================================================

    def _looks_like_mutation_request(
        self,
        question: str,
    ) -> bool:

        q = self._normalize(
            question
        )

        informational_markers = (
            "ai được",
            "ai có quyền",
            "quy trình",
            "như thế nào",
            "thế nào",
            "có được",
            "có quyền",
            "được phép",
            "hướng dẫn",
            "tại sao",
            "vì sao",
            "bước nào",
            "đang ở đâu",
            "trạng thái",
        )

        if any(
            marker in q
            for marker in informational_markers
        ):
            return False

        action_patterns = (
            r"\bduyệt\b",
            r"\bphê duyệt\b",
            r"\btừ chối\b",
            r"\bapprove\b",
            r"\breject\b",
            r"\bký duyệt\b",
            r"\bxác nhận\b",
            r"\bhủy đơn\b",
            r"\bhủy yêu cầu\b",
        )

        return any(
            re.search(
                pattern,
                q,
                re.IGNORECASE,
            )
            for pattern in action_patterns
        )

    # =====================================================
    # DOMAIN DETECTION
    # =====================================================

    def _domain(
        self,
        question: str,
    ) -> str:

        q = self._normalize(
            question
        )

        if any(
            item in q
            for item in (
                "nghỉ phép",
                "đơn nghỉ",
                "leave",
            )
        ):
            return "LEAVE"

        if any(
            item in q
            for item in (
                "điều chỉnh công",
                "giải trình công",
                "chấm công",
            )
        ):
            return "ATTENDANCE_ADJUSTMENT"

        if "bổ nhiệm" in q:
            return "APPOINTMENT"

        if "miễn nhiệm" in q:
            return "DISMISSAL"

        if any(
            item in q
            for item in (
                "điều chuyển",
                "thuyên chuyển",
            )
        ):
            return "TRANSFER"

        if "khen thưởng" in q:
            return "REWARD"

        if "đề xuất" in q:
            return "PROPOSAL"

        return "GENERIC"

    # =====================================================
    # READ-ONLY GUIDANCE
    # =====================================================

    def _guidance(
        self,
        domain: str,
        roles: set[str],
    ) -> str:

        # -------------------------------------------------
        # LEAVE
        #
        # Source hiện tại:
        # Employee -> Manager -> Approved / Rejected.
        # -------------------------------------------------

        if domain == "LEAVE":

            if "MANAGER" in roles:

                return (
                    "Với đơn nghỉ phép thông thường, "
                    "Manager trực tiếp là cấp duyệt theo "
                    "workflow hiện tại. Hệ thống backend "
                    "sẽ kiểm tra phạm vi phòng ban, trạng thái "
                    "đơn và ngăn tự duyệt. "
                    "Việc phê duyệt/từ chối phải thực hiện "
                    "trên chức năng HRMS, không thực hiện qua AVA."
                )

            if "EMPLOYEE" in roles:

                return (
                    "Nhân viên có thể tạo và theo dõi đơn "
                    "nghỉ phép của chính mình. Đơn sẽ được "
                    "chuyển tới Manager phụ trách để duyệt. "
                    "AVA chỉ có thể hướng dẫn hoặc giải thích "
                    "trạng thái, không thể tự phê duyệt đơn."
                )

            if (
                "LEADER" in roles
                or "HR" in roles
                or "ADMIN" in roles
            ):

                return (
                    "Workflow nghỉ phép thông thường hiện tại "
                    "không cho Leader, HR hoặc Admin duyệt thay "
                    "Manager. Quyết định cuối phải tuân theo "
                    "LeaveWorkflowPolicy trong backend."
                )

        # -------------------------------------------------
        # MULTI-STAGE BUSINESS WORKFLOWS
        # -------------------------------------------------

        if domain in {
            "APPOINTMENT",
            "DISMISSAL",
            "TRANSFER",
            "REWARD",
        }:

            if "MANAGER" in roles:

                return (
                    "Manager chỉ được thực hiện bước nghiệp vụ "
                    "thuộc phạm vi quản lý và trạng thái hiện tại. "
                    "Các workflow cần phê duyệt cấp lãnh đạo sẽ "
                    "được chuyển sang Leader theo policy backend. "
                    "AVA không thể chuyển hoặc duyệt thay."
                )

            if "LEADER" in roles:

                return (
                    "Leader có thể xử lý các hồ sơ đang ở đúng "
                    "giai đoạn chờ Lãnh đạo duyệt và thuộc phạm vi "
                    "được phân công. Backend vẫn kiểm tra scope, "
                    "trạng thái và quy tắc tự duyệt trước khi "
                    "cho phép hành động."
                )

            if "HR" in roles:

                return (
                    "HR xử lý các bước nghiệp vụ HR được policy "
                    "quy định, ví dụ xác minh hoặc hoàn tất hồ sơ. "
                    "HR không được bỏ qua Manager/Leader nếu "
                    "workflow hiện tại yêu cầu các cấp đó."
                )

        if domain == "ATTENDANCE_ADJUSTMENT":

            return (
                "Yêu cầu điều chỉnh/chấm công phải được backend "
                "kiểm tra quyền Manager và phạm vi nhân viên. "
                "AVA chỉ giải thích workflow, không được tự "
                "Approve hoặc Reject."
            )

        if domain == "PROPOSAL":

            return (
                "Đề xuất phải đi theo workflow và phạm vi quyền "
                "đã cấu hình trong backend. AVA chỉ có thể hướng "
                "dẫn bước tiếp theo; mọi quyết định vẫn phải qua "
                "ApprovalEngine và WorkflowPolicy."
            )

        return (
            "Việc duyệt, từ chối, ký xác nhận hoặc thay đổi "
            "trạng thái workflow phải được thực hiện bởi chức "
            "năng HRMS tương ứng. AVA chỉ hỗ trợ hiểu yêu cầu, "
            "giải thích quyền và hướng dẫn bước tiếp theo."
        )

    # =====================================================
    # MAIN
    # =====================================================

    def analyze(
        self,
        question: str,
        user: UserContext,
    ) -> WorkflowAdvisoryResult | None:

        if not self._looks_like_workflow(
            question
        ):
            return None

        roles = self._roles(
            user
        )

        domain = self._domain(
            question
        )

        guidance = self._guidance(
            domain,
            roles,
        )

        if self._looks_like_mutation_request(
            question
        ):

            return WorkflowAdvisoryResult(
                answer=(
                    "AVA không trực tiếp thực hiện thao tác "
                    "duyệt, từ chối, ký hoặc thay đổi trạng thái "
                    "workflow.\n\n"
                    + guidance
                ),
                intent="WorkflowActionBlocked",
                warning=(
                    "WORKFLOW_ACTION_REQUIRES_HRMS_BACKEND"
                ),
            )

        return WorkflowAdvisoryResult(
            answer=guidance,
            intent="WorkflowGuidance",
            warning=None,
        )
