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

import openai
from dotenv import load_dotenv
from openai import OpenAI

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
_GROQ_MODEL: str = os.getenv("GROQ_MODEL", "openai/gpt-oss-120b")


# ---------------------------------------------------------------------------
# Helpers – file loading
# ---------------------------------------------------------------------------


def _load_system_prompt() -> str:
    """Đọc system prompt từ disk; fallback về chuỗi mặc định nếu không tìm thấy."""
    try:
        base_prompt = _SYSTEM_PROMPT_PATH.read_text(encoding="utf-8").strip()
        from datetime import UTC, datetime

        today = datetime.now(UTC).date()
        day_of_week = today.strftime("%A")
        return base_prompt.replace(
            "{{CURRENT_DATE}}", today.strftime("%Y-%m-%d")
        ).replace("{{CURRENT_DAY_OF_WEEK}}", day_of_week)
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
    "GetPendingApprovals": "pending_approvals",
    "ViewAuditLog": "audit_log",
    "ListBotUsers": "bot_users",
    "ManageBotUser": "manage_bot_user",
    "GetOverdueOrders": "overdue_orders",
    "RequestRelease": "order_detail",
    "ApproveOrder": "order_detail",
    "RejectApproval": "order_detail",
    "ReleaseOrder": "order_detail",
    "RejectOrder": "order_detail",
    "ForwardOrder": "order_detail",
    "GetKpiSummary": "kpi_summary",
    "GetKpiByCustomer": "kpi_by_customer",
    "GetKpiByProduct": "kpi_by_product",
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

    elif any(
        kw in lower
        for kw in (
            "tạo đơn",
            "tạo mới",
            "đặt hàng mới",
            "create order",
            "place order",
            "new order",
        )
    ):
        intent = "create_order"
        tool_calls.append(
            ToolCall(id="mock_create_001", function_name="CreateOrder", arguments={})
        )
        card_type = _get_adaptive_card_type("CreateOrder")
        reply = "[MOCK] Vui lòng cung cấp thông tin khách hàng và sản phẩm để tạo đơn hàng mới."

    elif any(
        kw in lower
        for kw in (
            "cập nhật reference",
            "cập nhật po",
            "đổi po",
            "update reference",
            "update po",
            "change po",
            "set reference",
        )
    ):
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
            reply = (
                f"[MOCK] Vui lòng cung cấp số PO reference mới cho đơn hàng {order_id}."
            )
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


def _is_order_id_in_message(order_id: Any, user_message: str) -> bool:
    """Kiểm tra xem order_id (hoặc dạng rút gọn/số của nó) có trong tin nhắn của user không."""
    oid_str = str(order_id).lower().strip()
    msg_lower = user_message.lower()

    # Khớp chuỗi trực tiếp
    if oid_str in msg_lower:
        return True

    # Trích xuất các cụm số liên tiếp
    oid_digits = re.findall(r"\d+", oid_str)
    if not oid_digits:
        return False

    # Trích xuất các cụm số trong tin nhắn gốc
    msg_digits = re.findall(r"\d+", msg_lower)

    # Loại bỏ số 0 ở đầu để so khớp dạng rút gọn (ví dụ: 0000000009 -> 9)
    oid_digits_stripped = [d.lstrip("0") for d in oid_digits]
    msg_digits_stripped = [d.lstrip("0") for d in msg_digits]

    # Kiểm tra xem các cụm số đã rút gọn của order_id có nằm trong cụm số rút gọn của tin nhắn không
    for d in oid_digits_stripped:
        if d and d in msg_digits_stripped:
            return True

    # Fallback: Kiểm tra xem cụm số rút gọn có xuất hiện dưới dạng chuỗi con trong tin nhắn không
    for d in oid_digits_stripped:
        if d and d in msg_lower:
            return True

    return False


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
            api_key=_GROQ_API_KEY,
            base_url=os.getenv("GROQ_BASE_URL", "https://api.groq.com/openai/v1"),
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

    def _get_subset_tools(self, user_message: str) -> list[dict[str, Any]] | None:
        """
        Phân tích user_message để chọn ra subset tools phù hợp, giảm lượng token payload.
        """
        if not self._tools:
            return None

        msg_lower = user_message.lower()

        # Định nghĩa các group tools
        kpi_tools = {"GetKpiSummary", "GetKpiByCustomer", "GetKpiByProduct"}
        create_update_tools = {"CreateOrder", "UpdateOrderReference"}
        order_op_tools = {
            "CheckOrderStatus",
            "GetOrderDetail",
            "ReleaseOrder",
            "RejectOrder",
            "ForwardOrder",
        }
        order_list_tools = {"GetSalesOrders", "GetOverdueOrders"}
        admin_tools = {
            "ManageBotUser",
            "PreAssignUser",
            "ListBotUsers",
            "ViewAuditLog",
            "DelegateApproval",
            "RevokeDelegation",
        }

        # Từ khóa định vị nhóm (gồm cả Tiếng Việt không dấu và synonyms)
        kpi_keywords = [
            "kpi",
            "doanh thu",
            "doanh số",
            "doanh so",
            "dashboard",
            "hiệu suất",
            "hieu suat",
            "bán chạy",
            "ban chay",
            "revenue",
        ]
        create_update_keywords = [
            "tạo",
            "tao",
            "đặt hàng",
            "dat hang",
            "lập đơn",
            "lap don",
            "lên đơn",
            "len don",
            "cập nhật",
            "cap nhat",
            "reference",
            "số po",
            "so po",
            "đổi po",
            "doi po",
            "gán số po",
            "gan so po",
            "create",
            "update",
            "new order",
            "place",
            "generate",
        ]
        admin_keywords = [
            "delegate",
            "revoke",
            "uỷ quyền",
            "ủy quyền",
            "thu hồi",
            "thu hoi",
            "force delegate",
            "cưỡng chế",
            "user",
            "users",
            "role",
            "sales org",
            "audit",
            "log",
            "add user",
            "thêm user",
            "pre assign",
            "allow list",
            "nhật ký",
        ]
        order_list_keywords = [
            "danh sách",
            "danh sach",
            "lọc đơn",
            "loc don",
            "tìm đơn",
            "tim don",
            "quá hạn",
            "qua han",
            "giao hàng trễ",
            "giao hang tre",
            "trễ hạn",
            "tre han",
            "late",
            "overdue",
            "list",
            "search",
        ]

        selected_names = set()

        # 1. Khớp nhóm KPI
        if any(kw in msg_lower for kw in kpi_keywords):
            selected_names.update(kpi_tools)

        # 2. Khớp nhóm Create/Update
        if any(kw in msg_lower for kw in create_update_keywords):
            selected_names.update(create_update_tools)

        # 3. Khớp nhóm List
        if any(kw in msg_lower for kw in order_list_keywords):
            selected_names.update(order_list_tools)
            selected_names.update(order_op_tools)

        # 3.5. Khớp nhóm Admin
        if any(kw in msg_lower for kw in admin_keywords):
            selected_names.update(admin_tools)

        # 4. Nếu không khớp nhóm đặc trưng nào hoặc khớp thao tác đơn lẻ, mặc định dùng Fallback (Op + List)
        if not selected_names or any(
            kw in msg_lower
            for kw in [
                "duyệt",
                "duyet",
                "hủy",
                "huy",
                "từ chối",
                "tu choi",
                "chuyển tiếp",
                "chuyen tiep",
                "bàn giao",
                "ban giao",
                "nhờ xử lý",
                "nho xu ly",
                "chi tiết",
                "chi tiet",
                "xem đơn",
                "xem don",
                "release",
                "approve",
                "reject",
                "forward",
            ]
        ):
            selected_names.update(order_op_tools)
            selected_names.update(order_list_tools)

        subset = [
            tool
            for tool in self._tools
            if tool.get("function", {}).get("name") in selected_names
        ]
        return subset if subset else None

    def process(
        self, user_message: str, chat_history: list[Any] | None = None
    ) -> ChatResponse:
        """
        Gửi user_message tới Groq và parse kết quả.
        """
        messages = [
            {"role": "system", "content": self._system_prompt},
        ]
        if chat_history:
            for msg in chat_history:
                # msg can be a ChatMessage model or a dict
                role = getattr(msg, "role", None) or (
                    msg.get("role") if isinstance(msg, dict) else None
                )
                content = getattr(msg, "content", None) or (
                    msg.get("content") if isinstance(msg, dict) else None
                )
                if role and content:
                    messages.append({"role": role, "content": content})

        messages.append({"role": "user", "content": user_message})

        kwargs: dict[str, Any] = {
            "model": _GROQ_MODEL,
            "messages": messages,
            "temperature": 0.1,
        }
        subset_tools = self._get_subset_tools(user_message)
        if subset_tools:
            kwargs["tools"] = subset_tools

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

                # FALLBACK: If model returned plain conversational text instead of a tool call
                body = getattr(exc, "body", None)
                if isinstance(body, dict):
                    err = body.get("error", {})
                    if err.get("code") == "tool_use_failed":
                        failed_gen = err.get("failed_generation")
                        if failed_gen is not None:
                            reply_text = failed_gen.strip()
                            if not reply_text or "<function=" in reply_text:
                                reply_text = (
                                    "Xin lỗi, tôi cần thêm thông tin để thực hiện yêu cầu "
                                    "hoặc yêu cầu chưa rõ ràng. Bạn vui lòng cung cấp thêm chi tiết nhé."
                                )
                            logger.info(
                                "Falling back to raw failed_generation text for conversational reply."
                            )
                            return ChatResponse(
                                reply=reply_text,
                                intent="general_query",
                                tool_calls=[],
                                adaptive_card_type=None,
                            )
                raise
            except openai.AuthenticationError as exc:
                logger.error("Groq API Authentication Error (Invalid API Key): %s", exc)
                raise
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
            except Exception as exc:
                logger.error("Groq API unexpected error: %s", exc)
                raise

        if last_exc:
            raise last_exc
        raise RuntimeError("Groq API call failed after max retries.")

    def _is_missing_or_null(self, value: Any) -> bool:
        """Trả về True nếu value là None, chuỗi rỗng, hoặc chuỗi 'null'."""
        if value is None:
            return True
        return bool(
            isinstance(value, str)
            and (value.strip() == "" or value.strip().lower() == "null")
        )

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
        # Convert string-serialized items array to list if needed
        items = args.get("items")
        if isinstance(items, str) and items.strip().startswith("["):
            try:
                args["items"] = json.loads(items)
            except json.JSONDecodeError:
                logger.debug("Could not parse items JSON string: %r", items)

        lower_msg = user_message.lower()
        order_id = args.get("order_id")
        is_reject_intent = fn_name == "RejectOrder"

        # ── Chống ảo tưởng order_id ──────────────────────────────────────────
        if (
            order_id
            and not self._is_missing_or_null(order_id)
            and not _is_order_id_in_message(order_id, user_message)
        ):
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
        if (
            forward_to_user
            and not self._is_missing_or_null(forward_to_user)
            and str(forward_to_user).lower() not in lower_msg
        ):
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
                elif fn_name == "DelegateApproval" and field in ["delegateUser", "validFrom", "validTo"]:
                    return ChatResponse(
                        reply=(
                            "Please provide the SAP User ID of the employee you'd like to "
                            "delegate approval to, as well as the start date (validFrom) and "
                            "end date (validTo) for the delegation (in YYYY-MM-DD format). If "
                            "you'd like to set a maximum order amount limit or include a reason, "
                            "you can share those as well."
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

            # Let's find all function calls in failed_gen!
            # e.g. <function=Name>args...
            pattern = r"<function=(\w+)>(.*?)(?=<function=|</function>|<function>|$)"
            matches = list(re.finditer(pattern, failed_gen, re.DOTALL))
            if not matches:
                return None

            recovered_tool_calls = []
            first_fn = None
            for idx, match in enumerate(matches):
                fn_name = match.group(1)
                raw_args = match.group(2).strip()
                # strip trailing <function> or </function> tags if present in raw_args
                raw_args = re.sub(r"</?function>", "", raw_args).strip()

                try:
                    args = json.loads(raw_args) if raw_args else {}
                except json.JSONDecodeError:
                    try:
                        # Handle double/quadruple escaped quotes from string-serialized JSON arrays
                        cleaned = raw_args.replace('\\\\"', '\\"')
                        args = json.loads(cleaned)
                    except json.JSONDecodeError:
                        try:
                            args = json.loads(raw_args.replace("'", '"'))
                        except json.JSONDecodeError:
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

                if not first_fn:
                    first_fn = fn_name

                recovered_tool_calls.append(
                    ToolCall(
                        id=f"recovered_{fn_name.lower()}_{idx}",
                        function_name=fn_name,
                        arguments=args,
                    )
                )

            if not recovered_tool_calls:
                return None

            intent = _function_name_to_intent(first_fn)
            card_type = _get_adaptive_card_type(first_fn)

            return ChatResponse(
                reply="",
                intent=intent,
                tool_calls=recovered_tool_calls,
                adaptive_card_type=card_type,
            )
        except (
            json.JSONDecodeError,
            KeyError,
            TypeError,
            ValueError,
            AttributeError,
        ) as e:
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
            return _get_orchestrator().process(
                request.user_message, request.chat_history
            )
        except Exception as exc:
            logger.error("Groq orchestration error: %s – raising exception.", exc)
            raise
    else:
        return _mock_response(request.user_message)
