"""
Groq API Orchestrator (OpenAI-compatible SDK).
Kiến trúc:
  - Nạp system prompt từ prompts/system_prompt.txt
  - Load dynamic function schemas từ functions/*.json (hỗ trợ hot-reload)
  - Gọi Groq với Function Calling
  - Validate tham số dựa trên JSON schema (generic, không hardcode tên hàm)
  - Map function name → adaptive_card_type cho frontend Adaptive Card
  - Trả về ChatResponse đúng contract mà Backend (.NET) kỳ vọng
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

# Map function name → Adaptive Card type cho frontend
_FUNCTION_CARD_TYPE: dict[str, str] = {
    "CheckOrderStatus": "order_detail",
    "GetOrderDetail": "order_detail",
    "GetSalesOrders": "order_list",
    "ReleaseOrder": "order_detail",
    "RejectOrder": "order_detail",
    "ForwardOrder": "order_detail",
    "GetKpiSummary": "kpi_summary",
    "GetKpiByCustomer": "kpi_by_customer",
    "GetKpiByProduct": "kpi_by_product",
    "GetOverdueOrders": "overdue_orders",
    "CreateOrder": "order_detail",
    "UpdateOrderReference": "order_detail",
}


def _is_real_key_configured() -> bool:
    """True khi GROQ_API_KEY trông như key thật (không phải placeholder)."""
    return bool(_GROQ_API_KEY) and "your-" not in _GROQ_API_KEY


def _function_name_to_intent(name: str) -> str:
    """CamelCase → snake_case, ví dụ: CheckOrderStatus → check_order_status."""
    s1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


def _get_adaptive_card_type(fn_name: str) -> str | None:
    """Trả về adaptive_card_type cho frontend dựa trên tên hàm."""
    return _FUNCTION_CARD_TYPE.get(fn_name)


# ---------------------------------------------------------------------------
# Mock fallback (không cần key thật)
# ---------------------------------------------------------------------------


def _mock_response(user_message: str) -> ChatResponse:
    """
    Phản hồi giả lập để dev/test không cần key thật.
    Logic nhận diện intent đơn giản dựa trên keyword.
    Hỗ trợ: CheckOrderStatus, GetOrderDetail, GetSalesOrders,
             GetKpiSummary, GetKpiByCustomer, GetKpiByProduct, GetOverdueOrders.
    """
    logger.info("Using MOCK Groq response (no real API key configured).")

    tool_calls: list[ToolCall] = []
    intent = "general_query"
    card_type: str | None = None
    reply = f"[MOCK] Tôi đã nhận được tin nhắn của bạn: '{user_message}'"

    lower = user_message.lower()

    # KPI keywords (kiểm tra trước để tránh nhầm với order)
    if any(
        kw in lower
        for kw in (
            "kpi",
            "doanh thu",
            "revenue",
            "dashboard",
            "hiệu suất",
            "performance",
        )
    ):
        if any(kw in lower for kw in ("khách hàng", "customer", "client")):
            intent = "get_kpi_by_customer"
            fn_name = "GetKpiByCustomer"
        elif any(kw in lower for kw in ("sản phẩm", "product", "material", "hàng")):
            intent = "get_kpi_by_product"
            fn_name = "GetKpiByProduct"
        else:
            intent = "get_kpi_summary"
            fn_name = "GetKpiSummary"
        tool_calls.append(
            ToolCall(id="mock_kpi_001", function_name=fn_name, arguments={})
        )
        card_type = _get_adaptive_card_type(fn_name)
        reply = "[MOCK] Đang truy xuất dữ liệu KPI từ SAP..."

    elif any(
        kw in lower
        for kw in ("quá hạn", "overdue", "giao trễ", "late delivery", "past due")
    ):
        intent = "get_overdue_orders"
        tool_calls.append(
            ToolCall(
                id="mock_overdue_001", function_name="GetOverdueOrders", arguments={}
            )
        )
        card_type = _get_adaptive_card_type("GetOverdueOrders")
        reply = "[MOCK] Đang lấy danh sách đơn hàng quá hạn từ SAP..."

    elif any(kw in lower for kw in ("tạo đơn", "tạo mới", "đặt hàng mới", "create order", "place order", "new order")):
        intent = "create_order"
        tool_calls.append(
            ToolCall(
                id="mock_create_001", function_name="CreateOrder", arguments={}
            )
        )
        card_type = _get_adaptive_card_type("CreateOrder")
        reply = "[MOCK] Vui lòng cung cấp thông tin khách hàng và sản phẩm để tạo đơn hàng mới."

    elif any(kw in lower for kw in ("cập nhật reference", "cập nhật po", "đổi po", "update reference", "update po", "change po", "set reference")):
        match = re.search(r"(ord-\w+)", lower)
        order_id = match.group(1).upper() if match else ""
        if order_id:
            intent = "update_order_reference"
            tool_calls.append(
                ToolCall(
                    id="mock_updateref_001",
                    function_name="UpdateOrderReference",
                    arguments={"order_id": order_id},
                )
            )
            card_type = _get_adaptive_card_type("UpdateOrderReference")
            reply = f"[MOCK] Vui lòng cung cấp số PO reference mới cho đơn hàng {order_id}."
        else:
            reply = "[MOCK] Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện."

    elif any(kw in lower for kw in ("chi tiết", "detail", "line item", "mặt hàng")):
        match = re.search(r"(ord-\w+)", lower)
        order_id = match.group(1).upper() if match else ""
        if order_id:
            intent = "get_order_detail"
            tool_calls.append(
                ToolCall(
                    id="mock_detail_001",
                    function_name="GetOrderDetail",
                    arguments={"order_id": order_id},
                )
            )
            card_type = _get_adaptive_card_type("GetOrderDetail")
            reply = f"[MOCK] Đang lấy chi tiết đơn hàng {order_id}..."
        else:
            reply = "[MOCK] Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện."

    elif any(
        kw in lower for kw in ("đơn hàng", "order", "ord-", "trạng thái", "status")
    ):
        match = re.search(r"(ord-\w+)", lower)
        order_id = match.group(1).upper() if match else ""
        if order_id:
            intent = "check_order_status"
            tool_calls.append(
                ToolCall(
                    id="mock_check_001",
                    function_name="CheckOrderStatus",
                    arguments={"order_id": order_id},
                )
            )
            card_type = _get_adaptive_card_type("CheckOrderStatus")
            reply = f"[MOCK] Đang kiểm tra trạng thái đơn hàng {order_id}..."
        else:
            # Danh sách đơn hàng
            intent = "get_sales_orders"
            tool_calls.append(
                ToolCall(
                    id="mock_list_001", function_name="GetSalesOrders", arguments={}
                )
            )
            card_type = _get_adaptive_card_type("GetSalesOrders")
            reply = "[MOCK] Đang lấy danh sách đơn hàng từ SAP..."

    return ChatResponse(
        reply=reply, intent=intent, tool_calls=tool_calls, adaptive_card_type=card_type
    )


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
        # Cache schema objects (dict name→schema) để generic validation đọc required fields
        self._schema_cache: dict[str, dict[str, Any]] = self._build_schema_cache()
        logger.info(
            "AIOrchestrator initialized | model=%s | tools_loaded=%d",
            _GROQ_MODEL,
            len(self._tools) if self._tools else 0,
        )

    def _build_schema_cache(self) -> dict[str, dict[str, Any]]:
        """Xây dựng dict {function_name: parameters_schema} từ danh sách tools đã load."""
        cache: dict[str, dict[str, Any]] = {}
        if not self._tools:
            return cache
        for tool in self._tools:
            fn = tool.get("function", {})
            name = fn.get("name", "")
            params = fn.get("parameters", {})
            if name:
                cache[name] = params
        return cache

    def reload_tools(self) -> None:
        """
        Hot-reload function schemas từ disk mà không cần restart server.
        Gọi method này khi file JSON trong functions/ được thêm/sửa/xóa.
        """
        self._system_prompt = _load_system_prompt()
        self._tools = _load_groq_tools()
        self._schema_cache = self._build_schema_cache()
        logger.info(
            "AIOrchestrator tools reloaded | tools_loaded=%d",
            len(self._tools) if self._tools else 0,
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

    def _is_missing_or_null(self, value: Any) -> bool:
        """Trả về True nếu value là None, chuỗi rỗng, hoặc chuỗi 'null'."""
        if value is None:
            return True
        if isinstance(value, str) and (
            value.strip() == "" or value.strip().lower() == "null"
        ):
            return True
        return False

    def _validate_and_build_response(
        self, fn_name: str, args: dict, user_message: str
    ) -> ChatResponse | None:
        """
        Kiểm tra tính hợp lệ của tham số theo JSON Schema (generic).
        - Phát hiện ảo tưởng (hallucinated) order_id / forward_to_user
        - Kiểm tra required fields từ schema cache
        - Kiểm tra các điều kiện nghiệp vụ đặc thù (RejectOrder, ForwardOrder)
        Trả về ChatResponse nếu cần yêu cầu người dùng cung cấp thêm thông tin,
        hoặc None nếu tất cả tham số hợp lệ.
        """
        lower_msg = user_message.lower()
        order_id = args.get("order_id")
        is_reject_intent = fn_name == "RejectOrder"

        # ── Chống ảo tưởng order_id ──────────────────────────────────────────
        if order_id and not self._is_missing_or_null(order_id):
            if str(order_id).lower() not in lower_msg:
                logger.warning(
                    "Hallucinated order_id detected: '%s' not in query: '%s'",
                    order_id,
                    user_message,
                )
                if is_reject_intent:
                    return ChatResponse(
                        reply="Tôi chưa xác định được đơn hàng nào. Vui lòng cho tôi mã đơn hàng.",
                        intent="general_query",
                        tool_calls=[],
                    )
                return ChatResponse(
                    reply="Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện.",
                    intent="general_query",
                    tool_calls=[],
                )

        # ── Chống ảo tưởng forward_to_user ──────────────────────────────────
        forward_to_user = args.get("forward_to_user")
        if forward_to_user and not self._is_missing_or_null(forward_to_user):
            if str(forward_to_user).lower() not in lower_msg:
                logger.warning(
                    "Hallucinated forward_to_user detected: '%s' not in query: '%s'",
                    forward_to_user,
                    user_message,
                )
                return ChatResponse(
                    reply=(
                        f"Tôi chưa rõ bạn muốn chuyển tiếp đơn hàng {order_id} cho ai. "
                        "Vui lòng cung cấp tên hoặc email người nhận."
                    ),
                    intent="general_query",
                    tool_calls=[],
                )

        # ── Kiểm tra required fields từ JSON Schema (generic) ────────────────
        schema = self._schema_cache.get(fn_name, {})
        required_fields: list[str] = schema.get("required", [])
        properties: dict[str, Any] = schema.get("properties", {})

        for field in required_fields:
            value = args.get(field)
            if self._is_missing_or_null(value):
                logger.warning(
                    "Missing required field '%s' for function '%s'", field, fn_name
                )
                # Thông điệp ngữ cảnh theo từng loại field
                if field == "order_id":
                    if is_reject_intent:
                        return ChatResponse(
                            reply="Tôi chưa xác định được đơn hàng nào. Vui lòng cho tôi mã đơn hàng.",
                            intent="general_query",
                            tool_calls=[],
                        )
                    return ChatResponse(
                        reply="Vui lòng cung cấp mã đơn hàng cụ thể để tôi thực hiện.",
                        intent="general_query",
                        tool_calls=[],
                    )
                elif field == "reason_code":
                    return ChatResponse(
                        reply=(
                            f"Tôi đã ghi nhận yêu cầu hủy đơn hàng {order_id}. "
                            "Bạn vui lòng cho biết lý do hủy đơn là gì "
                            "(do sai giá, hết hàng, hay lý do khác) để tôi cập nhật "
                            "chính xác lên hệ thống SAP nhé?"
                        ),
                        intent="general_query",
                        tool_calls=[],
                    )
                elif field == "forward_to_user":
                    return ChatResponse(
                        reply=(
                            f"Tôi chưa rõ bạn muốn chuyển tiếp đơn hàng {order_id} cho ai. "
                            "Vui lòng cung cấp tên hoặc email người nhận."
                        ),
                        intent="general_query",
                        tool_calls=[],
                    )
                else:
                    # Generic fallback cho required fields khác
                    field_desc = properties.get(field, {}).get("description", field)
                    return ChatResponse(
                        reply=f"Vui lòng cung cấp thông tin bắt buộc: {field_desc}.",
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

            # Làm sạch null/empty cho hàm có optional-only params
            schema = self._schema_cache.get(fn_name, {})
            if not schema.get("required"):
                args = {
                    k: v
                    for k, v in args.items()
                    if v is not None and str(v).strip() != "" and str(v) != "null"
                }

            recovered_tool_call = ToolCall(
                id="recovered_" + fn_name.lower(), function_name=fn_name, arguments=args
            )
            intent = _function_name_to_intent(fn_name)
            card_type = _get_adaptive_card_type(fn_name)

            return ChatResponse(
                reply="",
                intent=intent,
                tool_calls=[recovered_tool_call],
                adaptive_card_type=card_type,
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

                # Làm sạch null/empty cho các hàm có optional-only params
                # (áp dụng chung: GetSalesOrders, GetKpiSummary, GetKpiByCustomer, GetKpiByProduct, GetOverdueOrders)
                schema = self._schema_cache.get(fn_name, {})
                if not schema.get(
                    "required"
                ):  # Hàm không có required field → clean nulls
                    cleaned_args = {
                        k: v
                        for k, v in args.items()
                        if v is not None and str(v).strip() != "" and str(v) != "null"
                    }
                    args = cleaned_args

                parsed_tool_calls.append(
                    ToolCall(
                        id=tc.id,
                        function_name=fn_name,
                        arguments=args,
                    )
                )

        card_type: str | None = None
        if parsed_tool_calls:
            first_fn = parsed_tool_calls[0].function_name
            intent = _function_name_to_intent(first_fn)
            card_type = _get_adaptive_card_type(first_fn)
            if not reply_text:
                reply_text = ""

        return ChatResponse(
            reply=reply_text,
            intent=intent,
            tool_calls=parsed_tool_calls,
            adaptive_card_type=card_type,
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
