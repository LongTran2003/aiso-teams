"""
orchestrator.py – AI Orchestration Layer (Gemini API edition).

Chuyển đổi từ Azure OpenAI → Google Gemini (google-genai SDK).
Kiến trúc giữ nguyên: nạp system prompt, load dynamic function schemas
từ /functions, gọi Gemini với Function Calling, parse kết quả về
đúng JSON contract mà Backend (.NET) kỳ vọng.
"""

from __future__ import annotations

import json
import logging
import os
import re
import uuid
from pathlib import Path
from typing import Any

from dotenv import load_dotenv
from google import genai
from google.genai import types

from schemas import ChatRequest, ChatResponse, ToolCall

# ---------------------------------------------------------------------------
# Bootstrap
# ---------------------------------------------------------------------------

load_dotenv()

logger = logging.getLogger(__name__)

_BASE_DIR = Path(__file__).resolve().parent
_SYSTEM_PROMPT_PATH = _BASE_DIR / "prompts" / "system_prompt.txt"
_FUNCTIONS_DIR = _BASE_DIR / "functions"

# Gemini credentials (from .env)
_GEMINI_API_KEY: str = os.getenv("GEMINI_API_KEY", "")
_GEMINI_MODEL: str = os.getenv("GEMINI_MODEL_NAME", "gemini-1.5-flash")


# ---------------------------------------------------------------------------
# Helpers – file loading
# ---------------------------------------------------------------------------


def _load_system_prompt() -> str:
    """Đọc system prompt từ disk; fallback về chuỗi mặc định nếu không tìm thấy."""
    try:
        return _SYSTEM_PROMPT_PATH.read_text(encoding="utf-8").strip()
    except FileNotFoundError:
        logger.warning("system_prompt.txt not found – using built-in default.")
        return "You are a helpful AI assistant."


def _json_schema_to_gemini_function(raw: dict[str, Any]) -> types.FunctionDeclaration:
    """
    Chuyển đổi JSON Schema OpenAI format → types.FunctionDeclaration của Gemini SDK.

    OpenAI format (lưu trong /functions/*.json):
        {
          "type": "function",
          "function": {
            "name": "...",
            "description": "...",
            "parameters": { "type": "object", "properties": {...}, "required": [...] }
          }
        }

    Gemini FunctionDeclaration nhận trực tiếp name, description, parameters
    dưới dạng types.Schema.
    """
    fn = raw.get("function", raw)  # hỗ trợ cả 2 format: wrapped & flat
    name: str = fn["name"]
    description: str = fn.get("description", "")
    params_raw: dict[str, Any] = fn.get("parameters", {})

    # Xây dựng Schema cho từng property
    properties: dict[str, types.Schema] = {}
    for prop_name, prop_def in params_raw.get("properties", {}).items():
        prop_type = _map_json_type(prop_def.get("type", "string"))
        properties[prop_name] = types.Schema(
            type=prop_type,
            description=prop_def.get("description", ""),
        )

    parameters_schema = types.Schema(
        type=types.Type.OBJECT,
        properties=properties,
        required=params_raw.get("required", []),
    )

    return types.FunctionDeclaration(
        name=name,
        description=description,
        parameters=parameters_schema,
    )


def _map_json_type(json_type: str) -> types.Type:
    """Map JSON Schema primitive types → Gemini types.Type enum."""
    return {
        "string": types.Type.STRING,
        "number": types.Type.NUMBER,
        "integer": types.Type.INTEGER,
        "boolean": types.Type.BOOLEAN,
        "array": types.Type.ARRAY,
        "object": types.Type.OBJECT,
    }.get(json_type.lower(), types.Type.STRING)


def _load_gemini_tools() -> list[types.Tool] | None:
    """
    Quét thư mục /functions, load từng *.json, chuyển hóa sang
    types.FunctionDeclaration, gộp tất cả vào một types.Tool duy nhất.
    Trả về None nếu không có file nào (Gemini không cần tools=[]).
    """
    if not _FUNCTIONS_DIR.exists():
        logger.warning("functions/ directory not found – no tools loaded.")
        return None

    declarations: list[types.FunctionDeclaration] = []
    for path in sorted(_FUNCTIONS_DIR.glob("*.json")):
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
            decl = _json_schema_to_gemini_function(raw)
            declarations.append(decl)
            logger.debug("Loaded Gemini FunctionDeclaration: %s", decl.name)
        except (KeyError, json.JSONDecodeError, OSError) as exc:
            logger.error("Failed to load %s: %s", path.name, exc)

    return [types.Tool(function_declarations=declarations)] if declarations else None


# ---------------------------------------------------------------------------
# Helpers – misc
# ---------------------------------------------------------------------------


def _is_real_key_configured() -> bool:
    """True khi GEMINI_API_KEY trông như key thật (không phải placeholder)."""
    return bool(_GEMINI_API_KEY) and "your-" not in _GEMINI_API_KEY


def _function_name_to_intent(name: str) -> str:
    """CamelCase → snake_case, ví dụ: CheckOrderStatus → check_order_status."""
    s1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


# ---------------------------------------------------------------------------
# Mock fallback (không cần key thật)
# ---------------------------------------------------------------------------


def _mock_response(user_message: str) -> ChatResponse:
    """
    Phản hồi giả lập để dev/test không cần key thật.
    Logic nhận diện intent đơn giản dựa trên keyword.
    """
    logger.info("Using MOCK Gemini response (no real API key configured).")

    tool_calls: list[ToolCall] = []
    intent = "general_query"
    reply = f"[MOCK] Tôi đã nhận được tin nhắn của bạn: '{user_message}'"

    lower = user_message.lower()
    if any(kw in lower for kw in ("đơn hàng", "order", "ord-", "trạng thái")):
        intent = "check_order_status"
        match = re.search(r"(ord-\w+)", lower)
        order_id = match.group(1).upper() if match else "UNKNOWN"

        tool_calls.append(
            ToolCall(
                id="mock_call_001",
                function_name="CheckOrderStatus",
                arguments={"order_id": order_id},
            )
        )
        reply = (
            f"[MOCK] Tôi sẽ kiểm tra trạng thái đơn hàng **{order_id}** cho bạn. "
            "Vui lòng chờ hệ thống SAP phản hồi."
        )

    return ChatResponse(reply=reply, intent=intent, tool_calls=tool_calls)


# ---------------------------------------------------------------------------
# AIOrchestrator class – Gemini
# ---------------------------------------------------------------------------


class AIOrchestrator:
    """
    Quản lý toàn bộ lifecycle của một lần gọi Gemini:
      1. Khởi tạo google.genai.Client
      2. Nạp system prompt + dynamic function tools
      3. Gửi message và parse function_calls từ response
      4. Trả về ChatResponse đúng contract với Backend .NET
    """

    def __init__(self) -> None:
        self._client = genai.Client(api_key=_GEMINI_API_KEY)
        self._system_prompt = _load_system_prompt()
        self._tools = _load_gemini_tools()
        logger.info(
            "AIOrchestrator initialized | model=%s | tools_loaded=%s",
            _GEMINI_MODEL,
            self._tools is not None,
        )

    def process(self, user_message: str) -> ChatResponse:
        """
        Gửi user_message tới Gemini và parse kết quả.

        Gemini trả về một GenerateContentResponse.
        Nếu model muốn gọi hàm → response.function_calls là list[FunctionCall].
        Mỗi FunctionCall có .name (str) và .args (dict).
        """
        config_kwargs: dict[str, Any] = {
            "system_instruction": self._system_prompt,
        }
        if self._tools:
            config_kwargs["tools"] = self._tools

        config = types.GenerateContentConfig(**config_kwargs)

        try:
            response = self._client.models.generate_content(
                model=_GEMINI_MODEL,
                contents=user_message,
                config=config,
            )
        except Exception as exc:
            logger.error("Gemini API call failed: %s", exc)
            raise

        return self._parse_response(response, user_message)

    # ── Response parser ────────────────────────────────────────────────────

    def _parse_response(
        self,
        response: Any,
        user_message: str,
    ) -> ChatResponse:
        """
        Bóc tách kết quả từ GenerateContentResponse:

        - Nếu có function_calls  → is_function_call=True, trích xuất name + args
        - Nếu chỉ có text        → general_query, tool_calls=[]
        """
        parsed_tool_calls: list[ToolCall] = []
        intent = "general_query"
        reply_text = ""

        # Lấy phần text (nếu có) từ candidate đầu tiên
        try:
            reply_text = response.text or ""
        except (AttributeError, ValueError):
            # response.text raise ValueError nếu response chứa function_calls
            reply_text = ""

        # Bóc tách function_calls
        # response.function_calls trả về list[types.FunctionCall] hoặc []
        function_calls = getattr(response, "function_calls", None) or []

        for fc in function_calls:
            fn_name: str = fc.name
            # fc.args là proto MapComposite – ép sang dict thuần Python
            args: dict[str, Any] = dict(fc.args) if fc.args else {}

            parsed_tool_calls.append(
                ToolCall(
                    id=str(uuid.uuid4()),  # Gemini không trả id → tự sinh
                    function_name=fn_name,
                    arguments=args,
                )
            )

        if parsed_tool_calls:
            intent = _function_name_to_intent(parsed_tool_calls[0].function_name)
            if not reply_text:
                reply_text = "Đang thực thi hàm: " + ", ".join(
                    tc.function_name for tc in parsed_tool_calls
                )

        return ChatResponse(
            reply=reply_text,
            intent=intent,
            tool_calls=parsed_tool_calls,
        )


# ---------------------------------------------------------------------------
# Singleton instance (lazy)
# ---------------------------------------------------------------------------

_orchestrator_instance: AIOrchestrator | None = None


def _get_orchestrator() -> AIOrchestrator:
    """Trả về singleton AIOrchestrator, khởi tạo lần đầu khi cần."""
    global _orchestrator_instance
    if _orchestrator_instance is None:
        _orchestrator_instance = AIOrchestrator()
    return _orchestrator_instance


# ---------------------------------------------------------------------------
# Public API – entry point từ main.py (giữ nguyên signature)
# ---------------------------------------------------------------------------


def process_user_message(request: ChatRequest) -> ChatResponse:
    """
    Entry point được gọi từ FastAPI route handler.
    Signature không đổi → Backend .NET không bị ảnh hưởng.

    - Có GEMINI_API_KEY thật → gọi Gemini API
    - Không có key          → trả mock response
    """
    if _is_real_key_configured():
        try:
            return _get_orchestrator().process(request.user_message)
        except Exception as exc:
            logger.error("Gemini orchestration error: %s – raising exception.", exc)
            raise
    else:
        return _mock_response(request.user_message)
