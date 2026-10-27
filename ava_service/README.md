# AVA internal service

This folder hosts the existing AVA orchestration core behind a small FastAPI
adapter. The service is intended to listen on loopback and be called only by
the ASP.NET HRMS gateway.

## Run locally

1. Create a virtual environment with Python 3.11+ and install `requirements.txt`.
2. Copy `.env.example` to `.env` and configure the variable names documented there.
3. Set the same internal secret in the ASP.NET configuration (`Ava__InternalSecret`)
   and Python (`AVA_INTERNAL_SECRET`).
4. From this directory run:

```powershell
python -m uvicorn api.main:app --host 127.0.0.1 --port 8010
```

Endpoints:

- `GET /health` — liveness/configuration status (does not expose secrets).
- `POST /api/v1/chat` — internal-only chat endpoint; requires
  `X-AVA-Service-Key`.

The browser must call ASP.NET `/api/ai/chat`; it must never call this service
directly or send role/permission fields.

Conversation history is persisted by the ASP.NET application after the
`AddAiConversations` EF migration is applied. The migration is intentionally
not applied by this change; apply it through the normal HRMS deployment gate
after validating the target database.
