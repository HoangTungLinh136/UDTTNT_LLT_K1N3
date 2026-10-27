from __future__ import annotations

from core.enums import AiRoute
from core.models import RouteResult
from config.settings import settings
from providers.provider_factory import ProviderFactory


class MultimodalAnswerService:
    """
    Handoff cuối cho Image/Document.

    Mục tiêu:
    - Vision/Parser đã trích nội dung thì câu trả lời cuối PHẢI dùng nội dung đó.
    - Không được nói "không xem được ảnh" sau khi vision đã chạy thành công.
    - Không biến upload thành route thứ 5; đây chỉ là preprocessing/output adapter.
    """

    def __init__(self):
        self.llm = ProviderFactory.create(
            settings.GENERAL_LLM_PROVIDER
        )

    def answer_from_image_context(
        self,
        question: str,
        visual_context: str,
    ) -> RouteResult:

        system_prompt = """
Bạn là AVA, trợ lý AI đa phương thức.

Bạn đã được hệ thống Vision/OCR cung cấp mô tả của ảnh.
Hãy trả lời câu hỏi của người dùng DỰA TRÊN phần VISUAL_CONTEXT.

Quy tắc:
- Trả lời bằng tiếng Việt.
- Không nói rằng bạn không thể xem ảnh, vì Vision đã xử lý ảnh.
- Không suy đoán danh tính người.
- Không tự bịa chi tiết không có trong VISUAL_CONTEXT.
- Nếu ảnh không đủ dữ liệu để trả lời, nói rõ phần nào chưa xác định được.
"""

        user_prompt = f"""
CÂU HỎI:
{question}

VISUAL_CONTEXT:
{visual_context}
"""

        try:
            answer, usage = self.llm.chat_with_usage(
                system_prompt=system_prompt,
                user_prompt=user_prompt,
                temperature=0.1,
                max_tokens=800,
            )
        except Exception:
            return RouteResult(
                route=AiRoute.GENERAL_LLM,
                answer_draft=(
                    "Tôi đã đọc ảnh nhưng hiện tại không thể "
                    "tổng hợp câu trả lời."
                ),
                warning="VISION_ANSWER_FAILED",
                confidence=0.0,
            )

        return RouteResult(
            route=AiRoute.GENERAL_LLM,
            answer_draft=(answer or "").strip(),
            data={
                "multimodal": "image",
                "llm_usage": self.llm.usage_to_dict(usage),
            },
            confidence=0.95,
        )

    def answer_from_document_context(
        self,
        question: str,
        document_context: str,
    ) -> RouteResult:

        system_prompt = """
Bạn là AVA, trợ lý AI phân tích tài liệu.

Hãy trả lời câu hỏi DỰA TRÊN DOCUMENT_CONTEXT đã được parser trích xuất.

Quy tắc:
- Trả lời bằng tiếng Việt.
- Không bịa nội dung ngoài tài liệu.
- Nếu tài liệu không hỗ trợ câu trả lời, nói rõ.
- Có thể tóm tắt, phân tích, trích ý, so sánh và giải thích nội dung.
"""

        user_prompt = f"""
CÂU HỎI:
{question}

DOCUMENT_CONTEXT:
{document_context}
"""

        try:
            answer, usage = self.llm.chat_with_usage(
                system_prompt=system_prompt,
                user_prompt=user_prompt,
                temperature=0.1,
                max_tokens=1000,
            )
        except Exception:
            return RouteResult(
                route=AiRoute.GENERAL_LLM,
                answer_draft=(
                    "Tôi đã đọc tài liệu nhưng hiện tại không thể "
                    "tổng hợp câu trả lời."
                ),
                warning="DOCUMENT_ANSWER_FAILED",
                confidence=0.0,
            )

        return RouteResult(
            route=AiRoute.GENERAL_LLM,
            answer_draft=(answer or "").strip(),
            data={
                "multimodal": "document",
                "llm_usage": self.llm.usage_to_dict(usage),
            },
            confidence=0.95,
        )
