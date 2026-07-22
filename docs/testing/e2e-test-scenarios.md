# Kịch bản test E2E trên MS Teams

Bộ kịch bản kiểm thử end-to-end cho bot AISO-Teams (bot → BE → AI → SAP thật).
Dùng để test tay trên Teams và làm checklist cho buổi demo.

> Bot hiểu ngôn ngữ tự nhiên (Việt/Anh) nên câu lệnh chỉ là gợi ý — có thể diễn đạt khác.
> Mọi hành động thay đổi (release/reject/forward) đều có **thẻ xác nhận** trước khi thực thi.

---

## 0. Chuẩn bị trước khi test

| # | Việc cần làm | Ghi chú |
|---|---|---|
| 0.1 | BE + Bot đã publish bản RBAC + maker-checker | Branch `feature/be-rbac-role-gating` / đã merge |
| 0.2 | Migration Postgres: `Role`, `SalesOrg`, `order_approvals` | Seed 3 role trước khi test |
| 0.3 | AI đã deploy schema `RequestRelease` / `ApproveOrder` / … | Hot-load `ai/functions/*.json` |
| 0.4 | SAP đã activate `cl_abap_behavior_saver_failed` | Lỗi BAPI trả đúng về bot |
| 0.5 | Chuẩn bị ≥ 3 SO **Open** (owner = Employee) | Mỗi lệnh một order riêng |
| 0.6 | Biết SAP User ID + role từng người test | Employee / Manager (`SalesOrg`) / Admin |

**Demo có thời gian (Mentor Review):** xem [`demo-test-script.md`](./demo-test-script.md).

**Lưu ý trạng thái order (quan trọng):**
- **Release / Approve** chỉ thành công với order chưa release (còn delivery block / Open).
- **Employee không release trực tiếp** — dùng `RequestRelease`; Manager `ApproveOrder`.
- **Reject** chỉ thành công với order chưa delivered/chưa xử lý.
- Nên chuẩn bị **mỗi lệnh một order riêng** để tránh vướng trạng thái.

---

## 1. Đăng nhập / Đăng xuất

| # | Câu lệnh | Kết quả mong đợi |
|---|---|---|
| 1.1 | `hi` (lần đầu, chưa map) | Bot khởi động luồng đăng nhập, hỏi SAP User ID |
| 1.2 | Nhập SAP User ID hợp lệ (vd `DEV-249`) | Map thành công, báo đã đăng nhập |
| 1.3 | `help` | Hiện thẻ hướng dẫn các lệnh |
| 1.4 | `logout` | Báo "Đã đăng xuất tài khoản SAP thành công" |
| 1.5 | Gõ lệnh nghiệp vụ khi chưa đăng nhập | Bot yêu cầu đăng nhập trước |
| 1.6 | `cancel` / `thoát` khi đang giữa một tiến trình | Huỷ tiến trình, báo có thể bắt đầu lại |

---

## 2. Truy vấn Sales Order

| # | Câu lệnh | Kết quả mong đợi |
|---|---|---|
| 2.1 | `Show my sales orders` / `Xem danh sách đơn hàng` | Danh sách SO (dạng thẻ/list) |
| 2.2 | `Show orders of customer <mã KH>` | Lọc theo khách hàng |
| 2.3 | `Show detail of order 0000000009` / `Xem chi tiết đơn 9` | Thẻ chi tiết: khách, giá trị, trạng thái, items |
| 2.4 | `Check status of order 0000000009` | Trả về trạng thái hiện tại của order |
| 2.5 | Hỏi order không tồn tại (vd `order 9999999999`) | Bot báo không tìm thấy (không crash) |

---

## 3. KPI / Báo cáo

| # | Câu lệnh | Kết quả mong đợi |
|---|---|---|
| 3.1 | `Show KPI summary` / `Tổng quan KPI` | Thẻ KPI tổng hợp |
| 3.2 | `KPI by customer <mã KH>` | KPI theo khách hàng |
| 3.3 | `KPI by product <mã SP>` | KPI theo sản phẩm |
| 3.4 | `Show overdue orders` / `Đơn hàng quá hạn` | Danh sách order quá hạn |

---

## 4. Maker-checker Release (RBAC Phase B)

| # | Role | Câu lệnh | Kết quả mong đợi |
|---|---|---|---|
| 4.1 | Employee | `Release order <SO Open>` / Approve trực tiếp | **`NOT_AUTHORIZED`** (cần Manager+) |
| 4.2 | Employee | `Request release for order <SO Open>` hoặc Confirm Release trên card | **`ReleaseRequested`**; SAP chưa release |
| 4.3 | Employee | `Show pending approvals` | **`NOT_AUTHORIZED`** |
| 4.4 | Manager | `Show pending approvals` | List chứa `SO` vừa request (đúng VKORG) |
| 4.5 | Manager | `Approve order <SO pending>` | **`Approved`** + SAP released; số SO đúng (không UNKNOWN) |
| 4.6 | Manager | `Reject approval for order <SO pending khác>` | **`ApprovalRejected`**; SAP không release |
| 4.7 | Manager | Approve SO **khác SalesOrg** của Manager | Bị từ chối (scope VKORG) |
| 4.8 | Manager/Admin | `Release order <SO>` trực tiếp (không pending) | Vẫn được (Manager+); số SO đúng |
| 4.9 | Admin | `View audit log` | Thấy các dòng Success / Denied gần đây |
| 4.10 | — | Owner khác (SAP ownership) | `Order owned by another user` |

---

## 5. Reject Order

| # | Câu lệnh | Kết quả mong đợi |
|---|---|---|
| 5.1 (happy) | `Reject order <SO đang Open>` → chọn reason → **Confirm Reject** | Thẻ "Action completed", **số SO đúng**, Status = `Rejected` |
| 5.2 (đã deliver) | `Reject order <SO đã release+deliver>` | Báo lỗi SAP thật, vd `The material in item 000010 cannot be changed` — **đây là đúng** (chứng minh error propagation hoạt động) |
| 5.3 (thiếu reason) | Reject nhưng không chọn reason | Bot bắt buộc chọn reason (`*`) |
| 5.4 (huỷ) | `Reject order ...` → **Cancel** | Không thực thi |

---

## 6. Forward Order

| # | Câu lệnh | Kết quả mong đợi |
|---|---|---|
| 6.1 (happy) | `Forward order <SO>` → chọn Recipient → (note tuỳ chọn) → **Send** | `Sales order <số SO> has been forwarded to <SAP User ID người nhận>` |
| 6.2 (hiển thị) | Kiểm tra tên người nhận trong message | Hiện **SAP User ID** (vd `DEV-249`), không nhập nhằng |
| 6.3 (huỷ) | `Forward order ...` → **Cancel** | Không thực thi |

---

## 7. Xử lý lỗi & Edge cases

| # | Tình huống | Kết quả mong đợi |
|---|---|---|
| 7.1 | Câu lệnh mơ hồ / ngoài phạm vi | Bot trả lời lịch sự, không crash |
| 7.2 | Lệnh nghiệp vụ khi SAP trả lỗi | Bot hiện thẻ "Something went wrong" + Error code + Details (message SAP thật) |
| 7.3 | Gõ liên tục nhiều lệnh | Không kẹt trạng thái, mỗi lệnh xử lý độc lập |
| 7.4 | Order ID sai định dạng (vd `order abc`) | Bot báo lỗi hợp lý |

---

## 8. Checklist demo (rút gọn)

Chi tiết theo phút: [`demo-test-script.md`](./demo-test-script.md).

- [ ] Seed 3 role (Employee / Manager+VKORG / Admin)
- [ ] Employee: query + KPI card
- [ ] Employee: release trực tiếp → `NOT_AUTHORIZED`
- [ ] Employee: `RequestRelease` → pending
- [ ] Manager: `GetPendingApprovals` → `ApproveOrder` → SAP released, số SO đúng
- [ ] Manager: `RejectApproval` trên SO khác
- [ ] Admin: `ViewAuditLog`
- [ ] Reject + Forward 1 order (số SO / recipient SAP ID đúng)
- [ ] (Tuỳ chọn) 1 lỗi SAP thật
- [ ] Logout

---

*Cập nhật: Sprint 5 — RBAC Phase B + maker-checker, KPI card, auth SAP / error propagation.*
