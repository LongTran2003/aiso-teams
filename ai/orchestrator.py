"""
orchestrator.py – AI Orchestration Layer (Groq API edition).

Chuyển đổi từ Google Gemini → Groq API (OpenAI-compatible SDK).
Kiến trúc giữ nguyên: nạp system prompt, load dynamic function schemas
từ /functions, gọi Groq với Function Calling, parse kết quả về
đúng JSON contract mà Backend (.NET) kỳ vọng.
"""

from __future__ import annotations

import json
import logging
import os
import re
from pathlib import Path
from typing import Any

from dotenv import load_dotenv
from openai import OpenAI
import openai

from schemas import ChatRequest, ChatResponse, ToolCall

# ---------------------------------------------------------------------------
# Bootstrap
# ---------------------------------------------------------------------------

load_dotenv()

logger = logging.getLogger(__name__)

_BASE_DIR = Path(__file__).resolve().parent
_SYSTEM_PROMPT_PATH = _BASE_DIR / "prompts" / "system_prompt.txt"
_FUNCTIONS_DIR = _BASE_DIR / "functions"

# Groq credentials (from .env)
_GROQ_API_KEY: str = os.getenv("GROQ_API_KEY", "")
_GROQ_MODEL: str = os.getenv("GROQ_MODEL", "llama-3.1-8b-instant")


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


def _load_groq_tools() -> list[dict[str, Any]] | None:
    """
    Quét thư mục /functions, load từng *.json, chuyển hóa sang
    OpenAI tool format. Trả về None nếu không có file nào.
    """
    if not _FUNCTIONS_DIR.exists():
        logger.warning("functions/ directory not found – no tools loaded.")
        return None

    tools: list[dict[str, Any]] = []
    for path in sorted(_FUNCTIONS_DIR.glob("*.json")):
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
            # Chuẩn hóa về format OpenAI tool: {"type": "function", "function": {...}}
            if "type" in raw and raw["type"] == "function":
                tool = raw
            else:
                # Nếu là flat format trong file json
                tool = {
                    "type": "function",
                    "function": {
                        "name": raw["name"],
                        "description": raw.get("description", ""),
                        "parameters": raw.get("parameters", {}),
                    },
                }
            tools.append(tool)
            logger.debug("Loaded Groq Tool: %s", tool["function"]["name"])
        except (KeyError, json.JSONDecodeError, OSError) as exc:
            logger.error("Failed to load %s: %s", path.name, exc)

    return tools if tools else None


# ---------------------------------------------------------------------------
# Helpers – misc
# ---------------------------------------------------------------------------


def _is_real_key_configured() -> bool:
    """True khi GROQ_API_KEY trông như key thật (không phải placeholder)."""
    return bool(_GROQ_API_KEY) and "your-" not in _GROQ_API_KEY


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
    logger.info("Using MOCK Groq response (no real API key configured).")

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
# AIOrchestrator class – Groq (OpenAI Client)
# ---------------------------------------------------------------------------


class AIOrchestrator:
    """
    Quản lý toàn bộ lifecycle của một lần gọi Groq:
      1. Khởi tạo openai.OpenAI Client trỏ tới Groq endpoint
      2. Nạp system prompt + dynamic function tools
      3. Gửi message và parse tool_calls từ response
      4. Trả về ChatResponse đúng contract với Backend .NET
    """

    def __init__(self) -> None:
        self._client = OpenAI(
            api_key=_GROQ_API_KEY, base_url="https://api.groq.com/openai/v1"
        )
        self._system_prompt = _load_system_prompt()
        self._tools = _load_groq_tools()
        logger.info(
            "AIOrchestrator initialized | model=%s | tools_loaded=%s",
            _GROQ_MODEL,
            self._tools is not None,
        )

    def process(self, user_message: str) -> ChatResponse:
        """
        Gửi user_message tới Groq và parse kết quả.
        """
        messages = [
            {"role": "system", "content": self._system_prompt},
            {"role": "user", "content": user_message},
        ]

        kwargs: dict[str, Any] = {
            "model": _GROQ_MODEL,
            "messages": messages,
            "temperature": 0.1,
        }
        if self._tools:
            kwargs["tools"] = self._tools
            kwargs["tool_choice"] = "auto"

        max_retries = 6
        initial_backoff = 3
        import time

        last_exc = None
        for attempt in range(max_retries):
            try:
                response = self._client.chat.completions.create(**kwargs)
                return self._parse_response(response, user_message)
            except openai.RateLimitError as exc:
                last_exc = exc
                sleep_time = initial_backoff * (2**attempt)
                logger.warning(
                    "Groq API Rate Limit hit. Retrying in %s seconds...", sleep_time
                )
                time.sleep(sleep_time)
            except openai.APIConnectionError as exc:
                last_exc = exc
                sleep_time = initial_backoff * (2**attempt)
                logger.warning(
                    "Groq API Connection error. Retrying in %s seconds...", sleep_time
                )
                time.sleep(sleep_time)
            except openai.APIStatusError as exc:
                if exc.status_code >= 500:
                    last_exc = exc
                    sleep_time = initial_backoff * (2**attempt)
                    logger.warning(
                        "Groq API Server Error (%s). Retrying in %s seconds...",
                        exc.status_code,
                        sleep_time,
                    )
                    time.sleep(sleep_time)
                else:
                    logger.error(
                        "Groq API returned status code %s: %s", exc.status_code, exc
                    )
                    raise
            except openai.AuthenticationError as exc:
                logger.error("Groq API Authentication Error (Invalid API Key): %s", exc)
                raise
            except openai.BadRequestError as exc:
                logger.error(
                    "Groq API Bad Request (Invalid parameters/schema): %s", exc
                )
                recovered = self._recover_failed_generation(exc, user_message)
                if recovered is not None:
                    logger.info(
                        "Successfully recovered from Groq API Bad Request (tool_use_failed)."
                    )
                    return recovered
                raise
            except Exception as exc:
                logger.error("Groq API unexpected error: %s", exc)
                raise

        if last_exc:
            raise last_exc
        raise Exception("Groq API call failed after max retries.")

    def _validate_and_build_response(
        self, fn_name: str, args: dict, user_message: str
    ) -> ChatResponse | None:
        """
        Kiểm tra tính hợp lệ của tham số (bao gồm cả việc tự ý suy diễn order_id).
        Nếu không hợp lệ, trả về ChatResponse tương ứng (yêu cầu người dùng cung cấp thông tin).
        Nếu hợp lệ, trả về None.
        """
        order_id = args.get("order_id")

        # 1. Phát hiện ảo tưởng order_id (không xuất hiện trong user_message)
        if order_id and str(order_id).lower() not in user_message.lower():
            logger.warning(
                "Hallucinated order_id detected: %s not in query: %s",
                order_id,
                user_message,
            )
            if fn_name in ("CheckOrderStatus", "ReleaseOrder", "ForwardOrder"):
                return ChatResponse(
                    reply="Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện.",
                    intent="general_query",
                    tool_calls=[],
                )
            else:  # RejectOrder
                return ChatResponse(
                    reply="Tôi chưa xác định được đơn hàng nào. Vui lòng cho tôi mã đơn hàng.",
                    intent="general_query",
                    tool_calls=[],
                )

        # 2. Phát hiện ảo tưởng forward_to_user (không xuất hiện trong user_message)
        forward_to_user = args.get("forward_to_user")
        if forward_to_user and str(forward_to_user).lower() not in user_message.lower():
            logger.warning(
                "Hallucinated forward_to_user detected: %s not in query: %s",
                forward_to_user,
                user_message,
            )
            return ChatResponse(
                reply=f"Tôi chưa rõ bạn muốn chuyển tiếp đơn hàng {order_id} cho ai. Vui lòng cung cấp tên hoặc email người nhận.",
                intent="general_query",
                tool_calls=[],
            )

        # 2. Kiểm tra tham số theo Quy tắc 1
        if fn_name == "CheckOrderStatus":
            if not order_id or order_id == "null" or str(order_id).strip() == "":
                return ChatResponse(
                    reply="Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện.",
                    intent="general_query",
                    tool_calls=[],
                )
        elif fn_name == "ReleaseOrder":
            if not order_id or order_id == "null" or str(order_id).strip() == "":
                return ChatResponse(
                    reply="Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện.",
                    intent="general_query",
                    tool_calls=[],
                )
        elif fn_name == "RejectOrder":
            reason_code = args.get("reason_code")
            if not order_id or order_id == "null" or str(order_id).strip() == "":
                return ChatResponse(
                    reply="Tôi chưa xác định được đơn hàng nào. Vui lòng cho tôi mã đơn hàng.",
                    intent="general_query",
                    tool_calls=[],
                )
            if (
                not reason_code
                or reason_code == "null"
                or str(reason_code).strip() == ""
            ):
                return ChatResponse(
                    reply=f"Tôi đã ghi nhận yêu cầu hủy đơn hàng {order_id}. Bạn vui lòng cho biết lý do hủy đơn là gì (do sai giá, hết hàng, hay lý do khác) để tôi cập nhật chính xác lên hệ thống SAP nhé?",
                    intent="general_query",
                    tool_calls=[],
                )
        elif fn_name == "ForwardOrder":
            forward_to_user = args.get("forward_to_user")
            if not order_id or order_id == "null" or str(order_id).strip() == "":
                return ChatResponse(
                    reply="Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện.",
                    intent="general_query",
                    tool_calls=[],
                )
            if (
                not forward_to_user
                or forward_to_user == "null"
                or str(forward_to_user).strip() == ""
            ):
                return ChatResponse(
                    reply=f"Tôi chưa rõ bạn muốn chuyển tiếp đơn hàng {order_id} cho ai. Vui lòng cung cấp tên hoặc email người nhận.",
                    intent="general_query",
                    tool_calls=[],
                )

        return None

    def _recover_failed_generation(
        self,
        exc: openai.BadRequestError,
        user_message: str,
    ) -> ChatResponse | None:
        """
        Trích xuất thông tin function call bị lỗi từ failed_generation của Groq API (400 Bad Request).
        Kiểm tra tính hợp lệ của tham số và trả về ChatResponse tương ứng hoặc None nếu không khôi phục được.
        """
        try:
            failed_gen = None
            body = getattr(exc, "body", None)
            if isinstance(body, dict):
                failed_gen = body.get("error", {}).get("failed_generation")

            if not failed_gen:
                exc_str = str(exc)
                func_idx = exc_str.find("<function=")
                if func_idx != -1:
                    match_func = re.search(
                        r"(<function=.*?>.*?<function>|<function=.*?>.*?</function>|<function=.*?>.*?(?=['\"]|\}))",
                        exc_str[func_idx:],
                        re.DOTALL,
                    )
                    if match_func:
                        failed_gen = match_func.group(1)

            if not failed_gen:
                return None

            match = re.search(
                r"<function=(\w+)>(.*?)(?:<function>|</function>|$)",
                failed_gen,
                re.DOTALL,
            )
            if not match:
                return None

            fn_name = match.group(1)
            raw_args = match.group(2).strip()

            try:
                args = json.loads(raw_args) if raw_args else {}
            except json.JSONDecodeError:
                try:
                    args = json.loads(raw_args.replace("'", '"'))
                except Exception:
                    args = {}

            # Validation các quy tắc
            validation_resp = self._validate_and_build_response(
                fn_name, args, user_message
            )
            if validation_resp is not None:
                return validation_resp

            if fn_name == "GetSalesOrders":
                cleaned_args = {}
                for k, v in args.items():
                    if v is not None and str(v).strip() != "" and str(v) != "null":
                        cleaned_args[k] = v
                args = cleaned_args

            recovered_tool_call = ToolCall(
                id="recovered_" + fn_name.lower(), function_name=fn_name, arguments=args
            )
            intent = _function_name_to_intent(fn_name)
            reply_text = f"Đang thực thi hàm: {fn_name}"

            return ChatResponse(
                reply=reply_text, intent=intent, tool_calls=[recovered_tool_call]
            )
        except Exception as e:
            logger.warning("Failed to recover from failed_generation: %s", e)
            return None

    # ── Response parser ────────────────────────────────────────────────────

    def _parse_response(
        self,
        response: Any,
        user_message: str,
    ) -> ChatResponse:
        """
        Bóc tách kết quả từ ChatCompletion:
        - Nếu có tool_calls → is_function_call=True, trích xuất name + args
        - Nếu chỉ có text      → general_query, tool_calls=[]
        """
        parsed_tool_calls: list[ToolCall] = []
        intent = "general_query"

        choice = response.choices[0]
        reply_text = choice.message.content or ""
        tool_calls = getattr(choice.message, "tool_calls", None) or []

        for tc in tool_calls:
            if tc.type == "function":
                fn_name = tc.function.name
                try:
                    args = (
                        json.loads(tc.function.arguments)
                        if tc.function.arguments
                        else {}
                    )
                except json.JSONDecodeError:
                    logger.warning(
                        "Failed to parse function arguments: %s", tc.function.arguments
                    )
                    args = {}

                validation_resp = self._validate_and_build_response(
                    fn_name, args, user_message
                )
                if validation_resp is not None:
                    return validation_resp

                if fn_name == "GetSalesOrders":
                    cleaned_args = {}
                    for k, v in args.items():
                        if v is not None and str(v).strip() != "" and str(v) != "null":
                            cleaned_args[k] = v
                    args = cleaned_args

                parsed_tool_calls.append(
                    ToolCall(
                        id=tc.id,
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

    - Có GROQ_API_KEY thật → gọi Groq API
    - Không có key         → trả mock response
    """
    if _is_real_key_configured():
        try:
            return _get_orchestrator().process(request.user_message)
        except Exception as exc:
            logger.error("Groq orchestration error: %s – raising exception.", exc)
            raise
    else:
        return _mock_response(request.user_message)
