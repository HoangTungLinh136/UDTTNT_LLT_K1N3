import os

# Official Chroma setting.  Set it before importing chromadb so client
# startup telemetry is disabled as well as per-client telemetry.
os.environ.setdefault("ANONYMIZED_TELEMETRY", "False")

import chromadb
from chromadb.config import Settings as ChromaSettings

from config.settings import settings


class VectorStore:

    def __init__(self):
        self.client = chromadb.PersistentClient(
            path=settings.VECTOR_DB_DIR,
            settings=ChromaSettings(
                anonymized_telemetry=False,
            ),
        )

    def get_collection(self):
        try:
            return self.client.get_collection(
                name=settings.VECTOR_COLLECTION
            )
        except Exception:
            return None

    def search(
        self,
        query_embedding: list[float],
        top_k: int | None = None,
    ) -> dict:

        collection = self.get_collection()

        if collection is None:
            return {
                "ids": [[]],
                "documents": [[]],
                "metadatas": [[]],
                "distances": [[]],
            }

        top_k = (
            top_k
            or settings.RAG_CANDIDATE_TOP_K
        )

        result = collection.query(
            query_embeddings=[
                query_embedding
            ],
            n_results=top_k,
            include=[
                "documents",
                "metadatas",
                "distances",
            ],
        )

        return result
