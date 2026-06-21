# Kịch bản Test (Sprint 3: Bot ↔ SAP Integration)

Tài liệu này hướng dẫn cách test toàn bộ luồng chức năng của Bot trong môi trường Microsoft Teams sau khi đã hoàn tất Sprint 3 (tích hợp thực tế với SAP OData V4).

## Chuẩn bị
1. **Khởi chạy hệ thống**: Đảm bảo Backend (AISO.Api) đang chạy (bằng ngrok hoặc Dev Tunnels).
2. **Teams Client**: Mở ứng dụng AISO Teams Bot trên web/desktop.
3. **SSO Login**: Nếu Bot hiện nút "Log in", hãy bấm vào và đăng nhập bằng tài khoản Microsoft 365 (SSO).

---

## 1. Test Case: Truy vấn danh sách Sales Order (GET)

Mục đích: Kiểm tra ODataQueryBuilder có cấu trúc đúng `$filter` và `$top` hay không.

- **Người dùng chat**: 
  > *"Lấy cho tôi 3 đơn hàng gần đây của khách hàng có mã 100000001."*
- **Bot phản hồi mong đợi**:
  1. Hiển thị thông báo "Đang xử lý..."
  2. Gọi function `GetSalesOrdersFunction`.
  3. Trả về Adaptive Card chứa danh sách 3 đơn hàng của khách hàng "100000001" (dữ liệu thật từ SAP).

---

## 2. Test Case: Tạo Sales Order mới (POST + CSRF)

Mục đích: Kiểm tra cơ chế tự động lấy và truyền `x-csrf-token` và `Cookies`.

- **Người dùng chat**: 
  > *"Tạo một đơn hàng mới cho khách hàng 100000001, mã sản phẩm TG11, số lượng 5 cái."*
- **Bot phản hồi mong đợi**:
  1. Hiển thị "Đang xử lý..."
  2. Gọi `CreateOrderFunction`. Dưới nền, `SapClient` sẽ lấy CSRF Token từ Redis.
  3. POST lên SAP để tạo đơn hàng.
  4. Trả về thông báo thành công: "Đã tạo đơn hàng thành công. Mã đơn hàng (SO Number): [Mã mới sinh ra từ SAP]."

---

## 3. Test Case: Cập nhật Reference (POST)

Mục đích: Cập nhật field mở rộng của đơn hàng qua OData Action.

- **Người dùng chat**: 
  > *"Cập nhật reference cho đơn hàng [Mã SO vừa tạo ở bước 2] thành 'VIP_ORDER_01'."*
- **Bot phản hồi mong đợi**:
  1. Gọi `UpdateOrderReferenceFunction`.
  2. Truyền POST request chứa `REQUESTING_TEAMS_USER` (từ context Teams) và `NEW_REFERENCE`.
  3. Trả về Adaptive Card thông báo cập nhật thành công.

---

## 4. Test Case: Hủy đơn hàng / Reject (POST)

Mục đích: Thực hiện OData action phức tạp (hủy đơn với lý do).

- **Người dùng chat**: 
  > *"Hủy đơn hàng [Mã SO vừa tạo ở bước 2] đi, lý do là khách hàng đổi ý không mua nữa."*
- **Bot phản hồi mong đợi**:
  1. Gọi `RejectOrderFunction`.
  2. Trả về thông báo thành công: "Đã hủy đơn hàng [Mã SO] với lý do: Khách hàng đổi ý."

---

## 5. Test Case: Kiểm tra lại trạng thái (GET by ID)

Mục đích: Đảm bảo đơn hàng vừa hủy ở bước 4 thực sự đã cập nhật trạng thái ở SAP.

- **Người dùng chat**: 
  > *"Kiểm tra trạng thái đơn hàng [Mã SO] giúp tôi."*
- **Bot phản hồi mong đợi**:
  1. Gọi `CheckOrderStatusFunction`.
  2. Trả về Adaptive Card chứa chi tiết đơn hàng, trong đó Status thể hiện là **Canceled / Rejected**.

---

## 🎯 Dấu hiệu nhận biết lỗi (Troubleshooting)
- Nếu Bot báo **"Không thể tạo đơn hàng / Lỗi kết nối"**: Hãy mở Console Backend để xem log.
  - Lỗi `403 Forbidden`: Cơ chế CSRF Token bị sai lệch (Cookie không khớp với Token).
  - Lỗi `401 Unauthorized`: Mật khẩu tài khoản SAP cấu hình trong `appsettings.json` bị sai.
  - Lỗi `400 Bad Request`: Payload gửi lên (Mã sản phẩm `TG11` hoặc Mã KH) không tồn tại trong hệ thống SAP. Cần đổi lại mã khác cho khớp với master data của hệ thống S40.
