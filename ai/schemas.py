"""
schemas.py – Pydantic models for request/response validation.
"""
from __future__ import annotations

from typing import Any

from pydantic import BaseModel, Field


# ---------------------------------------------------------------------------
# Request
# ---------------------------------------------------------------------------

class ChatRequest(BaseModel):
    """Payload gửi lên endpoint POST /api/v1/orchestrate."""

    user_message: str = Field(
        ...,
        min_length=1,
        max_length=4096,
        description="Câu hỏi / lệnh của người dùng.",
        examples=["Kiểm tra trạng thái đơn hàng ORD-20240001"],
    )

    model_config = {
        "json_schema_extra": {
            "example": {
                "user_message": "Kiểm tra trạng thái đơn hàng ORD-20240001"
            }
        }
    }


# ---------------------------------------------------------------------------
# Response
# ---------------------------------------------------------------------------

class ToolCall(BaseModel):
    """Đại diện cho một lần gọi hàm mà model yêu cầu."""

    id: str = Field(..., description="ID duy nhất của tool call.")
    function_name: str = Field(..., description="Tên hàm được gọi.")
    arguments: dict[str, Any] = Field(
        default_factory=dict,
        description="Các đối số được parse từ JSON string của model.",
    )


class ChatResponse(BaseModel):
    """Phản hồi trả về cho client."""

    reply: str = Field(..., description="Phản hồi dạng văn bản của assistant.")
    intent: str = Field(
        ...,
        description="Ý định được nhận diện, ví dụ: 'check_order_status', 'general_query'.",
    )
    tool_calls: list[ToolCall] = Field(
        default_factory=list,
        description="Danh sách các hàm mà model yêu cầu gọi (nếu có).",
    )

    model_config = {
        "json_schema_extra": {
            "example": {
                "reply": "Tôi sẽ kiểm tra trạng thái đơn hàng ORD-20240001 cho bạn.",
                "intent": "check_order_status",
                "tool_calls": [
                    {
                        "id": "call_abc123",
                        "function_name": "CheckOrderStatus",
                        "arguments": {"order_id": "ORD-20240001"},
                    }
                ],
            }
        }
    }
