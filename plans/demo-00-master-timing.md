# Demo Reel — Master Timing Sheet
**Tổng thời gian:** 15 phút (900 giây) cho 5 demo
**Phân bổ:** 3 phút mỗi demo + 1 phút transition giữa các demo

---

## TIMING CHI TIẾT

| Thứ tự | Demo | Người | Thời gian | Bắt đầu | Kết thúc |
|--------|------|-------|-----------|---------|---------|
| 1 | Thúy — NL Inquire (EN/VI) **← MAIN EVENT** | Thúy | 0:00 - 3:30 | 0:00 | 3:30 |
| 2 | Tiến — Role-Based UI | Tiến | 3:30 - 5:30 | 3:30 | 5:30 |
| 3 | Long — Create / Edit / RBAC | Long | 5:30 - 8:30 | 5:30 | 8:30 |
| 4 | Quân — SAP Verify | N.M. Quân | 8:30 - 11:30 | 8:30 | 11:30 |
| 5 | T.Đ.M. Quân — Maker-Checker | T.Đ.M. Quân | 11:30 - 15:00 | 11:30 | 15:00 |

---

## TRANSITION NOTES

| Từ | Sang | Ai nói | Gì |
|----|------|--------|-----|
| Demo 1 | Demo 2 | Thúy hoặc host | "Cảm ơn Thúy. Tiếp theo, Tiến sẽ demo Role-Based UI." |
| Demo 2 | Demo 3 | Tiến hoặc host | "Cảm ơn Tiến. Long sẽ demo core workflow tạo đơn." |
| Demo 3 | Demo 4 | Long hoặc host | "Cảm ơn Long. Quân sẽ verify đơn hàng trên SAP." |
| Demo 4 | Demo 5 | Quân hoặc host | "Cảm ơn Quân. Phần cuối, Đăng Minh Quân sẽ demo maker-checker." |

---

## CRITICAL CHECKLIST (TRƯỚC KHI BẮT ĐẦU)

### Mọi người đều làm
- [ ] Gateway SAP test — chạy 1 curl request trước 15 phút
- [ ] Tất cả tài khoản test đã login sẵn (không mất thời gian login)
- [ ] SAP GUI/Fiori đã mở sẵn
- [ ] Backup video/screenshots đã sẵn sàng (đặc biệt cho Demo 4)
- [ ] Backup plan cho từng demo đã đọc

### Ai làm gì
- [ ] Thúy: kiểm tra AI parsing + backend log có hiện được không
- [ ] Tiến: kiểm tra 3 tài khoản (Employee + Manager + Admin)
- [ ] Long: kiểm tra tài khoản Employee + Manager
- [ ] Quân: record video SAP backup + kiểm tra SAP login
- [ ] T.Đ.M. Quân: kiểm tra tài khoản Employee + Manager + SO number

---

## NẾU HẾT THỜI GIAN

**Ưu tiên giữ lại (theo thứ tự):**
1. Demo 3 — Create/Edit/RBAC (core feature)
2. Demo 5 — Maker-Checker (workflow quan trọng)
3. Demo 1 — Teams UX (impressive demo)
4. Demo 4 — SAP Verify (nếu gateway lỗi thì bỏ)
5. Demo 2 — NL Inquire (nếu AI không stable thì bỏ)

---

## FILES TRONG THƯ MỤC

```
plans/
├── demo-00-master-timing.md      ← file này
├── demo-01-nl-inquire.md        ← Thúy (MAIN EVENT)
├── demo-02-role-based-ui.md     ← Tiến
├── demo-03-create-edit-rbac.md  ← Long
├── demo-04-sap-verify.md        ← Quân
└── demo-05-maker-checker.md     ← T.Đ.M. Quân
```
