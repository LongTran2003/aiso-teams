# Demo 1: Natural Language Inquire (EN/VI) — MAIN EVENT
**Người demo:** Thúy
**Thời gian:** ~3.5 phút (đẩy mạnh vì đây là điểm khác biệt chính)
**Kỹ thuật:** AI parsing + keyword fallback + prompt/response visibility

---

## PHẦN NÓI (chuẩn bị trước)

### Giới thiệu (~20 giây)

> "Phần này là điểm khác biệt chính của hệ thống so với giải pháp có sẵn. Thay vì menu bấm, nhân viên có thể hỏi bằng tiếng Anh hoặc tiếng Việt — AI sẽ parse intent và chọn đúng function. Mình sẽ show cả AI log để thấy bot thật sự hiểu."

### Demo tiếng Anh (~60 giây)

> "Bây giờ mình thử tiếng Anh. Gõ: **'show me my orders'**"

- [ ] Bot parse intent → call GetOrders
- [ ] Trả về danh sách orders

> "Thử tiếp: **'any overdue orders?'**"

- [ ] Bot hiểu "overdue" → call GetOrders with status filter Overdue
- [ ] Trả về danh sách orders quá hạn

> "Và: **'check SO number 00042'**"

- [ ] Bot extract SO number → call GetOrderDetail(42)
- [ ] Trả về order detail

### Demo tiếng Việt (~60 giây)

> "Bây giờ thử tiếng Việt. Gõ: **'đơn hàng của tôi'**"

- [ ] Bot parse Vietnamese → call GetOrders
- [ ] Trả về danh sách

> "Tiếp: **'có đơn nào quá hạn không'**"

- [ ] Bot hiểu "quá hán" = overdue
- [ ] Trả về danh sách

> "Và: **'xem đơn 00042'**"

- [ ] Bot extract number → call GetOrderDetail(42)

### Demo câu phức tạp (~45 giây)

> "Thử câu phức tạp hơn: **'tôi muốn xem đơn hàng tháng 6 của khách hàng A mà chưa giao'**"

- [ ] Bot parse: intent=GetOrders, filter=month:6, customer:A, status:Open
- [ ] Trả về filtered results

### Show AI Log (~30 giây)

> "Để thấy rõ AI hoạt động, mình show prompt và response. [Mở backend log]"

- [ ] Show: User message → AI parsed → Function called → Response
- [ ] Hội đồng thấy được AI thật sự parse chứ không phải keyword match đơn giản

### Giải thích kỹ thuật (~20 giây)

> "Hệ thống dùng **AI + keyword hybrid**. Câu đơn giản như 'đơn của tôi' → keyword match nhanh. Câu phức tạp → AI parse intent. Đây là design decision để balance giữa speed và accuracy."

---

## CHECKLIST TRƯỚC DEMO

- [ ] Backend logging đã bật (show được prompt/response)
- [ ] Chuẩn bị sẵn orders có trạng thái khác nhau (Open, Delivered, Overdue)
- [ ] Backup: viết sẵn trigger keywords nếu AI không stable
- [ ] Nếu AI lỗi: fallback sang menu commands ngay lập tức

---

## BACKUP NẾU GẶP LỖI

| Tình huống | Xử lý |
|-----------|--------|
| AI không hiểu câu | Fallback ngay: "AI đang bận, thử gõ 'help' để xem commands" → dùng menu |
| AI trả sai function | Giải thích: "AI có thể nhầm — đây là limitation, đang optimize prompt" |
| Không có đơn Overdue | Nói: "hiện tại không có overdue, mình sẽ tạo 1 đơn quá hạn để test" |
| Không show được AI log | Bỏ qua phần log, tập trung vào demo live |

---

## ĐIỂM NHẤN (để hội đồng chú ý)

1. **AI + Keyword Hybrid** — không phải chỉ menu bấm
2. **Đa ngôn ngữ** — Anh + Việt
3. **Intent parsing** — bot hiểu "đơn nào", "có không", "xem" = intent khác nhau
4. **Prompt visibility** — show AI log để hội đồng thấy thật sự có AI

