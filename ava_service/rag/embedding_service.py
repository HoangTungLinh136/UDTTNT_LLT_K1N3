import os

from sentence_transformers import SentenceTransformer

from config.settings import settings


class EmbeddingService:

    def __init__(self):
        ai_cache = os.getenv("AI_CACHE", "").strip()
        cache_folder = (
            os.path.join(ai_cache, "huggingface", "hub")
            if ai_cache
            else None
        )

        self.model = SentenceTransformer(
            settings.EMBEDDING_MODEL,
            cache_folder=cache_folder,
            local_files_only=bool(cache_folder),
        )

    def embed_query(
        self,
        text: str,
    ) -> list[float]:

        vector = self.model.encode(
            text,
            normalize_embeddings=True,
        )

        return vector.tolist()

    def embed_documents(
        self,
        texts: list[str],
    ) -> list[list[float]]:

        vectors = self.model.encode(
            texts,
            normalize_embeddings=True,
        )

        return vectors.tolist()
