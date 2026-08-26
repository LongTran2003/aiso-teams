# Demo 4: SAP Verify Document
**Người demo:** Nguyễn Minh Quân
**Thời gian:** ~3 phút (lấy SO → mở SAP → verify header + items)
**Mục tiêu:** Chứng minh Teams là kênh, SAP là nơi lưu chứng từ

---

## PHẦN NÓI (chuẩn bị trước)

### Giới thiệu (~15 giây)

> "Phần này mình sẽ verify đơn hàng vừa tạo trên SAP — chứng minh rằng Teams chỉ là kênh tương tác, còn SAP là nơi lưu chứng từ thật sự."

### Lấy SO number (~30 giây)

> "Mình lấy SO number từ Demo 3 — là số ..."

> "Trên Teams, gõ: **'verify [SO number]'**"

- [ ] Bot hiển thị quick info: Customer, Total Amount, Status

### Mở SAP và Verify (~90 giây)

> "Bây giờ mình mở SAP Fiori / VA03 để verify."

> "Đầu tiên, check **Header**:"

- [ ] SO Number (khớp với Teams)
- [ ] Sold-to Party = Customer (khớp)
- [ ] Sales Organization = UE00
- [ ] Order Type = OR
- [ ] Reference = PO-2026-001 (khớp nếu đã update)

> "Tiếp theo, check **Items**:"

- [ ] Item 10: Material A, Qty X, Amount Y (khớp với Teams)
- [ ] Item 20: Material B, Qty X, Amount Y (khớp với Teams)

### Kết luận (~30 giây)

> "Như vậy, dữ liệu trên Teams và SAP hoàn toàn khớp. Teams là giao diện — giúp nhân viên tạo đơn nhanh hơn, nhưng tất cả dữ liệu được lưu chính xác vào SAP. Không có data loss, không có mismatch."

---

## CHECKLIST TRƯỚC DEMO

- [ ] SAP GUI hoặc Fiori đã mở sẵn (tránh mất thời gian tìm app)
- [ ] Đăng nhập SAP với tài khoản có quyền đọc Sales Order
- [ ] SO number từ Demo 3 đã note lại
- [ ] Chuẩn bị sẵn 2 SAP windows: Header view + Item view

---

## BACKUP NẾU GẶP LỖI

| Tình huống | Xử lý |
|-----------|--------|
| SAP không mở được | **PRIMARY:** Hiện video/screenshot đã record sẵn |
| SO không tìm thấy | Kiểm tra lại SO number — có thể nhập sai. Nếu đúng là lỗi SAP, chuyển qua |
| Data không khớp | Giải thích ngay: "Có thể có lỗi sync — đây là bug chúng em cần investigate" |
| Không có quyền đọc | Test với tài khoản khác hoặc nói "cần tài khoản với authorization đầy đủ" |
| Network lag/lỗi | **IMMEDIATE:** Chuyển sang video/screenshot backup |

---

## CHUẨN BỊ VIDEO/SCREENSHOT BACKUP

### Bước 1: Record trước defense

1. Mở SAP GUI/Fiori
2. Tìm 1 Sales Order có sẵn
3. Record màn hình (OBS, Loom, hoặc Windows Game Bar)
4. Show: Header view + Item view
5. Duration: 10-15 giây

### Bước 2: Lưu backup

- Video: `plans/assets/demo-04-sap-verify-backup.mp4`
- Screenshots: `plans/assets/demo-04-sap-header.png`, `demo-04-sap-items.png`

### Bước 3: Khi nào dùng

- SAP không mở được
- Network lag quá 10 giây
- SAP trả lỗi
- Bất kỳ tình huống nào live không work

---

## ĐIỂM NHẤN (để hội đồng chú ý)

1. **Data integrity** — Teams ↔ SAP khớp 100%
2. **SAP là source of truth** — tài liệu thật, không mock
3. **Seamless workflow** — nhân viên không cần biết SAP, chỉ cần Teams

