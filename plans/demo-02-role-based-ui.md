# Demo 1: Role-Based UI (Rút gọn)
**Người demo:** Tiến
**Thời gian:** ~2 phút (rút gọn từ 3 phút)
**Kênh:** Live demo trên Teams

---

## PHẦN NÓI (chuẩn bị trước)

### Giới thiệu (~15 giây)

> "Mình sẽ demo phần Role-Based UI — cùng một màn hình nhưng hiển thị khác nhau tùy theo role của người dùng."

### Demo Role Switching (~75 giây)

> "**Demo 1 — Employee view:** Đây là màn My Orders khi đăng nhập với quyền Employee. Nhìn các nút bên phải: chỉ có Create, Edit, View. Không có Approve hay Admin."

> "**Demo 2 — Manager view:** Bây giờ mình chuyển sang tài khoản Manager. Cùng màn My Orders — nhưng các nút đã khác: thêm Approve, Reject, Dashboard. Employee không thấy được các nút này."

> "**Demo 3 — Admin view:** Chuyển sang Admin. Thêm User Management, System Config. Mỗi role chỉ thấy những gì được phép."

### Kết luận (~15 giây)

> "Điểm quan trọng: đây không phải 3 màn hình khác nhau, mà là **cùng 1 component** với RolePolicy filter. Khi đổi user, policy tự động render đúng UI — không cần reload, không cần redirect."

---

## CHECKLIST TRƯỚC DEMO

- [ ] 3 tài khoản test đã login sẵn: Employee + Manager + Admin
- [ ] Cửa sổ Teams đã mở sẵn 3 tabs cho 3 user
- [ ] Switch nhanh giữa các profile Chrome/Teams

---

## BACKUP NẾU GẶP LỖI

| Tình huống | Xử lý |
|-----------|--------|
| Không switch được profile | Dùng 3 cửa sổ Teams khác nhau, mỗi cửa sổ 1 user |
| SAP lỗi | Không ảnh hưởng — Demo 1 chỉ show UI, không gọi SAP |
| Đơn hàng trống | Dùng mock data, nói rõ "đang dùng sample data để demo UI" |

---

## ĐIỂM NHẤN (để hội đồng chú ý)

1. **1 Component — 3 Views** — cùng code, khác policy
2. **Real-time switching** — không reload, không redirect
3. **Security** — UI không hiển thị những gì user không được phép làm

