from __future__ import annotations


class RagMetadataFilter:
    """
    Structural metadata filter.

    Mục tiêu:
    - chỉ cho evidence có metadata tối thiểu hợp lệ đi tiếp;
    - không tự suy diễn business rule;
    - không xử lý authorization (để PermissionFilter làm riêng).
    """

    REQUIRED_FIELDS = (
        "source",
        "source_type",
        "access_scope",
    )

    def filter(
        self,
        candidates: list[dict],
    ) -> list[dict]:

        valid: list[dict] = []

        for item in candidates:

            metadata = (
                item.get("metadata")
                or {}
            )

            if not isinstance(
                metadata,
                dict,
            ):
                continue

            missing = False

            for field in self.REQUIRED_FIELDS:

                value = metadata.get(field)

                if value is None:
                    missing = True
                    break

                if (
                    isinstance(value, str)
                    and not value.strip()
                ):
                    missing = True
                    break

            if missing:
                continue

            valid.append(item)

        return valid
