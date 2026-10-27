from __future__ import annotations
import json
import re
import threading
import time
from pathlib import Path

class GeneralWebCache:
    _lock = threading.Lock()

    def __init__(self, path: str = "data/general_web_cache.json"):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        if not self.path.exists():
            self.path.write_text("{}", encoding="utf-8")

    @staticmethod
    def normalize_key(question: str) -> str:
        return re.sub(r"\s+", " ", (question or "").strip().lower())

    def _read_all(self) -> dict:
        try:
            data = json.loads(self.path.read_text(encoding="utf-8"))
            return data if isinstance(data, dict) else {}
        except Exception:
            return {}

    def _write_all(self, data: dict) -> None:
        tmp = self.path.with_suffix(".tmp")
        tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        tmp.replace(self.path)

    def get(self, question: str, *, ttl_seconds: int, allow_stale: bool = False) -> dict | None:
        key = self.normalize_key(question)
        with self._lock:
            data = self._read_all()
        item = data.get(key)
        if not isinstance(item, dict):
            return None
        created_at = float(item.get("created_at", 0))
        age = max(0.0, time.time() - created_at)
        item = dict(item)
        item["age_seconds"] = age
        if allow_stale or age <= ttl_seconds:
            return item
        return None

    def set(self, question: str, answer: str) -> None:
        key = self.normalize_key(question)
        with self._lock:
            data = self._read_all()
            data[key] = {"answer": answer, "created_at": time.time()}
            self._write_all(data)
