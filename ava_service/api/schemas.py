from __future__ import annotations

from typing import Any, Literal

from pydantic import (
    AliasChoices,
    BaseModel,
    ConfigDict,
    Field,
)


class AttachmentRequest(BaseModel):
    model_config = ConfigDict(
        populate_by_name=True
    )

    file_id: str | None = Field(
        default=None,
        validation_alias=AliasChoices(
            "fileId",
            "file_id",
        ),
    )

    file_name: str | None = Field(
        default=None,
        validation_alias=AliasChoices(
            "fileName",
            "file_name",
        ),
    )

    mime_type: str | None = Field(
        default=None,
        validation_alias=AliasChoices(
            "mimeType",
            "mime_type",
        ),
    )

    extracted_text: str | None = Field(
        default=None,
        max_length=20_000,
        validation_alias=AliasChoices(
            "extractedText",
            "extracted_text",
        ),
    )

    content_base64: str | None = Field(
        default=None,
        max_length=28_000_000,
        validation_alias=AliasChoices(
            "contentBase64",
            "content_base64",
        ),
    )

    metadata: dict[str, Any] = Field(
        default_factory=dict
    )


class TranscribeRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    file_name: str = Field(min_length=1, max_length=180, validation_alias=AliasChoices("fileName", "file_name"))
    mime_type: str = Field(min_length=1, max_length=100, validation_alias=AliasChoices("mimeType", "mime_type"))
    content_base64: str = Field(min_length=1, max_length=28_000_000, validation_alias=AliasChoices("contentBase64", "content_base64"))


class UserContextRequest(BaseModel):
    model_config = ConfigDict(
        populate_by_name=True
    )

    user_id: str = Field(
        validation_alias=AliasChoices(
            "userId",
            "user_id",
        ),
    )

    employee_id: str | None = Field(
        default=None,
        validation_alias=AliasChoices(
            "employeeId",
            "employee_id",
        ),
    )

    employee_code: str | None = Field(
        default=None,
        validation_alias=AliasChoices(
            "employeeCode",
            "employee_code",
        ),
    )

    is_authenticated: bool = Field(
        default=True,
        validation_alias=AliasChoices(
            "isAuthenticated",
            "is_authenticated",
        ),
    )

    roles: list[str] = Field(
        default_factory=list
    )

    permissions: list[str] = Field(
        default_factory=list
    )

    department_ids: list[str] = Field(
        default_factory=list,
        validation_alias=AliasChoices(
            "departmentIds",
            "department_ids",
        ),
    )

    department_names: list[str] = Field(
        default_factory=list,
        validation_alias=AliasChoices(
            "departmentNames",
            "department_names",
        ),
    )


class ConversationTurnRequest(BaseModel):
    """
    Một lượt hội thoại được ASP.NET gateway gửi sang AVA.

    role chỉ cho phép các loại đã biết.
    content không được rỗng.
    """

    model_config = ConfigDict(
        populate_by_name=True
    )

    role: Literal[
        "user",
        "assistant",
        "system",
    ]

    content: str = Field(
        min_length=1,
        max_length=8_000,
    )

    attachment_context: dict[str, Any] = Field(
        default_factory=dict,
        validation_alias=AliasChoices(
            "attachmentContext",
            "attachment_context",
        ),
    )



class UserMemoryRequest(BaseModel):
    """
    Personal memory được backend ASP.NET gửi sang AVA.

    Đây là context hỗ trợ cá nhân hóa.
    Không phải authorization source.
    """

    model_config = ConfigDict(
        populate_by_name=True
    )

    memory_type: str = Field(
        min_length=1,
        max_length=64,
        validation_alias=AliasChoices(
            "memoryType",
            "memory_type",
        ),
    )

    memory_key: str = Field(
        min_length=1,
        max_length=128,
        validation_alias=AliasChoices(
            "memoryKey",
            "memory_key",
        ),
    )

    content: str = Field(
        min_length=1,
        max_length=4_000,
    )

    confidence: float = Field(
        default=1.0,
        ge=0.0,
        le=1.0,
    )

    source: str = Field(
        default="conversation",
        max_length=64,
    )


class ChatRequest(BaseModel):
    """
    Payload accepted only from the trusted ASP.NET gateway.

    user_context và history không được browser tự khai báo trực tiếp.
    ASP.NET chịu trách nhiệm authentication / authorization
    trước khi gọi AVA.
    """

    model_config = ConfigDict(
        populate_by_name=True
    )

    text: str = Field(
        min_length=1,
        max_length=8_000,
    )

    # Exact browser-authored text. ``text`` may contain a trusted, neutral
    # internal instruction for an attachment-only turn.
    user_text: str | None = Field(
        default=None,
        max_length=8_000,
        validation_alias=AliasChoices(
            "userText",
            "user_text",
        ),
    )

    modality: str = "TEXT"

    conversation_id: str | None = Field(
        default=None,
        max_length=128,
        validation_alias=AliasChoices(
            "conversationId",
            "conversation_id",
        ),
    )

    request_id: str | None = Field(
        default=None,
        max_length=128,
        validation_alias=AliasChoices(
            "requestId",
            "request_id",
        ),
    )

    trace_id: str | None = Field(
        default=None,
        max_length=128,
        validation_alias=AliasChoices(
            "traceId",
            "trace_id",
        ),
    )

    user_context: UserContextRequest = Field(
        validation_alias=AliasChoices(
            "userContext",
            "user_context",
        ),
    )

    history: list[ConversationTurnRequest] = Field(
        default_factory=list,
        max_length=8,
    )

    conversation_summary: str | None = Field(
        default=None,
        max_length=20_000,
        validation_alias=AliasChoices(
            "conversationSummary",
            "conversation_summary",
        ),
    )

    user_memories: list[UserMemoryRequest] = Field(
        default_factory=list,
        max_length=20,
        validation_alias=AliasChoices(
            "userMemories",
            "user_memories",
        ),
    )

    attachments: list[AttachmentRequest] = Field(
        default_factory=list,
        max_length=5,
    )


class ChatResponse(BaseModel):
    # Persist the bounded, sanitized attachment context returned by preprocessing.
    attachmentContext: dict[str, Any] = Field(default_factory=dict)

    requestId: str
    traceId: str
    conversationId: str | None = None

    answer: str

    route: str | None = None

    intent: str

    sources: list[dict[str, Any]] = Field(
        default_factory=list
    )

    citations: list[dict[str, Any]] = Field(
        default_factory=list
    )

    warning: str | None = None

    canSpeak: bool = True

    verification: dict[str, Any]

    safety: dict[str, Any]

    diagnostics: dict[str, Any] | None = None
