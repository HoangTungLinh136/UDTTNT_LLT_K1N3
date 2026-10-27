from __future__ import annotations

from rag.evidence_selector import (
    EvidenceSelector,
)


def item(
    text: str,
    source: str,
):
    return {
        "document": text,
        "metadata": {
            "source": source,
            "source_type": "DOCUMENT",
            "access_scope": "INTERNAL",
        },
    }


def test_duplicate_removed():

    selector = EvidenceSelector(
        max_items=4,
    )

    evidence = [
        item(
            "Quy định đi muộn áp dụng cho nhân viên.",
            "A",
        ),
        item(
            "Quy định đi muộn áp dụng cho nhân viên.",
            "A",
        ),
    ]

    selected = selector.select(
        evidence
    )

    assert len(selected) == 1


def test_source_diversity():

    selector = EvidenceSelector(
        max_items=4,
        max_per_source=2,
    )

    evidence = [
        item(
            "Nội dung tài liệu A số một đủ dài.",
            "A",
        ),
        item(
            "Nội dung tài liệu A số hai đủ dài.",
            "A",
        ),
        item(
            "Nội dung tài liệu A số ba đủ dài.",
            "A",
        ),
        item(
            "Nội dung tài liệu B số một đủ dài.",
            "B",
        ),
    ]

    selected = selector.select(
        evidence
    )

    sources = [
        x["metadata"]["source"]
        for x in selected
    ]

    assert sources.count("A") == 2
    assert sources.count("B") == 1


def test_short_chunk_removed():

    selector = EvidenceSelector(
        min_document_chars=20,
    )

    evidence = [
        item(
            "quá ngắn",
            "A",
        ),
        item(
            "Đây là nội dung đầy đủ và đủ dài để sử dụng.",
            "B",
        ),
    ]

    selected = selector.select(
        evidence
    )

    assert len(selected) == 1
    assert (
        selected[0]["metadata"]["source"]
        == "B"
    )


def test_max_items():

    selector = EvidenceSelector(
        max_items=2,
        max_per_source=2,
    )

    evidence = [
        item(
            "Nội dung evidence thứ nhất đủ dài.",
            "A",
        ),
        item(
            "Nội dung evidence thứ hai đủ dài.",
            "B",
        ),
        item(
            "Nội dung evidence thứ ba đủ dài.",
            "C",
        ),
    ]

    selected = selector.select(
        evidence
    )

    assert len(selected) == 2


def test_order_preserved():

    selector = EvidenceSelector()

    evidence = [
        item(
            "Evidence có relevance cao nhất và đủ dài.",
            "A",
        ),
        item(
            "Evidence có relevance thứ hai và đủ dài.",
            "B",
        ),
    ]

    selected = selector.select(
        evidence
    )

    assert (
        selected[0]["metadata"]["source"]
        == "A"
    )


def test_empty_evidence():

    selector = EvidenceSelector()

    assert selector.select([]) == []


def test_unknown_source_still_limited():

    selector = EvidenceSelector(
        max_items=5,
        max_per_source=1,
    )

    evidence = [
        {
            "document":
                "Nội dung unknown source thứ nhất đủ dài.",
            "metadata": {},
        },
        {
            "document":
                "Nội dung unknown source thứ hai đủ dài.",
            "metadata": {},
        },
    ]

    selected = selector.select(
        evidence
    )

    assert len(selected) == 1
