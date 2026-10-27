from enum import Enum


class AiRoute(str, Enum):
    RAG = "RAG"
    LIVE_SQL = "LIVE_SQL"
    PERSONAL_DATA = "PERSONAL_DATA"
    GENERAL_LLM = "GENERAL_LLM"


class InputModality(str, Enum):
    TEXT = "TEXT"
    IMAGE = "IMAGE"
    DOCUMENT = "DOCUMENT"
    VOICE = "VOICE"


class GuardrailDecision(str, Enum):
    ALLOW = "ALLOW"
    SANITIZE = "SANITIZE"
    MASK = "MASK"
    REQUIRE_AUTH = "REQUIRE_AUTH"
    BLOCK = "BLOCK"
    SAFE_FALLBACK = "SAFE_FALLBACK"


class VerificationStatus(str, Enum):
    PASS = "PASS"
    FAIL = "FAIL"
    WARNING = "WARNING"


class SafetyStatus(str, Enum):
    SAFE = "SAFE"
    BLOCKED = "BLOCKED"
    REQUIRES_AUTH = "REQUIRES_AUTH"
    SANITIZED = "SANITIZED"