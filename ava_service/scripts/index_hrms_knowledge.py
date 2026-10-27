from __future__ import annotations

import json
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from config.settings import settings
from rag.embedding_service import EmbeddingService
from rag.vector_store import VectorStore


KNOWLEDGE_FILE = ROOT / "knowledge" / "hrms_knowledge.json"


def _metadata(item: dict) -> dict[str, str | int | float | bool]:
    excluded = {"id", "content"}
    result: dict[str, str | int | float | bool] = {}

    for key, value in item.items():
        if key in excluded or value is None:
            continue
        if isinstance(value, (str, int, float, bool)):
            result[key] = value
        else:
            result[key] = json.dumps(value, ensure_ascii=False)

    return result


def main() -> int:
    items = json.loads(KNOWLEDGE_FILE.read_text(encoding="utf-8"))
    if not isinstance(items, list) or not items:
        raise RuntimeError("Knowledge source is empty or invalid.")

    ids = [str(item["id"]) for item in items]
    documents = [str(item["content"]).strip() for item in items]
    if any(not document for document in documents):
        raise RuntimeError("Knowledge source contains an empty document.")

    embeddings = EmbeddingService().embed_documents(documents)
    store = VectorStore()
    collection = store.client.get_or_create_collection(
        name=settings.VECTOR_COLLECTION,
        metadata={"hnsw:space": "cosine"},
    )
    collection.upsert(
        ids=ids,
        documents=documents,
        embeddings=embeddings,
        metadatas=[_metadata(item) for item in items],
    )

    print(f"Indexed {collection.count()} knowledge documents.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
