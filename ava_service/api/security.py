from __future__ import annotations

import hmac
import os

from fastapi import Header, HTTPException, status


def require_internal_service_key(
    x_ava_service_key: str | None = Header(default=None),
) -> None:
    """Reject every request that is not authenticated as the ASP.NET gateway."""

    expected = os.getenv("AVA_INTERNAL_SECRET", "").strip()
    if not expected or not x_ava_service_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="AI service authentication required.",
        )

    if not hmac.compare_digest(x_ava_service_key, expected):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="AI service authentication failed.",
        )
