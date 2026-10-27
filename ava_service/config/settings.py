import os

from dotenv import load_dotenv

load_dotenv()


class Settings:
    # =========================
    # LOCAL MODEL RUNTIME
    # =========================
    OLLAMA_BASE_URL = os.getenv(
        "OLLAMA_BASE_URL",
        "http://localhost:11434",
    ).strip().rstrip("/")
    OLLAMA_TIMEOUT_SECONDS = float(
        os.getenv("OLLAMA_TIMEOUT_SECONDS", "120")
    )
    OLLAMA_TEXT_MODEL = os.getenv(
        "OLLAMA_TEXT_MODEL",
        "qwen3:4b-instruct",
    ).strip()

    # Model identifiers are not provider names. Generation is always local Ollama.
    LLM_MODEL = os.getenv(
        "LLM_MODEL",
        "openai/gpt-oss-20b",
    )
    VISION_MODEL = os.getenv(
        "VISION_MODEL",
        "qwen2.5vl:3b",
    ).strip()
    # RTX 3050 4 GB on the target workstation crashes the qwen25vl hybrid
    # runner. Vision stays on CPU; the text model can still use GPU normally.
    OLLAMA_VISION_NUM_GPU = int(
        os.getenv("OLLAMA_VISION_NUM_GPU", "0")
    )
    GENERAL_LLM_PROVIDER = "ollama"
    RAG_LLM_PROVIDER = "ollama"
    DIAGNOSTICS_ENABLED = os.getenv(
        "AVA_DIAGNOSTICS_ENABLED", "false"
    ).strip().lower() in {"1", "true", "yes", "on"}
    # Audio is first transcribed as-is. A conservative speech enhancement
    # pass is attempted only when Whisper's segment confidence is below this
    # threshold, and is accepted only when it scores better.
    STT_CONFIDENCE_THRESHOLD = float(
        os.getenv("AVA_STT_CONFIDENCE_THRESHOLD", "0.58")
    )
    STT_ADAPTIVE_ENHANCEMENT = os.getenv(
        "AVA_STT_ADAPTIVE_ENHANCEMENT", "true"
    ).strip().lower() in {"1", "true", "yes", "on"}

    # =========================
    # SQL SERVER
    # =========================
    SQL_SERVER = os.getenv("SQL_SERVER", r".\SQLEXPRESS")
    SQL_DATABASE = os.getenv("SQL_DATABASE", "HRManagementDB")
    SQL_DRIVER = os.getenv("SQL_DRIVER", "").strip()

    SQL_CONNECTION_STRING = (
        "DRIVER={ODBC Driver 18 for SQL Server};"
        f"SERVER={SQL_SERVER};"
        f"DATABASE={SQL_DATABASE};"
        "Trusted_Connection=yes;"
        "Encrypt=no;"
        "TrustServerCertificate=yes;"
    )

    # =========================
    # RAG
    # =========================
    VECTOR_DB_DIR = os.getenv("VECTOR_DB_DIR", "vector_db")
    VECTOR_COLLECTION = os.getenv(
        "VECTOR_COLLECTION",
        "hrms_knowledge"
    )

    EMBEDDING_MODEL = os.getenv(
        "EMBEDDING_MODEL",
        "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
    )

    # Candidate retrieval trước rerank.
    RAG_CANDIDATE_TOP_K = int(
        os.getenv("RAG_CANDIDATE_TOP_K", "10")
    )

    # Sau rerank chỉ lấy evidence tốt nhất.
    RAG_FINAL_TOP_K = int(
        os.getenv("RAG_FINAL_TOP_K", "4")
    )

    # Tạm thời giữ baseline.
    # Sau này tune bằng validation set.
    RAG_MAX_DISTANCE = float(
        os.getenv("RAG_MAX_DISTANCE", "0.9")
    )

    # =========================
    # CONTEXT
    # =========================
    MAX_HISTORY_MESSAGES = int(
        os.getenv("MAX_HISTORY_MESSAGES", "8")
    )

    CONTEXT_CLARIFY_THRESHOLD = float(
        os.getenv("CONTEXT_CLARIFY_THRESHOLD", "0.60")
    )

    ROUTE_CONFIDENCE_THRESHOLD = float(
        os.getenv("ROUTE_CONFIDENCE_THRESHOLD", "0.60")
    )


settings = Settings()

