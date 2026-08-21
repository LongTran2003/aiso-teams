"""
Groq API Orchestrator (OpenAI-compatible SDK).
Kiáº¿n trÃºc:
  - Náº¡p system prompt tá»« prompts/system_prompt.txt
  - Load dynamic function schemas tá»« functions/*.json (há»— trá»£ hot-reload)
  - Gá»i Groq vá»›i Function Calling
  - Validate tham sá»‘ dá»±a trÃªn JSON schema (generic, khÃ´ng hardcode tÃªn hÃ m)
  - Map function name â†’ adaptive_card_type cho frontend Adaptive Card
  - Tráº£ vá» ChatResponse Ä‘Ãºng contract mÃ  Backend (.NET) ká»³ vá»ng
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
# Helpers â€“ file loading
# ---------------------------------------------------------------------------


def _load_system_prompt() -> str:
    """Äá»c system prompt tá»« disk; fallback vá» chuá»—i máº·c Ä‘á»‹nh náº¿u khÃ´ng tÃ¬m tháº¥y."""
    try:
        base_prompt = _SYSTEM_PROMPT_PATH.read_text(encoding="utf-8").strip()
        from datetime import UTC, datetime

        today = datetime.now(UTC).date()
        day_of_week = today.strftime("%A")
        return base_prompt.replace(
            "{{CURRENT_DATE}}", today.strftime("%Y-%m-%d")
        ).replace("{{CURRENT_DAY_OF_WEEK}}", day_of_week)
    except FileNotFoundError:
        logger.warning("system_prompt.txt not found â€“ using built-in default.")
        return "You are a helpful AI assistant."


def _load_groq_tools() -> list[dict[str, Any]] | None:
    """
    QuÃ©t thÆ° má»¥c /functions, load tá»«ng *.json, chuyá»ƒn hÃ³a sang
    OpenAI tool format. Tráº£ vá» None náº¿u khÃ´ng cÃ³ file nÃ o.
    """
    if not _FUNCTIONS_DIR.exists():
        logger.warning("functions/ directory not found â€“ no tools loaded.")
        return None

    tools: list[dict[str, Any]] = []
    for path in sorted(_FUNCTIONS_DIR.glob("*.json")):
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
            # Chuáº©n hÃ³a vá» format OpenAI tool: {"type": "function", "function": {...}}
            if "type" in raw and raw["type"] == "function":
                tool = raw
            else:
                # Náº¿u lÃ  flat format trong file json
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
# Helpers â€“ misc
# ---------------------------------------------------------------------------

# Map function name â†’ Adaptive Card type cho frontend
_FUNCTION_CARD_TYPE: dict[str, str] = {
    "CheckOrderStatus": "order_detail",
    "GetOrderDetail": "order_detail",
    "GetSalesOrders": "order_list",
    "GetPendingApprovals": "pending_approvals",
    "ViewAuditLog": "audit_log",
    "ListBotUsers": "bot_users",
    "ManageBotUser": "manage_bot_user",
    "ListDelegations": "list_delegations",
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
    """True khi GROQ_API_KEY trÃ´ng nhÆ° key tháº­t (khÃ´ng pháº£i placeholder)."""
    return bool(_GROQ_API_KEY) and "your-" not in _GROQ_API_KEY


def _function_name_to_intent(name: str) -> str:
    """CamelCase â†’ snake_case, vÃ­ dá»¥: CheckOrderStatus â†’ check_order_status."""
    s1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


def _get_adaptive_card_type(fn_name: str) -> str | None:
    """Tráº£ vá» adaptive_card_type cho frontend dá»±a trÃªn tÃªn hÃ m."""
    return _FUNCTION_CARD_TYPE.get(fn_name)


# ---------------------------------------------------------------------------
# Mock fallback (khÃ´ng cáº§n key tháº­t)
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
        kw in lower
        for kw in (
            "my order",
            "my orders",
            "my sale",
            "của tôi",
            "cua toi",
            "đơn của tôi",
            "đơn hàng của tôi",
            "đơn hàng",
            "đơn",
            "order",
            "ord-",
            "trạng thái",
            "status",
        )
    ):
        match = re.search(r"(ord-\w+)", lower)
        order_id = match.group(1).upper() if match else ""

        # Check for "my" keywords to set ownedByMe
        has_my = any(
            kw in lower
            for kw in (
                "my order",
                "my orders",
                "my sale",
                "của tôi",
                "cua toi",
                "đơn của tôi",
                "đơn hàng của tôi",
            )
        )

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
            # Danh sách đơn hàng - set ownedByMe=true nếu có "my"
            intent = "get_sales_orders"
            args = {"ownedByMe": True} if has_my else {}
            tool_calls.append(
                ToolCall(
                    id="mock_list_001", function_name="GetSalesOrders", arguments=args
                )
            )
            card_type = _get_adaptive_card_type("GetSalesOrders")
            if has_my:
                reply = "[MOCK] Đang lấy danh sách đơn hàng của bạn từ SAP..."
            else:
                reply = "[MOCK] Đang lấy danh sách đơn hàng từ SAP..."

    return ChatResponse(
        reply=reply, intent=intent, tool_calls=tool_calls, adaptive_card_type=card_type
    )


def _is_order_id_in_message(order_id: Any, user_message: str) -> bool:
    """Kiá»ƒm tra xem order_id (hoáº·c dáº¡ng rÃºt gá»n/sá»‘ cá»§a nÃ³) cÃ³ trong tin nháº¯n cá»§a user khÃ´ng."""
    oid_str = str(order_id).lower().strip()
    msg_lower = user_message.lower()

    # Khá»›p chuá»—i trá»±c tiáº¿p
    if oid_str in msg_lower:
        return True

    # TrÃ­ch xuáº¥t cÃ¡c cá»¥m sá»‘ liÃªn tiáº¿p
    oid_digits = re.findall(r"\d+", oid_str)
    if not oid_digits:
        return False

    # TrÃ­ch xuáº¥t cÃ¡c cá»¥m sá»‘ trong tin nháº¯n gá»‘c
    msg_digits = re.findall(r"\d+", msg_lower)

    # Loáº¡i bá» sá»‘ 0 á»Ÿ Ä‘áº§u Ä‘á»ƒ so khá»›p dáº¡ng rÃºt gá»n (vÃ­ dá»¥: 0000000009 -> 9)
    oid_digits_stripped = [d.lstrip("0") for d in oid_digits]
    msg_digits_stripped = [d.lstrip("0") for d in msg_digits]

    # Kiá»ƒm tra xem cÃ¡c cá»¥m sá»‘ Ä‘Ã£ rÃºt gá»n cá»§a order_id cÃ³ náº±m trong cá»¥m sá»‘ rÃºt gá»n cá»§a tin nháº¯n khÃ´ng
    for d in oid_digits_stripped:
        if d and d in msg_digits_stripped:
            return True

    # Fallback: Kiá»ƒm tra xem cá»¥m sá»‘ rÃºt gá»n cÃ³ xuáº¥t hiá»‡n dÆ°á»›i dáº¡ng chuá»—i con trong tin nháº¯n khÃ´ng
    for d in oid_digits_stripped:
        if d and d in msg_lower:
            return True

    return False


# ---------------------------------------------------------------------------
# AIOrchestrator class â€“ Groq (OpenAI Client)
# ---------------------------------------------------------------------------


class AIOrchestrator:
    """
    Quáº£n lÃ½ toÃ n bá»™ lifecycle cá»§a má»™t láº§n gá»i Groq:
      1. Khá»Ÿi táº¡o openai.OpenAI Client trá» tá»›i Groq endpoint
      2. Náº¡p system prompt + dynamic function tools
      3. Gá»­i message vÃ  parse tool_calls tá»« response
      4. Tráº£ vá» ChatResponse Ä‘Ãºng contract vá»›i Backend .NET
    """

    def __init__(self) -> None:
        self._client = OpenAI(
            api_key=_GROQ_API_KEY, base_url="https://api.groq.com/openai/v1"
        )
        self._system_prompt = _load_system_prompt()
        self._tools = _load_groq_tools()
        # Cache schema objects (dict nameâ†’schema) Ä‘á»ƒ generic validation Ä‘á»c required fields
        self._schema_cache: dict[str, dict[str, Any]] = self._build_schema_cache()
        logger.info(
            "AIOrchestrator initialized | model=%s | tools_loaded=%d",
            _GROQ_MODEL,
            len(self._tools) if self._tools else 0,
        )

    def _build_schema_cache(self) -> dict[str, dict[str, Any]]:
        """XÃ¢y dá»±ng dict {function_name: parameters_schema} tá»« danh sÃ¡ch tools Ä‘Ã£ load."""
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
        Hot-reload function schemas tá»« disk mÃ  khÃ´ng cáº§n restart server.
        Gá»i method nÃ y khi file JSON trong functions/ Ä‘Æ°á»£c thÃªm/sá»­a/xÃ³a.
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
        PhÃ¢n tÃ­ch user_message Ä‘á»ƒ chá»n ra subset tools phÃ¹ há»£p, giáº£m lÆ°á»£ng token payload.
        """
        if not self._tools:
            return None

        msg_lower = user_message.lower()

        # Äá»‹nh nghÄ©a cÃ¡c group tools
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
            "ListDelegations",
        }

        # Tá»« khÃ³a Ä‘á»‹nh vá»‹ nhÃ³m (gá»“m cáº£ Tiáº¿ng Viá»‡t khÃ´ng dáº¥u vÃ  synonyms)
        kpi_keywords = [
            "kpi",
            "doanh thu",
            "doanh sá»‘",
            "doanh so",
            "dashboard",
            "hiá»‡u suáº¥t",
            "hieu suat",
            "bÃ¡n cháº¡y",
            "ban chay",
            "revenue",
        ]
        create_update_keywords = [
            "táº¡o",
            "tao",
            "Ä‘áº·t hÃ ng",
            "dat hang",
            "láº­p Ä‘Æ¡n",
            "lap don",
            "lÃªn Ä‘Æ¡n",
            "len don",
            "cáº­p nháº­t",
            "cap nhat",
            "reference",
            "sá»‘ po",
            "so po",
            "Ä‘á»•i po",
            "doi po",
            "gÃ¡n sá»‘ po",
            "gan so po",
            "create",
            "update",
            "new order",
            "place",
            "generate",
        ]
        admin_keywords = [
            "delegate",
            "delegation",
            "delegations",
            "revoke",
            "uá»· quyá»n",
            "á»§y quyá»n",
            "thu há»“i",
            "thu hoi",
            "force delegate",
            "cÆ°á»¡ng cháº¿",
            "user",
            "users",
            "role",
            "sales org",
            "audit",
            "log",
            "add user",
            "thÃªm user",
            "pre assign",
            "allow list",
            "nháº­t kÃ½",
        ]
        order_list_keywords = [
            "danh sÃ¡ch",
            "danh sach",
            "lá»c Ä‘Æ¡n",
            "loc don",
            "tÃ¬m Ä‘Æ¡n",
            "tim don",
            "quÃ¡ háº¡n",
            "qua han",
            "giao hÃ ng trá»…",
            "giao hang tre",
            "trá»… háº¡n",
            "tre han",
            "late",
            "overdue",
            "list",
            "search",
        ]

        selected_names = set()

        # 1. Khá»›p nhÃ³m KPI
        if any(kw in msg_lower for kw in kpi_keywords):
            selected_names.update(kpi_tools)

        # 2. Khá»›p nhÃ³m Create/Update
        if any(kw in msg_lower for kw in create_update_keywords):
            selected_names.update(create_update_tools)

        # 3. Khá»›p nhÃ³m List
        if any(kw in msg_lower for kw in order_list_keywords):
            selected_names.update(order_list_tools)
            selected_names.update(order_op_tools)

        # 3.5. Khá»›p nhÃ³m Admin
        if any(kw in msg_lower for kw in admin_keywords):
            selected_names.update(admin_tools)

        # 4. Náº¿u khÃ´ng khá»›p nhÃ³m Ä‘áº·c trÆ°ng nÃ o hoáº·c khá»›p thao tÃ¡c Ä‘Æ¡n láº», máº·c Ä‘á»‹nh dÃ¹ng Fallback (Op + List)
        if not selected_names or any(
            kw in msg_lower
            for kw in [
                "duyá»‡t",
                "duyet",
                "há»§y",
                "huy",
                "tá»« chá»‘i",
                "tu choi",
                "chuyá»ƒn tiáº¿p",
                "chuyen tiep",
                "bÃ n giao",
                "ban giao",
                "nhá» xá»­ lÃ½",
                "nho xu ly",
                "chi tiáº¿t",
                "chi tiet",
                "xem Ä‘Æ¡n",
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

    def _detect_forced_tool(self, user_message: str) -> str | None:
        """Detect delegation/revoke intent and return forced tool name."""
        msg = user_message.lower()
        # Revoke keywords first (more specific)
        revoke_kws = ["revoke", "thu hồi", "thu hoi", "huỷ uỷ quyền", "huy uy quyen"]
        if any(kw in msg for kw in revoke_kws):
            return "RevokeDelegation"
        # Force delegate
        force_kws = ["force delegate", "cưỡng chế"]
        if any(kw in msg for kw in force_kws):
            return "ForceDelegateApproval"
        # List delegations
        list_kws = ["list delegation", "danh sách uỷ quyền", "danh sach uy quyen"]
        if any(kw in msg for kw in list_kws):
            return "ListDelegations"
        # Normal delegate
        delegate_kws = ["delegate", "uỷ quyền", "ủy quyền", "uy quyen"]
        if any(kw in msg for kw in delegate_kws):
            return "DelegateApproval"
        return None

    def process(
        self, user_message: str, chat_history: list[Any] | None = None
    ) -> ChatResponse:
        """
        Gá»­i user_message tá»›i Groq vÃ  parse káº¿t quáº£.
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
            kwargs["tool_choice"] = "auto"

            # Force specific tool for delegation intents
            forced_tool = self._detect_forced_tool(user_message)
            if forced_tool:
                # Ensure the forced tool is in the subset
                forced_in_subset = any(
                    t.get("function", {}).get("name") == forced_tool
                    for t in subset_tools
                )
                if not forced_in_subset:
                    # Add it from full tools list
                    for t in self._tools or []:
                        if t.get("function", {}).get("name") == forced_tool:
                            subset_tools.append(t)
                            kwargs["tools"] = subset_tools
                            break
                kwargs["tool_choice"] = {
                    "type": "function",
                    "function": {"name": forced_tool},
                }

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
        """Tráº£ vá» True náº¿u value lÃ  None, chuá»—i rá»—ng, hoáº·c chuá»—i 'null'."""
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
        Kiá»ƒm tra tÃ­nh há»£p lá»‡ cá»§a tham sá»‘ theo JSON Schema (generic).
        - PhÃ¡t hiá»‡n áº£o tÆ°á»Ÿng (hallucinated) order_id / forward_to_user
        - Kiá»ƒm tra required fields tá»« schema cache
        - Kiá»ƒm tra cÃ¡c Ä‘iá»u kiá»‡n nghiá»‡p vá»¥ Ä‘áº·c thÃ¹ (RejectOrder, ForwardOrder)
        Tráº£ vá» ChatResponse náº¿u cáº§n yÃªu cáº§u ngÆ°á»i dÃ¹ng cung cáº¥p thÃªm thÃ´ng tin,
        hoáº·c None náº¿u táº¥t cáº£ tham sá»‘ há»£p lá»‡.
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

        # â”€â”€ Chá»‘ng áº£o tÆ°á»Ÿng order_id â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
                    reply="TÃ´i chÆ°a xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c Ä‘Æ¡n hÃ ng nÃ o. Vui lÃ²ng cho tÃ´i mÃ£ Ä‘Æ¡n hÃ ng.",
                    intent="general_query",
                    tool_calls=[],
                )
            return ChatResponse(
                reply="Vui lÃ²ng cung cáº¥p mÃ£ Ä‘Æ¡n hÃ ng cá»¥ thá»ƒ Ä‘á»ƒ tÃ´i thá»±c hiá»‡n.",
                intent="general_query",
                tool_calls=[],
            )

        # â”€â”€ Chá»‘ng áº£o tÆ°á»Ÿng forward_to_user â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
                    f"TÃ´i chÆ°a rÃµ báº¡n muá»‘n chuyá»ƒn tiáº¿p Ä‘Æ¡n hÃ ng {order_id} cho ai. "
                    "Vui lÃ²ng cung cáº¥p tÃªn hoáº·c email ngÆ°á»i nháº­n."
                ),
                intent="general_query",
                tool_calls=[],
            )

        # â”€â”€ Kiá»ƒm tra required fields tá»« JSON Schema (generic) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        schema = self._schema_cache.get(fn_name, {})
        required_fields: list[str] = schema.get("required", [])
        properties: dict[str, Any] = schema.get("properties", {})

        for field in required_fields:
            value = args.get(field)
            if self._is_missing_or_null(value):
                logger.warning(
                    "Missing required field '%s' for function '%s'", field, fn_name
                )
                # ThÃ´ng Ä‘iá»‡p ngá»¯ cáº£nh theo tá»«ng loáº¡i field
                if field == "order_id":
                    if is_reject_intent:
                        return ChatResponse(
                            reply="TÃ´i chÆ°a xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c Ä‘Æ¡n hÃ ng nÃ o. Vui lÃ²ng cho tÃ´i mÃ£ Ä‘Æ¡n hÃ ng.",
                            intent="general_query",
                            tool_calls=[],
                        )
                    return ChatResponse(
                        reply="Vui lÃ²ng cung cáº¥p mÃ£ Ä‘Æ¡n hÃ ng cá»¥ thá»ƒ Ä‘á»ƒ tÃ´i thá»±c hiá»‡n.",
                        intent="general_query",
                        tool_calls=[],
                    )
                elif field == "reason_code":
                    return ChatResponse(
                        reply=(
                            f"TÃ´i Ä‘Ã£ ghi nháº­n yÃªu cáº§u há»§y Ä‘Æ¡n hÃ ng {order_id}. "
                            "Báº¡n vui lÃ²ng cho biáº¿t lÃ½ do há»§y Ä‘Æ¡n lÃ  gÃ¬ "
                            "(do sai giÃ¡, háº¿t hÃ ng, hay lÃ½ do khÃ¡c) Ä‘á»ƒ tÃ´i cáº­p nháº­t "
                            "chÃ­nh xÃ¡c lÃªn há»‡ thá»‘ng SAP nhÃ©?"
                        ),
                        intent="general_query",
                        tool_calls=[],
                    )
                elif field == "forward_to_user":
                    return ChatResponse(
                        reply=(
                            f"TÃ´i chÆ°a rÃµ báº¡n muá»‘n chuyá»ƒn tiáº¿p Ä‘Æ¡n hÃ ng {order_id} cho ai. "
                            "Vui lÃ²ng cung cáº¥p tÃªn hoáº·c email ngÆ°á»i nháº­n."
                        ),
                        intent="general_query",
                        tool_calls=[],
                    )
                else:
                    # Generic fallback cho required fields khÃ¡c
                    field_desc = properties.get(field, {}).get("description", field)
                    return ChatResponse(
                        reply=f"Vui lÃ²ng cung cáº¥p thÃ´ng tin báº¯t buá»™c: {field_desc}.",
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
        TrÃ­ch xuáº¥t thÃ´ng tin function call bá»‹ lá»—i tá»« failed_generation cá»§a Groq API (400 Bad Request).
        Kiá»ƒm tra tÃ­nh há»£p lá»‡ cá»§a tham sá»‘ vÃ  tráº£ vá» ChatResponse tÆ°Æ¡ng á»©ng hoáº·c None náº¿u khÃ´ng khÃ´i phá»¥c Ä‘Æ°á»£c.
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

                # Validation cÃ¡c quy táº¯c
                validation_resp = self._validate_and_build_response(
                    fn_name, args, user_message
                )
                if validation_resp is not None:
                    return validation_resp

                # LÃ m sáº¡ch null/empty cho hÃ m cÃ³ optional-only params
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

    # â”€â”€ Response parser â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    def _parse_response(
        self,
        response: Any,
        user_message: str,
    ) -> ChatResponse:
        """
        BÃ³c tÃ¡ch káº¿t quáº£ tá»« ChatCompletion:
        - Náº¿u cÃ³ tool_calls â†’ is_function_call=True, trÃ­ch xuáº¥t name + args
        - Náº¿u chá»‰ cÃ³ text      â†’ general_query, tool_calls=[]
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

                # LÃ m sáº¡ch null/empty cho cÃ¡c hÃ m cÃ³ optional-only params
                # (Ã¡p dá»¥ng chung: GetSalesOrders, GetKpiSummary, GetKpiByCustomer, GetKpiByProduct, GetOverdueOrders)
                schema = self._schema_cache.get(fn_name, {})
                if not schema.get(
                    "required"
                ):  # HÃ m khÃ´ng cÃ³ required field â†’ clean nulls
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
    """Tráº£ vá» singleton AIOrchestrator, khá»Ÿi táº¡o láº§n Ä‘áº§u khi cáº§n."""
    global _orchestrator_instance
    if _orchestrator_instance is None:
        _orchestrator_instance = AIOrchestrator()
    return _orchestrator_instance


# ---------------------------------------------------------------------------
# Public API â€“ entry point tá»« main.py (giá»¯ nguyÃªn signature)
# ---------------------------------------------------------------------------


def process_user_message(request: ChatRequest) -> ChatResponse:
    """
    Entry point Ä‘Æ°á»£c gá»i tá»« FastAPI route handler.
    Signature khÃ´ng Ä‘á»•i â†’ Backend .NET khÃ´ng bá»‹ áº£nh hÆ°á»Ÿng.

    - CÃ³ GROQ_API_KEY tháº­t â†’ gá»i Groq API
    - KhÃ´ng cÃ³ key         â†’ tráº£ mock response
    """
    if _is_real_key_configured():
        try:
            return _get_orchestrator().process(
                request.user_message, request.chat_history
            )
        except Exception as exc:
            logger.error("Groq orchestration error: %s â€“ raising exception.", exc)
            raise
    else:
        return _mock_response(request.user_message)
