"""
main.py – FastAPI application entry point.

Start the server:
    uvicorn main:app --reload --port 8000
"""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager
from typing import AsyncIterator

from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from orchestrator import process_user_message
from schemas import ChatRequest, ChatResponse

# ---------------------------------------------------------------------------
# Bootstrap
# ---------------------------------------------------------------------------

load_dotenv()

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s  %(name)s – %(message)s",
)
logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Lifespan hooks
# ---------------------------------------------------------------------------


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    logger.info("🚀  AI Orchestration Service starting up …")
    yield
    logger.info("🛑  AI Orchestration Service shutting down …")


# ---------------------------------------------------------------------------
# FastAPI app
# ---------------------------------------------------------------------------

app = FastAPI(
    title="AI Orchestration Microservice",
    description=(
        "REST API quản lý luồng hội thoại với Azure OpenAI "
        "và tích hợp Function Calling cho hệ thống SAP."
    ),
    version="1.0.0",
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_url="/openapi.json",
)

# ── CORS ─────────────────────────────────────────────────────────────────────
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Thu hẹp lại trong môi trường production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ---------------------------------------------------------------------------
# Routes
# ---------------------------------------------------------------------------


@app.get("/", tags=["Health"])
async def root() -> dict[str, str]:
    """Root health-check."""
    return {"status": "ok", "service": "AI Orchestration Microservice"}


@app.get("/health", tags=["Health"])
async def health_check() -> dict[str, str]:
    """Liveness probe cho Kubernetes / load-balancer."""
    return {"status": "healthy"}


@app.post(
    "/api/v1/orchestrate",
    response_model=ChatResponse,
    status_code=status.HTTP_200_OK,
    summary="Orchestrate a chat message through Azure OpenAI",
    description=(
        "Nhận tin nhắn người dùng, bổ sung system prompt và SAP function schemas, "
        "gọi Azure OpenAI (hoặc mock local), và trả về phản hồi kèm tool_calls."
    ),
    tags=["Orchestration"],
)
async def orchestrate(payload: ChatRequest) -> ChatResponse:
    """
    Endpoint chính.

    - **user_message**: Tin nhắn thô từ người dùng cuối.
    - Trả về **ChatResponse** gồm `reply`, `intent`, và `tool_calls`.
    """
    logger.info("Received orchestration request: %.120s …", payload.user_message)

    try:
        response = process_user_message(payload)
    except Exception as exc:
        logger.exception("Unexpected error during orchestration: %s", exc)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Orchestration failed: {exc}",
        ) from exc

    logger.info(
        "Response – intent=%s  tool_calls=%d",
        response.intent,
        len(response.tool_calls),
    )
    return response


# ---------------------------------------------------------------------------
# Global error handler
# ---------------------------------------------------------------------------


@app.exception_handler(Exception)
async def global_exception_handler(request, exc: Exception) -> JSONResponse:
    logger.error("Unhandled exception: %s", exc)
    return JSONResponse(
        status_code=500,
        content={"detail": "Internal server error. Kiểm tra logs để biết chi tiết."},
    )
