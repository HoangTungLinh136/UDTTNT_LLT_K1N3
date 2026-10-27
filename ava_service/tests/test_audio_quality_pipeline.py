from types import SimpleNamespace

from preprocessing.local_multimodal import (
    _prefer_enhanced_transcript,
    _weighted_segment_confidence,
)


def test_segment_confidence_is_weighted_by_duration():
    segments = [
        SimpleNamespace(t0=0, t1=100, probability=0.9),
        SimpleNamespace(t0=100, t1=400, probability=0.5),
    ]

    assert _weighted_segment_confidence(segments) == 0.6


def test_enhanced_transcript_must_score_measurably_better():
    assert _prefer_enhanced_transcript(
        "xin chào",
        0.50,
        "xin chào AVA",
        0.54,
    )
    assert not _prefer_enhanced_transcript(
        "xin chào",
        0.50,
        "nội dung khác",
        0.51,
    )


def test_missing_enhanced_text_never_replaces_original():
    assert not _prefer_enhanced_transcript(
        "nội dung gốc",
        0.40,
        "",
        0.95,
    )
