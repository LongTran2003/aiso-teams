# Demo 5: Maker-Checker Approval Flow
**Người demo:** Trần Đăng Minh Quân
**Thời gian:** ~3 phút (submit → approve/reject → verify on SAP)
**Mục tiêu:** Quy trình maker-checker khép kín trên Teams + SAP

---

## PHẦN NÓI (chuẩn bị trước)

### Giới thiệu (~15 giây)

> "Phần cuối, mình demo quy trình maker-checker — nhân viên gửi yêu cầu duyệt, quản lý approve hoặc reject, và kết quả được cập nhật thật trên SAP."

### Step 1: Employee gửi yêu cầu duyệt (~45 giây)

> "Mình đăng nhập với tài khoản Employee. Tạo 1 đơn hàng mới hoặc dùng đơn từ Demo 3. Gõ: **'submit for approval [SO number]'**"

- [ ] Bot kiểm tra: đơn đang ở status nào?
- [ ] Nếu chưa submit: gửi yêu cầu → status = Pending Approval
- [ ] Bot thông báo: "Đã gửi yêu cầu duyệt. Đơn đang chờ Manager."

### Step 2: Manager xem và duyệt (~60 giây)

> "Bây giờ chuyển qua tài khoản Manager."

> "Gõ: **'pending approvals'**"

- [ ] Bot hiển thị danh sách đơn chờ duyệt
- [ ] Mình chọn SO number cần duyệt

> "Xem chi tiết → **Approve**"

- [ ] Bot call SAP update status
- [ ] Confirm: "Order [SO number] approved successfully"
- [ ] Status trên SAP đã đổi thành "Approved"

### (Optional) Step 3: Test Reject Flow (~30 giây)

> "Nếu còn thời gian, mình test Reject. Gõ: **'reject [SO number]'** với lý do 'Giá không đúng'"

- [ ] Bot yêu cầu confirm reason
- [ ] Submit reject
- [ ] Status = Rejected, Employee nhận notification

### Verify trên SAP (~30 giây)

> "Sau khi approve, mình verify trên SAP để thấy status đã được cập nhật."

- [ ] Mở VA03 với SO number
- [ ] Status = Approved (hoặc Rejected tùy flow)

### Kết luận (~15 giây)

> "Quy trình maker-checker hoàn chỉnh: Employee gửi → Manager duyệt → SAP cập nhật. Không có bước thủ công nào — tất cả tự động qua chatbot."

---

## CHECKLIST TRƯỚC DEMO

- [ ] 2 tài khoản: Employee + Manager đã sẵn sàng và logged in
- [ ] SO number để test đã chuẩn bị
- [ ] Nếu test reject: chuẩn bị đơn thứ 2 (để vẫn còn đơn approve sau khi reject)
- [ ] SAP đã mở sẵn để verify sau mỗi bước

---

## BACKUP NẾU GẶP LỖI

| Tình huống | Xử lý |
|-----------|--------|
| Không có đơn pending | Tạo nhanh 1 đơn → submit for approval ngay |
| Manager không nhận notification | Kiểm tra notification settings trên Teams; fallback: "Manager chủ động check bằng lệnh 'pending approvals'" |
| SAP không update status | "Đây có thể là lỗi sync — backend đã call SAP nhưng SAP chưa commit. Đội SAP dev sẽ investigate" |
| Không đủ thời gian cho reject | Bỏ qua, chỉ demo approve — vẫn đủ để show workflow |

---

## ĐIỂM NHẤN (để hội đồng chú ý)

1. **End-to-end automation** — không cần email, không cần phone call
2. **Audit trail** — mọi approve/reject được log
3. **Real-time notification** — Manager nhận ngay khi có request
4. **SAP sync** — status cập nhật thật trên SAP

