from __future__ import annotations

from core.models import UserContext


class RagPermissionFilter:
    """
    Permission filter cho RAG evidence.

    access_scope hỗ trợ:
    - PUBLIC
    - INTERNAL
    - ROLE:<role>
    - PERMISSION:<permission>
    - DEPARTMENT:<department id/name>

    Quy tắc hiện tại:
    - PUBLIC: cho phép mọi request.
    - INTERNAL: bắt buộc authenticated user.
    - scope không nhận diện được: deny-by-default.
    """

    def _normalize_set(
        self,
        values: list[str] | None,
    ) -> set[str]:

        return {
            str(value).strip().lower()
            for value in (values or [])
            if str(value).strip()
        }

    def is_allowed(
        self,
        metadata: dict,
        user: UserContext | None,
    ) -> bool:

        raw_scope = (
            metadata.get("access_scope")
            or ""
        )

        scope = str(raw_scope).strip()

        if not scope:
            return False

        scope_upper = scope.upper()

        if scope_upper == "PUBLIC":
            return True

        if scope_upper == "INTERNAL":
            return bool(
                user
                and user.is_authenticated
            )

        if not user or not user.is_authenticated:
            return False

        if scope_upper.startswith("ROLE:"):

            required = (
                scope.split(":", 1)[1]
                .strip()
                .lower()
            )

            return required in self._normalize_set(
                user.roles
            )

        if scope_upper.startswith(
            "PERMISSION:"
        ):

            required = (
                scope.split(":", 1)[1]
                .strip()
                .lower()
            )

            return required in self._normalize_set(
                user.permissions
            )

        if scope_upper.startswith(
            "DEPARTMENT:"
        ):

            required = (
                scope.split(":", 1)[1]
                .strip()
                .lower()
            )

            allowed_departments = (
                self._normalize_set(
                    user.department_ids
                )
                | self._normalize_set(
                    user.department_names
                )
            )

            return required in allowed_departments

        # Unknown scope => deny by default.
        return False

    def filter(
        self,
        candidates: list[dict],
        user: UserContext | None,
    ) -> list[dict]:

        return [
            item
            for item in candidates
            if self.is_allowed(
                item.get("metadata") or {},
                user,
            )
        ]
