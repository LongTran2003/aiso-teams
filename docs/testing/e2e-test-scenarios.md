# Kịch bản test E2E trên MS Teams

Bộ kịch bản kiểm thử end-to-end cho bot AISO-Teams (bot → BE → AI → SAP thật).
Dùng để test tay trên Teams và làm checklist cho buổi demo.

> Bot hiểu ngôn ngữ tự nhiên (Việt/Anh) nên câu lệnh chỉ là gợi ý — có thể diễn đạt khác.
> Mọi hành động thay đổi (release/reject/forward) đều có **thẻ xác nhận** trước khi thực thi.

---

## 0. Chuẩn bị trước khi test

| # | Việc cần làm | Ghi chú |
|---|---|---|
| 0.1 | BE + Bot đã publish bản mới nhất lên Azure | Có fix hiển thị số SO + error propagation |
| 0.2 | SAP đã activate bản `cl_abap_behavior_saver_failed` | Để lỗi BAPI trả đúng về bot |
| 0.3 | Chuẩn bị sẵn danh sách SO theo trạng thái | Cần vài order **Open** (chưa xử lý) để test release/reject thành công |
| 0.4 | Biết SAP User ID đang test | VD `DEV-249` |

**Lưu ý trạng thái order (quan trọng):**
- **Release** chỉ thành công với order chưa release (còn delivery block / Open).
- **Reject** chỉ thành công với order chưa delivered/chưa xử lý. Order đã release+deliver sẽ báo lỗi SAP thật (đúng nghiệp vụ).
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

## 4. Release Order (Approve)

| # | Câu lệnh | Kết quả mong đợi |
|---|---|---|
| 4.1 (happy) | `Approve order <SO đang Open>` → bấm **Confirm Approve** | Thẻ "Action completed", **Sales order = đúng số SO** (không phải UNKNOWN), Status = `Released` |
| 4.2 (đã release) | `Approve order <SO đã Released>` | Báo lỗi SAP thật (vd delivery block đã gỡ / không đổi được) |
| 4.3 (huỷ) | `Approve order ...` → bấm **Cancel** | Không thực thi, không đổi trạng thái SAP |
| 4.4 (owner khác) | Release order do user khác sở hữu | Báo `Order owned by another user` |

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

- [ ] Đăng nhập SAP User ID
- [ ] Truy vấn danh sách + chi tiết 1 order
- [ ] Xem KPI summary
- [ ] Release 1 order Open → thành công, số SO đúng
- [ ] Reject 1 order Open → thành công, số SO đúng
- [ ] Forward 1 order → hiện đúng người nhận (SAP User ID)
- [ ] Demo 1 lỗi SAP thật (reject order đã deliver) → bot hiện message lỗi đúng
- [ ] Logout

---

*Cập nhật lần cuối: Sprint 5 — sau khi hoàn tất auth SAP, fix RAP dump, error propagation (`cl_abap_behavior_saver_failed`), và hiển thị số SO.*
