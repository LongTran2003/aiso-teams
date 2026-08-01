# Mentor backlog — tuần sau (Mon)

Ghi nhanh các đầu việc còn dang dở / đã bàn với tester Thuý để báo cáo GV.

## Đã làm / đang merge

| Item | Status |
|---|---|
| P0 GetOrderDetail + soft-lock | Done (#208) |
| Block reject after delivery | Done (#209) |
| P1 ẩn Sales Org filter Manager + Approval Waiting | Done (#210) |
| HasInvalidMaterial filter + detail warning | Done (#211) |
| T1/T2 SAP link allow-list (`sap_link_assignments`) | PR #214 — **ops còn**: seed assignment; mỗi acc mới = admin.cloud + SAP role + 1 dòng Postgres |
| T3 Help newbie flow + VI `hướng dẫn` | Branch `cursor/t3-newbie-help-flow-card` |

## Còn lại (ưu tiên)

| ID | Việc | Owner | Note |
|---|---|---|---|
| T1-ops | Seed `sap_link_assignments` + smoke link Thuý/Tiến/Long | Long | Bảng trống = logout/re-link sẽ bị chặn |
| T4 | Chuẩn hoá list SO: **my = owner only** (A) | BE + AI | PR #216 |
| T5 | Search customer theo **name** (`contains` CustomerName) | BE | PR #217 |
| T6 | Request release NL → **confirm card** trước submit | BE | Branch `cursor/t6-request-release-confirm-card` |
| T7 | Forward multi-turn / card chọn recipient | BE (+ AI) | |
| SAP | Expose KPI entities nếu muốn bỏ fallback; confirm OwnerSapUser | Quân | KpiByProduct/Customer CDS đã có một phần |
| Auth | Fiori-like login | — | **Tạm bỏ** — bàn lại GV (ý Long ghi nhầm trong PDF Thuý) |
| — | PO reference tiếng Việt | — | Không bug — data SAP |

## Demo SO ổn định

- `0000012912` (material hợp lệ — Quân confirm)
