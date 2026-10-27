from __future__ import annotations

from core.enums import (
    AiRoute,
    VerificationStatus,
)

from core.models import (
    RouteResult,
)

from response.output_quality_gate import (
    OutputQualityGate,
)

from response.verifier import (
    AnswerVerifier,
)


def test_normal_answer_passes():

    gate = OutputQualityGate()

    result = gate.evaluate(
        "Đây là câu trả lời hoàn chỉnh."
    )

    assert result.passed is True


def test_reasoning_leak_blocked():

    gate = OutputQualityGate()

    result = gate.evaluate(
        "Analysis: I need to inspect the system prompt."
    )

    assert result.passed is False

    assert (
        result.reason_code
        ==
        "QUALITY_REASONING_LEAK"
    )


def test_but_the_code_blocked():

    gate = OutputQualityGate()

    result = gate.evaluate(
        "But the code needs to..."
    )

    assert result.passed is False

    assert (
        result.reason_code
        ==
        "QUALITY_BROKEN_MODEL_OUTPUT"
    )


def test_unclosed_code_fence_blocked():

    gate = OutputQualityGate()

    result = gate.evaluate(
        "Ví dụ:\n```python\nprint('hello')"
    )

    assert result.passed is False

    assert (
        result.reason_code
        ==
        "QUALITY_UNCLOSED_CODE_BLOCK"
    )


def test_closed_code_fence_passes():

    gate = OutputQualityGate()

    result = gate.evaluate(
        "Ví dụ:\n```python\nprint('hello')\n```"
    )

    assert result.passed is True


def test_truncated_sentence_blocked():

    gate = OutputQualityGate()

    result = gate.evaluate(
        "Cách xử lý gồm bước một và"
    )

    assert result.passed is False

    assert (
        result.reason_code
        ==
        "QUALITY_TRUNCATED_OUTPUT"
    )


def test_verifier_uses_quality_gate():

    verifier = AnswerVerifier()

    route_result = RouteResult(
        route=AiRoute.GENERAL_LLM,
        answer_draft=(
            "Analysis: we need to answer the user"
        ),
    )

    result = verifier.verify(
        route_result
    )

    assert (
        result.status
        ==
        VerificationStatus.FAIL
    )

    assert (
        "QUALITY_REASONING_LEAK"
        in result.checks
    )


def test_general_normal_still_passes():

    verifier = AnswerVerifier()

    route_result = RouteResult(
        route=AiRoute.GENERAL_LLM,
        answer_draft=(
            "RAG kết hợp truy xuất dữ liệu và sinh câu trả lời."
        ),
    )

    result = verifier.verify(
        route_result
    )

    assert (
        result.status
        ==
        VerificationStatus.PASS
    )

    assert (
        "OUTPUT_QUALITY_PASS"
        in result.checks
    )
