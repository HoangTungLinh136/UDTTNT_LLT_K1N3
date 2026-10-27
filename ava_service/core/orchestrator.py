from __future__ import annotations

import os
import time
from dataclasses import replace

from config.settings import settings

from context.context_resolver import (
    ContextResolver,
)

from context.intent_classifier import (
    IntentClassifier,
)

from core.enums import (
    AiRoute,
    GuardrailDecision,
)

from core.models import (
    AiResponse,
    ConversationEntity,
    ConversationState,
    NormalizedAiInput,
    RouteResult,
    SafetyResult,
    UserContext,
)

from monitoring.telemetry import (
    TelemetryService,
)

from providers.provider_factory import (
    ProviderFactory,
)

from response.composer import (
    ResponseComposer,
)

from response.verifier import (
    AnswerVerifier,
)

from routing.router import (
    AiRouter,
)

from safety.authorization import (
    AuthorizationService,
)

from safety.guardrails import (
    GuardrailService,
)

from services.general_llm_service import (
    GeneralLlmService,
)

from services.live_sql_service import (
    LiveSqlService,
)

from services.personal_data_service import (
    PersonalDataService,
)

from services.workflow_advisory_service import (
    WorkflowAdvisoryService,
)


class AiOrchestrator:

    def __init__(self):

        self.guardrails = (
            GuardrailService()
        )

        self.authorization = (
            AuthorizationService()
        )

        self.context_resolver = (
            ContextResolver()
        )

        self.intent_classifier = (
            IntentClassifier()
        )

        self.router = (
            AiRouter()
        )

        # RAG imports sentence-transformers/torch and loads a ~471 MB model.
        # Defer it so /health and non-RAG routes do not pay cold-start cost.
        self.rag_service = None

        self.live_sql_service = (
            LiveSqlService()
        )

        self.personal_data_service = (
            PersonalDataService()
        )

        self.general_llm_service = (
            GeneralLlmService()
        )

        self.workflow_advisory_service = (
            WorkflowAdvisoryService()
        )

        self.composer = (
            ResponseComposer()
        )

        self.verifier = (
            AnswerVerifier()
        )

        self.telemetry = (
            TelemetryService()
        )

    def _get_rag_service(self):
        if self.rag_service is None:
            from services.rag_service import RagService

            self.rag_service = RagService()

        return self.rag_service

    # =====================================================
    # TELEMETRY PROVIDER / MODEL
    # =====================================================

    def _telemetry_provider_model(
        self,
        route: AiRoute | None,
        route_result: RouteResult | None = None,
    ) -> tuple[
        str | None,
        str | None,
    ]:

        if route == AiRoute.RAG:
            return ("rag_local", settings.OLLAMA_TEXT_MODEL)

        if route == AiRoute.GENERAL_LLM:

            agent_tool = None

            if (
                route_result is not None
                and isinstance(
                    route_result.data,
                    dict,
                )
            ):
                agent_tool = (
                    route_result.data.get(
                        "agent_tool"
                    )
                )

            if agent_tool in {
                "CALCULATOR",
                "CURRENT_TIME",
                "CURRENT_DATE",
                "NONE",
            }:

                return (
                    None,
                    None,
                )

            provider_name = route_result.data.get("provider") if isinstance(route_result.data, dict) else None
            model_name = route_result.data.get("model") if isinstance(route_result.data, dict) else None
            if provider_name:
                return str(provider_name), model_name
            return ProviderFactory.describe(settings.GENERAL_LLM_PROVIDER)

        if route in {
            AiRoute.LIVE_SQL,
            AiRoute.PERSONAL_DATA,
        }:

            return (
                "sqlserver",
                None,
            )

        return (
            None,
            None,
        )


    def _finalize(
        self,
        response: AiResponse,
        started_at: float,
        error_type: str | None = None,
        route_result: RouteResult | None = None,
    ) -> AiResponse:

        latency_ms = (
            time.perf_counter()
            - started_at
        ) * 1000

        route_value = None

        if response.route is not None:

            route_value = (
                response.route.value
                if hasattr(
                    response.route,
                    "value",
                )
                else str(
                    response.route
                )
            )

        safety_value = None

        if (
            response.safety
            and
            response.safety.decision
        ):

            safety_value = (
                response.safety
                .decision
                .value
                if hasattr(
                    response.safety.decision,
                    "value",
                )
                else str(
                    response.safety.decision
                )
            )

        verification_value = None

        if (
            response.verification
            and
            response.verification.status
        ):

            verification_value = (
                response.verification
                .status
                .value
                if hasattr(
                    response.verification.status,
                    "value",
                )
                else str(
                    response.verification.status
                )
            )

        provider, model = (
            self._telemetry_provider_model(
                response.route,
                route_result,
            )
        )

        safety_reason = None

        if response.safety:

            safety_reason = (
                response.safety
                .reason_code
            )

        # =================================================
        # PHASE 19C - LLM USAGE
        # =================================================
        prompt_tokens = None
        completion_tokens = None
        total_tokens = None
        estimated_cost_usd = None

        if (
            route_result is not None
            and isinstance(
                route_result.data,
                dict,
            )
        ):

            llm_usage = (
                route_result.data.get(
                    "llm_usage"
                )
            )

            if isinstance(
                llm_usage,
                dict,
            ):

                prompt_tokens = (
                    llm_usage.get(
                        "prompt_tokens"
                    )
                )

                completion_tokens = (
                    llm_usage.get(
                        "completion_tokens"
                    )
                )

                total_tokens = (
                    llm_usage.get(
                        "total_tokens"
                    )
                )

                estimated_cost_usd = (
                    llm_usage.get(
                        "estimated_cost_usd"
                    )
                )

        try:

            self.telemetry.log_request(
                request_id=(
                    response.request_id
                ),
                trace_id=(
                    response.trace_id
                ),
                route=(
                    route_value
                ),
                intent=(
                    response.intent
                ),
                provider=provider,
                model=model,
                latency_ms=(
                    latency_ms
                ),
                safety_decision=(
                    safety_value
                ),
                safety_reason=(
                    safety_reason
                ),
                verification_status=(
                    verification_value
                ),
                warning=(
                    response.warning
                ),
                error_type=(
                    error_type
                ),
                prompt_tokens=(
                    prompt_tokens
                ),
                completion_tokens=(
                    completion_tokens
                ),
                total_tokens=(
                    total_tokens
                ),
                estimated_cost_usd=(
                    estimated_cost_usd
                ),
            )

        except Exception:

            # Telemetry không được làm
            # hỏng request chính.
            pass

        if settings.DIAGNOSTICS_ENABLED and os.getenv("AVA_ENVIRONMENT", "production").strip().lower() == "development":
            tool_name = None
            if route_result is not None and isinstance(route_result.data, dict):
                tool_name = route_result.data.get("tool") or route_result.data.get("operation") or route_result.data.get("agent_tool")
            response.diagnostics = {
                "route": route_value,
                "intent": response.intent,
                "confidence": route_result.confidence if route_result else None,
                "provider": provider,
                "model": model,
                "generation": (
                    route_result.data.get("generation_provider")
                    if route_result is not None and isinstance(route_result.data, dict)
                    else None
                ),
                "tool": tool_name,
                "latencyMs": round(latency_ms, 2),
            }

        return response

    # =====================================================
    # OUTPUT GUARDRAIL
    #
    # Chạy sau Composer và trước khi lưu conversation history.
    # Mục tiêu:
    # - mask API key / token / password / internal path;
    # - không làm thay đổi business answer bình thường;
    # - không ghi unmasked output vào recent_turns.
    #
    # Nếu request trước đó đã BLOCK/REQUIRE_AUTH thì giữ
    # safety decision mạnh hơn, không ghi đè thành MASK.
    # =====================================================

    def _apply_output_guardrail(
        self,
        response: AiResponse,
    ) -> AiResponse:

        masked_answer, output_safety = (
            self.guardrails.inspect_output(
                response.answer,
                response.route,
            )
        )

        response.answer = masked_answer

        if (
            output_safety.decision
            != GuardrailDecision.ALLOW
        ):

            if (
                response.safety.decision
                == GuardrailDecision.ALLOW
            ):

                response.safety = (
                    output_safety
                )

        return response

    # =====================================================
    # RAG OBSERVABILITY
    # =====================================================

    def _log_rag_observability(
        self,
        response: AiResponse,
        route_result: RouteResult,
    ) -> None:

        if (
            route_result.route
            != AiRoute.RAG
        ):
            return

        if not isinstance(
            route_result.data,
            dict,
        ):
            return

        metrics = (
            route_result.data.get(
                "retrieval_metrics"
            )
        )

        if not isinstance(
            metrics,
            dict,
        ):
            return

        try:

            self.telemetry.log_rag_retrieval(
                request_id=(
                    response.request_id
                ),
                trace_id=(
                    response.trace_id
                ),
                candidate_count=(
                    metrics.get(
                        "candidate_count",
                        0,
                    )
                ),
                evidence_count=(
                    metrics.get(
                        "evidence_count",
                        0,
                    )
                ),
                candidate_top_k=(
                    metrics.get(
                        "candidate_top_k",
                        0,
                    )
                ),
                final_top_k=(
                    metrics.get(
                        "final_top_k",
                        0,
                    )
                ),
                max_distance=(
                    metrics.get(
                        "max_distance",
                        0.0,
                    )
                ),
                top_distance=(
                    metrics.get(
                        "top_distance"
                    )
                ),
                retrieval_ms=(
                    metrics.get(
                        "retrieval_ms",
                        0.0,
                    )
                ),
                vector_search_ms=(
                    metrics.get(
                        "vector_search_ms",
                        0.0,
                    )
                ),
                query_variants=(
                    metrics.get(
                        "query_variants",
                        1,
                    )
                ),
                no_evidence=(
                    metrics.get(
                        "no_evidence",
                        False,
                    )
                ),
                source_count=(
                    metrics.get(
                        "source_count",
                        len(
                            route_result.sources
                        ),
                    )
                ),
                citation_count=(
                    metrics.get(
                        "citation_count",
                        len(
                            route_result.citations
                        ),
                    )
                ),
                metadata_filtered_count=(
                    metrics.get(
                        "metadata_filtered_count",
                        0,
                    )
                ),
                permission_filtered_count=(
                    metrics.get(
                        "permission_filtered_count",
                        0,
                    )
                ),
                permission_denied_count=(
                    metrics.get(
                        "permission_denied_count",
                        0,
                    )
                ),
                pre_rerank_count=(
                    metrics.get(
                        "pre_rerank_count",
                        0,
                    )
                ),
                post_rerank_count=(
                    metrics.get(
                        "post_rerank_count",
                        0,
                    )
                ),
                evidence_pruned_count=(
                    metrics.get(
                        "evidence_pruned_count",
                        0,
                    )
                ),
                best_rerank_score=(
                    metrics.get(
                        "best_rerank_score"
                    )
                ),
                best_lexical_affinity=(
                    metrics.get(
                        "best_lexical_affinity"
                    )
                ),
            )

        except Exception:

            # Observability không được làm hỏng
            # câu trả lời chính.
            pass

    # =====================================================
    # UPDATE ENTITY STATE
    # =====================================================

    def _update_entity_state(
        self,
        question: str,
        state: ConversationState,
    ) -> None:

        detected = (
            self.context_resolver
            .entity_resolver
            .detect(
                question
            )
        )

        if not detected:
            return

        entity_type = (
            detected.get(
                "type"
            )
        )

        value = (
            detected.get(
                "value"
            )
        )

        if (
            not entity_type
            or
            not value
        ):
            return

        new_entity = (
            ConversationEntity(
                entity_type=(
                    entity_type
                ),
                value=(
                    value
                ),
                confidence=float(
                    detected.get(
                        "confidence",
                        1.0,
                    )
                ),
            )
        )

        # =================================================
        # REPLACE SAME ENTITY TYPE
        # =================================================

        state.active_entities = [
            entity
            for entity
            in state.active_entities
            if (
                entity.entity_type
                !=
                new_entity.entity_type
            )
        ]

        state.active_entities.append(
            new_entity
        )

        state.active_entities = (
            state.active_entities[-8:]
        )

    # =====================================================
    # MAIN PROCESS
    # =====================================================

    def process(
        self,
        ai_input: NormalizedAiInput,
        user: UserContext,
        state: ConversationState,
    ) -> AiResponse:

        started_at = (
            time.perf_counter()
        )

        try:

            # =================================================
            # 1. INPUT GUARDRAILS
            # =================================================

            input_safety = (
                self.guardrails
                .inspect_input(
                    ai_input
                )
            )

            if (
                input_safety.decision
                !=
                GuardrailDecision.ALLOW
            ):

                response = AiResponse(
                    answer=(
                        input_safety
                        .public_message
                        or
                        "Yêu cầu không thể xử lý."
                    ),
                    route=None,
                    intent=(
                        "Blocked"
                    ),
                    warning=(
                        input_safety
                        .reason_code
                    ),
                    safety=(
                        input_safety
                    ),
                )

                return self._finalize(
                    response,
                    started_at,
                )

            # Attachment bytes/OCR/history are evidence, never instructions or
            # identity for classification. Keep F2's attachment context for
            # generation while resolving only the current user's request.
            original_question = str(
                (ai_input.metadata or {}).get("user_text", ai_input.text)
                or (ai_input.metadata or {}).get("effective_prompt", "")
            )
            routing_input = replace(ai_input, text=original_question)

            
            # =================================================
            # PHASE 08 WORKFLOW ACTION GUARD
            #
            # AVA chỉ advisory.
            # Không được trực tiếp approve / reject / sign /
            # mutate workflow state.
            #
            # Backend ApprovalEngine + WorkflowPolicy
            # mới là source of truth.
            # =================================================

            workflow_advisory = (
                self.workflow_advisory_service
                .analyze(
                    original_question,
                    user,
                )
            )

            if workflow_advisory is not None:

                response = AiResponse(
                    answer=(
                        workflow_advisory.answer
                    ),
                    route=None,
                    intent=(
                        workflow_advisory.intent
                    ),
                    warning=(
                        workflow_advisory.warning
                    ),
                    safety=SafetyResult(),
                )

                response = (
                    self._apply_output_guardrail(
                        response
                    )
                )

                return self._finalize(
                    response,
                    started_at,
                )

            # =================================================
            # 2. CONTEXT RESOLUTION
            # =================================================

            (
                standalone_question,
                resolution_confidence,
                need_clarification,
                clarification_question,
            ) = (
                self.context_resolver
                .resolve_question(
                    ai_input=(
                        routing_input
                    ),
                    state=(
                        state
                    ),
                )
            )

            # =================================================
            # 3. INTENT
            # =================================================

            context_intent = (
                self.intent_classifier
                .classify(
                    original_question=(
                        original_question
                    ),
                    standalone_question=(
                        standalone_question
                    ),
                    resolution_confidence=(
                        resolution_confidence
                    ),
                    need_clarification=(
                        need_clarification
                    ),
                    clarification_question=(
                        clarification_question
                    ),
                )
            )

            attachment_evidence = (
                (ai_input.metadata or {}).get("document_rag_evidence", [])
            )
            if attachment_evidence:
                # Uploaded documents are untrusted evidence, never route instructions.
                # Preserve the higher-priority personal-data and live-SQL classifiers.
                if not context_intent.signals.requires_user_specific_data and not context_intent.signals.requires_current_hrms_value:
                    context_intent.signals.requires_internal_knowledge = True
                    context_intent.intent = "DOCUMENT_RAG"

            # =================================================
            # 4. ROUTER
            # =================================================

            route_decision = (
                self.router.route(
                    context=(
                        context_intent
                    ),
                    user=(
                        user
                    ),
                )
            )

            # =================================================
            # 5. CLARIFICATION
            # =================================================

            if (
                route_decision
                .need_clarification
            ):

                response = AiResponse(
                    answer=(
                        context_intent
                        .clarification_question
                        or
                        "Bạn có thể nói rõ hơn "
                        "câu hỏi được không?"
                    ),
                    route=None,
                    intent=(
                        context_intent
                        .intent
                    ),
                    warning=(
                        "LOW_CONTEXT_CONFIDENCE"
                    ),
                    safety=(
                        SafetyResult()
                    ),
                )

                return self._finalize(
                    response,
                    started_at,
                )

            route = (
                route_decision.route
            )

            # =================================================
            # 6. UNKNOWN ROUTE
            # =================================================

            if route is None:

                response = AiResponse(
                    answer=(
                        "Không xác định được "
                        "nguồn xử lý phù hợp."
                    ),
                    route=None,
                    intent=(
                        context_intent.intent
                    ),
                    warning=(
                        "UNKNOWN_ROUTE"
                    ),
                )

                return self._finalize(
                    response,
                    started_at,
                )

            # =================================================
            # 7. AUTHORIZATION
            #
            # Quan trọng:
            # truyền standalone_question để Authorization
            # kiểm NVxxx / department / scope.
            # =================================================

            route_safety = (
                self.authorization
                .authorize(
                    route=(
                        route
                    ),
                    user=(
                        user
                    ),
                    question=(
                        standalone_question
                    ),
                )
            )

            if (
                route_safety.decision
                !=
                GuardrailDecision.ALLOW
            ):

                response = AiResponse(
                    answer=(
                        route_safety
                        .public_message
                        or
                        "Bạn không có quyền "
                        "thực hiện yêu cầu này."
                    ),
                    route=(
                        route
                    ),
                    intent=(
                        context_intent
                        .intent
                    ),
                    warning=(
                        route_safety
                        .reason_code
                    ),
                    safety=(
                        route_safety
                    ),
                )

                return self._finalize(
                    response,
                    started_at,
                )

            # =================================================
            # 8. EXECUTE ROUTE
            # =================================================

            if (
                route
                == AiRoute.RAG
            ):
                rag_service = self._get_rag_service()
                if attachment_evidence:
                    route_result = rag_service.answer_from_attachment(
                        standalone_question,
                        attachment_evidence,
                    )
                else:
                    route_result = rag_service.answer(
                        standalone_question,
                        user,
                    )

            elif (
                route
                == AiRoute.LIVE_SQL
            ):

                route_result = (
                    self.live_sql_service
                    .answer(
                        standalone_question
                    )
                )

            elif (
                route
                == AiRoute.PERSONAL_DATA
            ):

                route_result = (
                    self.personal_data_service
                    .answer(
                        standalone_question,
                        user,
                    )
                )

            elif (
                route
                == AiRoute.GENERAL_LLM
            ):

                route_result = (
                    self.general_llm_service
                    .answer_with_context(
                        standalone_question,
                        state,
                    )
                )

            else:

                route_result = (
                    RouteResult(
                        route=(
                            AiRoute.GENERAL_LLM
                        ),
                        answer_draft=(
                            "Không xác định được "
                            "nguồn xử lý phù hợp."
                        ),
                        warning=(
                            "UNKNOWN_ROUTE"
                        ),
                        confidence=0.0,
                    )
                )

            # =================================================
            # 9. VERIFY
            # =================================================

            verification = (
                self.verifier
                .verify(
                    route_result
                )
            )

            # =================================================
            # 10. COMPOSE
            # =================================================

            response = (
                self.composer
                .compose(
                    route_result=(
                        route_result
                    ),
                    intent=(
                        context_intent
                        .intent
                    ),
                    verification=(
                        verification
                    ),
                    safety=(
                        route_safety
                    ),
                )
            )

            # =================================================
            # 10.1 OUTPUT GUARDRAIL
            #
            # Phải chạy trước history để secret đã bị mask
            # không bị lưu vào ConversationState.
            # =================================================

            response = (
                self._apply_output_guardrail(
                    response
                )
            )

            # =================================================
            # 10.2 RAG OBSERVABILITY
            # =================================================

            self._log_rag_observability(
                response=response,
                route_result=route_result,
            )

            # =================================================
            # 11. UPDATE STATE
            # =================================================

            state.last_route = (
                route
            )

            state.resolution_confidence = (
                resolution_confidence
            )

            # =================================================
            # 11.1 ACTIVE TOPIC
            # =================================================

            detected_topic = (
                self.context_resolver
                .detect_topic(
                    standalone_question
                )
            )

            if detected_topic:

                state.active_topic = (
                    detected_topic
                )

            # =================================================
            # 11.2 TIME SCOPE
            # =================================================

            detected_time_scope = (
                self.context_resolver
                .time_scope_resolver
                .detect(
                    original_question
                )
            )

            if detected_time_scope:

                state.time_scope = (
                    detected_time_scope
                )

            # =================================================
            # 11.3 ENTITY STATE
            # =================================================

            self._update_entity_state(
                question=(
                    original_question
                ),
                state=(
                    state
                ),
            )

            # =================================================
            # 11.4 HISTORY
            # =================================================

            state.recent_turns.append(
                {
                    "role": (
                        "user"
                    ),
                    "content": (
                        original_question
                    ),
                }
            )

            state.recent_turns.append(
                {
                    "role": (
                        "assistant"
                    ),
                    "content": (
                        response.answer
                    ),
                }
            )

            state.recent_turns = (
                state.recent_turns[-8:]
            )

            # =================================================
            # 12. FINALIZE
            # =================================================

            return self._finalize(
                response,
                started_at,
                route_result=(
                    route_result
                ),
            )

        # =====================================================
        # TOP LEVEL ERROR
        # =====================================================

        except Exception as exc:

            error_type = (
                type(exc).__name__
            )

            response = AiResponse(
                answer=(
                    "Hệ thống AI gặp lỗi trong "
                    "quá trình xử lý yêu cầu."
                ),
                route=None,
                intent=(
                    "SystemError"
                ),
                warning=(
                    "AI_PROCESSING_FAILED"
                ),
            )

            return self._finalize(
                response,
                started_at,
                error_type=(
                    error_type
                ),
            )

