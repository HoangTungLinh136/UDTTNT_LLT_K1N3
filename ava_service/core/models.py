from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Optional
from uuid import uuid4

from core.enums import (
    AiRoute,
    GuardrailDecision,
    InputModality,
    VerificationStatus,
)


# =========================================================
# INPUT
# =========================================================

@dataclass
class AttachmentRef:
    file_id: Optional[str] = None
    file_name: Optional[str] = None
    mime_type: Optional[str] = None

    extracted_text: Optional[str] = None

    metadata: dict[str, Any] = field(
        default_factory=dict
    )


@dataclass
class NormalizedAiInput:
    text: str

    modality: InputModality = (
        InputModality.TEXT
    )

    attachments: list[AttachmentRef] = field(
        default_factory=list
    )

    metadata: dict[str, Any] = field(
        default_factory=dict
    )


# =========================================================
# USER CONTEXT
# =========================================================

@dataclass
class UserContext:
    user_id: Optional[str] = None
    employee_id: Optional[str] = None
    employee_code: Optional[str] = None

    is_authenticated: bool = False

    roles: list[str] = field(
        default_factory=list
    )

    permissions: list[str] = field(
        default_factory=list
    )

    department_ids: list[str] = field(
        default_factory=list
    )

    department_names: list[str] = field(
        default_factory=list
    )


# =========================================================
# CONVERSATION CONTEXT
# =========================================================

@dataclass
class ConversationEntity:
    """
    Một thực thể đang được nhắc tới trong hội thoại.

    Ví dụ:
    - employee_code = NV001
    - department = IT
    - month = 09/2026
    """

    entity_type: str
    value: str
    confidence: float = 1.0


@dataclass
class ConversationState:
    """
    Trạng thái hội thoại hiện tại của AVA.

    Có 3 nhóm dữ liệu chính:

    1. Context nghiệp vụ hiện tại
       - active_topic
       - active_entities
       - time_scope
       - last_route
       - last_source

    2. Short-term conversation state
       - recent_turns
       - pending_slot
       - pending_question
       - expected_answer_type
       - task_state

    3. Context dài hạn / cá nhân hóa
       - conversation_summary
       - user_memories

    Lưu ý:
    ConversationState KHÔNG phải database.
    Đây là object runtime được tạo cho từng request/conversation.
    """

    # -----------------------------------------------------
    # CURRENT TOPIC
    # -----------------------------------------------------

    active_topic: Optional[str] = None

    active_entities: list[ConversationEntity] = field(
        default_factory=list
    )

    time_scope: Optional[str] = None

    # -----------------------------------------------------
    # LAST PROCESSING RESULT
    # -----------------------------------------------------

    last_route: Optional[AiRoute] = None

    last_source: Optional[str] = None

    # -----------------------------------------------------
    # ATTACHMENT CONTEXT
    # -----------------------------------------------------

    attachment_context: dict[str, Any] = field(
        default_factory=dict
    )

    # -----------------------------------------------------
    # RECENT CONVERSATION
    #
    # Ví dụ:
    # [
    #     {
    #         "role": "user",
    #         "content": "Tôi muốn mua laptop"
    #     },
    #     {
    #         "role": "assistant",
    #         "content": "Bạn dùng laptop để làm gì?"
    #     }
    # ]
    # -----------------------------------------------------

    recent_turns: list[dict[str, str]] = field(
        default_factory=list
    )

    # -----------------------------------------------------
    # PENDING CONVERSATION
    #
    # Dùng khi AVA đang chờ user cung cấp thêm thông tin.
    #
    # Ví dụ:
    #
    # User:
    # "Tư vấn laptop cho tôi"
    #
    # AVA:
    # "Bạn dùng laptop để làm gì?"
    #
    # pending_slot = "purpose"
    # pending_question = "Bạn dùng laptop để làm gì?"
    # expected_answer_type = "text"
    #
    # User:
    # "Học tập"
    #
    # AVA hiểu "Học tập" là giá trị của purpose,
    # không coi đó là topic mới.
    # -----------------------------------------------------

    pending_slot: Optional[str] = None

    pending_question: Optional[str] = None

    expected_answer_type: Optional[str] = None

    # -----------------------------------------------------
    # TASK STATE
    #
    # Trạng thái của một tác vụ nhiều bước.
    #
    # Ví dụ:
    #
    # {
    #     "task": "laptop_recommendation",
    #     "purpose": "study",
    #     "budget": "15 triệu"
    # }
    #
    # Hoặc HRMS:
    #
    # {
    #     "task": "leave_request",
    #     "employee_code": "NV001",
    #     "stage": "manager_review"
    # }
    # -----------------------------------------------------

    task_state: dict[str, Any] = field(
        default_factory=dict
    )

    # -----------------------------------------------------
    # CONVERSATION SUMMARY
    #
    # Không gửi toàn bộ hàng trăm message cũ cho LLM.
    #
    # Những đoạn hội thoại cũ có thể được tóm tắt thành:
    #
    # "Người dùng đang tìm laptop khoảng 15 triệu,
    # mục đích học tập và lập trình."
    #
    # Sau này summary có thể được lấy từ database.
    # -----------------------------------------------------

    conversation_summary: Optional[str] = None

    # -----------------------------------------------------
    # PERSONAL MEMORY
    #
    # Những memory liên quan đến user hiện tại.
    #
    # Ví dụ:
    #
    # [
    #     {
    #         "memory_type": "preference",
    #         "content": "Ưu tiên câu trả lời ngắn",
    #         "confidence": 0.95
    #     }
    # ]
    #
    # Không lưu password/API key/dữ liệu nhạy cảm ở đây.
    #
    # Phase hiện tại chỉ chuẩn bị contract.
    # Database Personal Memory sẽ làm ở phase tiếp theo.
    # -----------------------------------------------------

    user_memories: list[dict[str, Any]] = field(
        default_factory=list
    )

    # -----------------------------------------------------
    # CONTEXT RESOLUTION CONFIDENCE
    # -----------------------------------------------------

    resolution_confidence: float = 1.0


# =========================================================
# CONTEXT + INTENT
# =========================================================

@dataclass
class IntentSignals:
    """
    Các tín hiệu để Router quyết định nguồn xử lý.

    Không coi đây là quyền truy cập.
    Authorization vẫn phải được kiểm tra riêng.
    """

    requires_internal_knowledge: bool = False

    requires_current_hrms_value: bool = False

    requires_user_specific_data: bool = False

    requires_general_knowledge: bool = False


@dataclass
class ContextIntentResult:
    """
    Kết quả sau khi:
    - resolve context
    - rewrite follow-up
    - classify intent
    """

    original_question: str

    standalone_question: str

    intent: str

    entities: list[ConversationEntity] = field(
        default_factory=list
    )

    time_scope: Optional[str] = None

    resolution_confidence: float = 1.0

    intent_confidence: float = 1.0

    route_hint: Optional[AiRoute] = None

    signals: IntentSignals = field(
        default_factory=IntentSignals
    )

    need_clarification: bool = False

    clarification_question: Optional[str] = None


# =========================================================
# ROUTER
# =========================================================

@dataclass
class RouteDecision:
    """
    Quyết định cuối của Router.

    route:
        RAG
        LIVE_SQL
        PERSONAL_DATA
        GENERAL_LLM

    Nếu thiếu dữ liệu:
        need_clarification = True
    """

    route: Optional[AiRoute]

    confidence: float

    need_clarification: bool = False

    reason_code: str = "OK"


# =========================================================
# SOURCE / CITATION
# =========================================================

@dataclass
class SourceRef:
    """
    Nguồn dữ liệu mà AVA đã sử dụng.

    Ví dụ:
    - RAG document
    - SQL
    - Personal Data
    - web result
    """

    source_type: str

    source_name: Optional[str] = None

    document_id: Optional[str] = None

    file_name: Optional[str] = None

    page: Optional[str] = None

    section: Optional[str] = None

    version: Optional[str] = None

    queried_at: Optional[str] = None

    metadata: dict[str, Any] = field(
        default_factory=dict
    )


@dataclass
class CitationRef:
    """
    Citation công khai cho câu trả lời.

    Chỉ sử dụng dữ liệu phù hợp để hiển thị cho user.
    """

    source_name: str

    document_id: Optional[str] = None

    file_name: Optional[str] = None

    page: Optional[str] = None

    section: Optional[str] = None

    quote: Optional[str] = None


# =========================================================
# ROUTE RESULT
# =========================================================

@dataclass
class RouteResult:
    """
    Kết quả thô của một route trước khi:
    - verification
    - response composition
    """

    route: AiRoute

    answer_draft: str

    data: Any = None

    sources: list[SourceRef] = field(
        default_factory=list
    )

    citations: list[CitationRef] = field(
        default_factory=list
    )

    warning: Optional[str] = None

    confidence: Optional[float] = None


# =========================================================
# SAFETY
# =========================================================

@dataclass
class SafetyResult:
    """
    Kết quả Guardrail.

    ALLOW:
        tiếp tục xử lý

    Các trạng thái khác:
        chặn hoặc thay đổi response theo policy
    """

    decision: GuardrailDecision = (
        GuardrailDecision.ALLOW
    )

    reason_code: Optional[str] = None

    public_message: Optional[str] = None


# =========================================================
# VERIFICATION
#
# Mặc định request chưa qua AnswerVerifier:
#
# status = WARNING
# checks = ["NOT_VERIFIED"]
# warning = None
#
# warning phải là None vì:
#
# response.warning
#
# được dành cho business/safety warning công khai,
# không được để "NOT_VERIFIED" rò ra UI bình thường.
# =========================================================

def _default_verification_checks() -> list[str]:
    return [
        "NOT_VERIFIED",
    ]


@dataclass
class VerificationResult:
    status: VerificationStatus = (
        VerificationStatus.WARNING
    )

    checks: list[str] = field(
        default_factory=_default_verification_checks
    )

    warning: Optional[str] = None


# =========================================================
# RESPONSE CONTRACT
# =========================================================

@dataclass
class AiResponse:
    """
    Response contract cuối cùng trả từ AVA.

    Không đưa private reasoning / chain-of-thought
    vào object response này.
    """

    answer: str

    route: Optional[AiRoute]

    intent: str

    sources: list[SourceRef] = field(
        default_factory=list
    )

    citations: list[CitationRef] = field(
        default_factory=list
    )

    warning: Optional[str] = None

    can_speak: bool = True

    # -----------------------------------------------------
    # REQUEST ID
    #
    # ID riêng cho request hiện tại.
    # -----------------------------------------------------

    request_id: str = field(
        default_factory=lambda: str(
            uuid4()
        )
    )

    # -----------------------------------------------------
    # TRACE ID
    #
    # Dùng correlation các telemetry event
    # trong cùng một AI request.
    # -----------------------------------------------------

    trace_id: str = field(
        default_factory=lambda: str(
            uuid4()
        )
    )

    safety: SafetyResult = field(
        default_factory=SafetyResult
    )

    verification: VerificationResult = field(
        default_factory=VerificationResult
    )

    diagnostics: Optional[dict[str, Any]] = None

    created_at: str = field(
        default_factory=lambda: (
            datetime.utcnow().isoformat()
        )
    )
