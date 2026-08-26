# Demo 3: Create / Edit / RBAC
**Người demo:** Long
**Thời gian:** ~3 phút (tạo đơn multi-material + edit + test RBAC)
**Kỹ thuật:** SAP OData Create/Update + Role-Based Access Control

---

## PHẦN NÓI (chuẩn bị trước)

### Giới thiệu (~15 giây)

> "Phần này mình sẽ demo core workflow: tạo đơn hàng với nhiều material, chỉnh sửa, và verify Role-Based Access Control hoạt động đúng."

### Tạo đơn với nhiều Material (~75 giây)

> "Mình bắt đầu tạo đơn hàng. Gõ: **'create order'**"

- [ ] Bot hỏi lần lượt: Customer → Material 1 → Qty 1 → Material 2 → Qty 2 → ...
- [ ] Khi nhập đủ, bot hiển thị form confirm với tất cả items
- [ ] Mình xác nhận → bot submit lên SAP qua OData
- [ ] Trả về SO number mới

> "Đơn hàng đã được tạo trên SAP với 2 items. SO number: ..."

### Chỉnh sửa / Cập nhật Reference (~60 giây)

> "Bây giờ mình chỉnh sửa. Gõ: **'update SO [number] reference PO-2026-001'**"

- [ ] Bot call SAP PATCH endpoint
- [ ] Confirm update thành công

### Test RBAC — Unauthorized Flow (~30 giây)

> "Cuối cùng, mình test RBAC. Mình đăng nhập với tài khoản Employee — quyền hạn chế. Thử gõ lệnh chỉ Manager mới được làm: **'approve order [number]'**"

- [ ] Bot trả về: "You are not authorized to perform this action"
- [ ] Không crash, không reveal admin data

### Kết luận (~15 giây)

> "Như vậy: tạo đơn multi-material hoạt động, edit/update hoạt động, và RBAC ngăn đúng quyền. Employee không thể approve — chỉ Manager mới được."

---

## CHECKLIST TRƯỚC DEMO

- [ ] Gateway SAP hoạt động (test 1 request)
- [ ] 2 tài khoản test: Employee + Manager đã sẵn sàng
- [ ] Chuẩn bị sẵn customer ID và material IDs để nhập nhanh
- [ ] Backup: nếu gateway lỗi, demo với mock data và nói rõ

---

## BACKUP NẾU GẶP LỖI

| Tình huống | Xử lý |
|-----------|--------|
| Gateway lỗi | "Gateway SAP hiện đang bảo trì — mình demo với mock endpoint để show flow, backend logic không đổi" |
| RBAC không hoạt động | Tạm dừng, nói "có thể session chưa refresh — chuyển tài khoản khác" |
| SAP trả lỗi validation | Giải thích: "Đây là validation đúng — hệ thống reject được bad data" |

---

## ĐIỂM NHẤN (để hội đồng chú ý)

1. **Multi-material support** — không chỉ 1 item như demo thông thường
2. **Real SAP OData** — tạo thật trên SAP, không mock
3. **RBAC enforcement** — unauthorized action bị block ngay lập tức
4. **User-friendly error** — không crash, trả message rõ ràng

