from config.settings import settings
from core.enums import AiRoute
from core.models import (
    ContextIntentResult,
    RouteDecision,
    UserContext,
)


class AiRouter:
    """
    Router chỉ chọn nguồn xử lý.

    Defensive V17:
    profile/entity nội bộ không được rơi xuống GENERAL_LLM.
    """

    def route(
        self,
        context: ContextIntentResult,
        user: UserContext,
    ) -> RouteDecision:

        if (
            context.need_clarification
            or context.resolution_confidence
            < settings.CONTEXT_CLARIFY_THRESHOLD
        ):
            return RouteDecision(
                route=None,
                confidence=(
                    context.resolution_confidence
                ),
                need_clarification=True,
                reason_code=(
                    "LOW_CONTEXT_CONFIDENCE"
                ),
            )

        signals = context.signals

        # 1. Personal Data — ưu tiên cao nhất.
        if signals.requires_user_specific_data:
            return RouteDecision(
                route=AiRoute.PERSONAL_DATA,
                confidence=(
                    context.intent_confidence
                ),
                reason_code=(
                    "USER_SPECIFIC_DATA"
                ),
            )

        # Defensive: employee entity + profile intent
        # không bao giờ cho General LLM tự đoán.
        has_employee_entity = any(
            getattr(
                entity,
                "entity_type",
                "",
            )
            == "employee_code"
            for entity in (
                context.entities
                or []
            )
        )

        standalone = (
            context.standalone_question
            or ""
        ).lower()

        if (
            has_employee_entity
            and any(
                phrase in standalone
                for phrase in (
                    "hồ sơ",
                    "thông tin",
                    "là ai",
                )
            )
        ):
            return RouteDecision(
                route=AiRoute.PERSONAL_DATA,
                confidence=max(
                    context.intent_confidence,
                    0.97,
                ),
                reason_code=(
                    "EMPLOYEE_PROFILE_ENTITY"
                ),
            )

        # 2. Live SQL
        if signals.requires_current_hrms_value:
            return RouteDecision(
                route=AiRoute.LIVE_SQL,
                confidence=(
                    context.intent_confidence
                ),
                reason_code=(
                    "CURRENT_HRMS_DATA"
                ),
            )

        # 3. RAG
        if signals.requires_internal_knowledge:
            return RouteDecision(
                route=AiRoute.RAG,
                confidence=(
                    context.intent_confidence
                ),
                reason_code=(
                    "INTERNAL_KNOWLEDGE"
                ),
            )

        # 4. General LLM
        return RouteDecision(
            route=AiRoute.GENERAL_LLM,
            confidence=(
                context.intent_confidence
            ),
            reason_code=(
                "GENERAL_KNOWLEDGE"
            ),
        )
