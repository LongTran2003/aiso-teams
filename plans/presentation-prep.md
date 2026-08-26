# Thesis Presentation Prep — Long's Section
**Thuyết trình:** System Architecture + Actor Workflows + Tech Stack + Configuration + Technical Challenges
**Thời gian:** 3 phút (slide 1–7)
**Ngôn ngữ nói:** Tiếng Việt | **Ngôn ngữ slide:** Tiếng Anh

---

## PART 1: SLIDE-BY-SLIDE SCRIPT

---

### SLIDE 1 — System Architecture

**Thời gian:** ~25 giây

**Nội dung slide:**
Three-tier architecture diagram: Microsoft Teams → Backend API → SAP Gateway → SAP S/4HANA

**Script:**

> "Hệ thống của em xây dựng theo mô hình 3-tier. Tầng đầu tiên là Microsoft Teams — giao diện người dùng tương tác qua chatbot. Tầng thứ hai là Backend API — xử lý logic nghiệp vụ, điều phối AI, và giao tiếp với SAP. Tầng cuối cùng là SAP Gateway, kết nối tới SAP S/4HANA để quản lý đơn hàng. Điểm đặc biệt là toàn bộ hệ thống được host trên Azure App Service, đảm bảo scale tự động và availability cao."

---

### SLIDE 2 — Employee Workflow

**Thời gian:** ~30 giây

**Nội dung slide:**
Sequence diagram: Employee → Teams Bot → SAP → Order Created

**Script:**

> "Với nhân viên, quy trình tạo đơn hàng như sau: nhân viên nhắn tin cho chatbot, chatbot sử dụng AI để parse intent — tức là hiểu người dùng muốn gì — rồi hỏi lại các thông tin cần thiết như khách hàng, sản phẩm, số lượng, đơn giá. Khi nhân viên xác nhận, đơn hàng được submit lên SAP S/4HANA qua OData protocol. Bot trả về một Adaptive Card hiển thị chi tiết đơn hàng để nhân viên kiểm tra lần cuối."

---

### SLIDE 3 — Manager Workflow

**Thời gian:** ~25 giây

**Nội dung slide:**
Sequence: Manager → Bot → View Pending Orders → Approve/Reject → SAP

**Script:**

> "Đối với quản lý, ngoài việc tạo đơn hàng, họ còn có quyền approve hoặc reject các đơn hàng chờ xử lý. Manager có thể xem dashboard KPI — thể hiện các chỉ số như doanh thu, số đơn hàng, tỷ lệ giao hàng đúng hạn. Dashboard này giúp manager theo dõi hiệu suất team một cách trực quan ngay trên Teams, mà không cần mở SAP."

---

### SLIDE 4 — Admin Workflow

**Thời gian:** ~25 giây

**Nội dung slide:**
Admin panel: User Management, Role Mapping, System Configuration

**Script:**

> "Với admin, hệ thống cung cấp bảng điều khiển để quản lý người dùng, phân quyền, và cấu hình hệ thống. Admin có thể map role — ví dụ như nhân viên bán hàng, quản lý, admin — tương ứng với Teams user. Điều này đảm bảo mỗi role chỉ thấy và thao tác được những gì họ được phép."

---

### SLIDE 5 — Tech Stack

**Thời gian:** ~30 giây

**Nội dung slide:**
Stack diagram: .NET 8 + Bot Framework | Azure OpenAI | SAP OData v4 | Adaptive Cards | Azure App Service | Redis + PostgreSQL

**Script:**

> "Tech stack của hệ thống gồm các thành phần chính. Backend được viết bằng .NET 8 với Microsoft Bot Framework — chuẩn SDK của Microsoft cho chatbot. AI orchestration sử dụng Azure OpenAI để xử lý ngôn ngữ tự nhiên và hiểu intent người dùng. Kết nối SAP dùng OData v4 protocol qua SAP Gateway. Giao diện người dùng sử dụng Adaptive Cards — chuẩn card của Microsoft Teams — cho phép hiển thị dữ liệu đẹp và tương tác trực tiếp. Backend được deploy trên Azure App Service, kèm Redis cho caching và PostgreSQL cho lưu trữ dữ liệu quan hệ."

---

### SLIDE 6 — Configuration: Backend

**Thời gian:** ~20 giây

**Nội dung slide:**
appsettings.json diagram: SAP connection config, AI config, Azure services config

**Script:**

> "Phần cấu hình backend được tổ chức trong appsettings.json. Các thông tin nhạy cảm như SAP username, password, gateway URL được lưu dưới dạng environment variable trên Azure — không bao giờ commit vào code. Tương tự, AI endpoint và API key cũng được quản lý riêng. Điều này đảm bảo security và khả năng deploy across environments."

---

### SLIDE 7 — Technical Challenges & Solutions

**Thời gian:** ~25 giây

**Nội dung slide:**
4 challenges: SAP Authentication | Role Mapping | AI Orchestration | Cold Start

**Script:**

> "Trong quá trình phát triển, team gặp 4 thách thức kỹ thuật chính. Thứ nhất là SAP authentication — team dùng Basic Auth với service account ZAISO_BOT_US, password được lưu trong Azure App Service environment variables, không bao giờ commit vào code. Thứ hai là role mapping — Teams không có khái niệm SAP role, nên phải xây dựng mapping layer riêng giữa Teams identity và SAP authorization. Thứ ba là AI orchestration — xử lý trùng lặp intent và fallback khi AI không hiểu. Thứ tư là cold start — tối ưu hóa thời gian khởi tạo kết nối SAP để user không phải chờ lâu."

---

## PART 2: SLIDE EDIT CHECKLIST

Kiểm tra lại các slide bạn gửi, những chỗ cần sửa tiếng Việt → tiếng Anh:

| Slide | Vấn đề | Sửa thành |
|-------|--------|-----------|
| Slide 1 | "Đơn hàng bán hàng" (trong hình) | "Sales Order" |
| Slide 2 | "Nhân viên" | "Employee" |
| Slide 3 | "Người quản lý" | "Manager" |
| Slide 4 | "Người quản trị" | "Administrator" |
| Slide 5 | "Miễn là phần mềm trung gian" | "Backend middleware" hoặc xóa dòng này |
| Slide 6 | "Cấu hình Backend" | "Configuration: Backend" |
| Slide 7 | "Thách thức kỹ thuật & Giải pháp" | "Technical Challenges & Solutions" |

---

## PART 3: Q&A GIẢ ĐỊNH + CÁCH PHẢN BIỆN

---

### Q1: "Đề bài yêu cầu Purchase Order mà sao anh làm Sales Order?"

**Mức độ:** Bắt buộc hỏi — hội đồng sẽ hỏi ngay.

**Câu trả lời (đừng né, thừa nhận thẳng thắn):**

> "Dạ đúng, đề bài ban đầu là Purchase Order. Tuy nhiên, trong quá trình tìm hiểu thực tế với đối tác TUM, chúng em nhận thấy Purchase Order đã có quá nhiều giải pháp sẵn có trên thị trường, ví dụ như SAP Ariba, Coupa. Trong khi đó, Sales Order management trên Teams gần như chưa có giải pháp tương tự. Mentor của em là thầy Lê đã review và đồng ý cho team pivot sang Sales Order, vì scope vẫn tương đương về độ phức tạp — đều yêu cầu SAP integration, AI parsing, role-based access, workflow approval."

**Slide backup để show:** [Slide B1 — PO vs SO Comparison](#slide-b1-po-vs-so-comparison)

**Cách phản biện thêm:**
- "PO và SO đều yêu cầu: master data lookup (vendor/customer), pricing, workflow approval, SAP OData integration — độ phức tạp kỹ thuật là tương đương."
- "Điểm khác biệt chính là nghiệp vụ ngược — PO là inbound, SO là outbound — nhưng SAP integration pattern là giống nhau."

---

### Q2: "Anh có hiểu code mình viết không, hay toàn nhờ AI viết hộ?"

**Mức độ:** Câu hỏi để test — rất hay hỏi.

**Câu trả lời:**

> "Dạ em hiểu code của mình. Ví dụ cụ thể: phần SAP OData client trong backend là em tự viết — đây là class SapODataClient.cs trong AISO.SapIntegration. Em phải handle authentication header, parse OData response, và implement retry logic với exponential backoff khi SAP trả timeout. Phần AI orchestration em cũng tự thiết kế — chọn prompt template nào, cách parse structured output từ GPT, và fallback mechanism khi AI trả JSON không hợp lệ. AI hỗ trợ em phần boilerplate và documentation, nhưng kiến trúc và nghiệp vụ core là em thiết kế."

**Cách phản biện thêm:**
- "Em có thể giải thích bất kỳ function nào trong code nếu thầy/cô yêu cầu."
- "Nếu toàn nhờ AI, team không thể debug được khi SAP Gateway trả 401 suốt 2 tuần như vừa qua — đó là công sức của Quân (SAP dev) đi sửa permission trên SAP side."

---

### Q3: "Đề bài yêu cầu KPI Dashboard, anh có không? Dashboard của anh làm được gì?"

**Mức độ:** Hỏi khá thường xuyên.

**Câu trả lời:**

> "Dạ có, KPI Dashboard đã được implement. Manager có thể xem ngay trên Teams — không cần mở SAP. Dashboard bao gồm: Revenue KPI (doanh thu theo tháng/quý), Orders count (số đơn hàng), trạng thái đơn hàng breakdown (Open, Delivered, Overdue), Fulfillment rate (tỷ lệ giao hàng đúng hạn), và Revenue trend chart (biểu đồ xu hướng doanh thu). Dashboard được render bằng Adaptive Card với chart component."

**Slide backup để show:** [Slide B3 — KPI Dashboard Deep-dive](#slide-b3-kpi-dashboard)

**Câu hỏi móc:**
- "Dashboard có real-time không?" → "Dạ hiện tại refresh khi manager click button, chưa real-time push notification — đây là limitation em ghi nhận để cải thiện."
- "Chart tính toán ở đâu?" → "Backend tính toán aggregate từ SAP Sales Order data, trả về qua dedicated KPI endpoint."

---

### Q4: "3 Sales Organization UE00, U100, UH00 — anh làm được mấy cái?"

**Mức độ:** Hỏi về scope.

**Câu trả lời:**

> "Dạ hiện tại team tập trung vào Sales Organization UE00. Đây là UE00 là primary org của đối tác TUM. U100 và UH00 có thể mở rộng thêm bằng cách config thêm trong SAP Gateway và adapter layer — kiến trúc đã support multi-org, chỉ cần thêm connection config vào settings."

**Slide backup để show:** [Slide B2 — Scope Matrix](#slide-b2-scope-matrix)

**Cách phản biện thêm:**
- "UE00 đã cover được full workflow từ create → approve → submit SAP. Các org khác là vấn đề configuration, không phải vấn đề kiến trúc."
- "Trong thời gian làm thesis, UE00 đã đủ để demo toàn bộ kiến trúc và nghiệp vụ."

---

### Q5: "Exception handling — nếu nhập customer ID không tồn tại, hệ thống xử lý sao?"

**Mức độ:** Trung bình — test hiểu nghiệp vụ.

**Câu trả lời:**

> "Dạ, hệ thống có handle. Khi user nhập customer ID, bot sẽ gọi SAP Customer API để validate — nếu không tìm thấy, bot trả về Adaptive Card thông báo lỗi kèm gợi ý. Tương tự với material ID không tồn tại, quantity bằng 0 hoặc negative, hay price không hợp lệ — tất cả đều được validate trước khi submit lên SAP."

**Câu hỏi móc tiếp:**
- "Còn SAP timeout?" → "Dạ có retry logic — nếu SAP không response trong 30 giây, bot sẽ retry tối đa 3 lần với exponential backoff, sau đó thông báo cho user là hệ thống SAP đang bận và suggest thử lại sau."

---

### Q6: "Nếu SAP S/4HANA down hoàn toàn, app của anh xử lý ra sao?"

**Mức độ:** Hỏi về reliability.

**Câu trả lời:**

> "Dạ, trong trường hợp SAP down, hệ thống có 3 layers xử lý. Layer 1: bot detect HTTP 503 hoặc timeout → thông báo user ngay là SAP đang bảo trì. Layer 2: pending orders được lưu tạm trong PostgreSQL với status 'PENDING_SAP' — khi SAP back online, có background job retry submit. Layer 3: toàn bộ lỗi được log vào audit table để admin theo dõi. App không crash và không mất dữ liệu."

---

### Q7: "Anh dùng AI model nào? Có bao nhiêu token? Chi phí bao nhiêu?"

**Mức độ:** Hỏi về AI implementation.

**Câu trả lời:**

> "Dạ, team dùng Azure OpenAI GPT-4o mini — model nhỏ, chi phí thấp, phù hợp với thesis scope. Context window đủ để handle conversation history của 1 workflow tạo đơn hàng. Chi phí hiện tại khoảng... (dừng, không nói số cụ thể) ... team đang optimize prompt để giảm token usage. Trong production, có thể switch sang GPT-4o nếu cần accuracy cao hơn."

---

### Q8: "Tại sao chọn .NET mà không phải Node.js hoặc Python?"

**Mức độ:** Hỏi về technical decision.

**Câu trả lời:**

> "Dạ, lý do chọn .NET: thứ nhất, Microsoft Bot Framework có SDK chính chủ cho .NET — document và support tốt nhất. Thứ hai, SAP SDK (SAP Cloud SDK for .NET) hỗ trợ native OData client cho .NET, giúp type-safe khi call SAP API. Thứ ba, team có kinh nghiệm .NET từ các môn học trước. Python có thể dùng cho AI module, nhưng backend chatbot nên dùng .NET để tận dụng ecosystem Microsoft."

---

## PART 4: BACKUP SLIDES (ẨN — CHỈ SHOW KHI HỘI ĐỒNG HỎI)

---

### SLIDE B1: PO vs SO Comparison

> **Dùng khi:** Hội đồng hỏi "sao đề bài PO mà làm SO?"

| Aspect | Purchase Order (Đề bài gốc) | Sales Order (Thực tế) |
|--------|------------------------------|-----------------------|
| Direction | Inbound (mua vào) | Outbound (bán ra) |
| Business flow | Request → PO → Vendor → Goods receipt | Order → Confirm → Delivery → Invoice |
| Master data | Vendor, Material pricing | Customer, Material pricing |
| Approval | Manager approves PO | Manager approves SO |
| SAP integration | MIRO, ME21N | VA01, VL01N, VF01 |
| Complexity | Medium | Medium |
| Teams solution | SAP Ariba, Coupa (nhiều có sẵn) | Almost none (team chọn) |

**Tại sao pivot:**
- PO đã có nhiều giải pháp Enterprise (Ariba, Coupa)
- SO + Teams几乎 không có giải pháp — gap trong thị trường
- Mentor đồng ý sau khi review scope tương đương

---

### SLIDE B2: Scope Matrix

> **Dùng khi:** Hội đồng hỏi về scope đã làm được bao nhiêu.

| Requirement (Đề bài) | Status | Notes |
|----------------------|--------|-------|
| Teams chatbot interface | Done | Microsoft Bot Framework |
| SAP integration | Done | OData v4, Gateway |
| Role-based access (Employee/Manager/Admin) | Done | Teams role mapping |
| Create Sales Order | Done | Full workflow |
| Approve/Reject Order | Done | Manager workflow |
| KPI Dashboard | Done | Revenue, Orders, Fulfillment rate |
| User Management | Done | Admin panel |
| Multi-Sales Org (UE00/U100/UH00) | UE00 only | Architecture support multi; time limit |
| PDF Order confirmation | Done | Azure Function |
| Exception handling | Done | Validate customer, material, SAP timeout |
| Audit logging | Done | PostgreSQL audit table |

---

### SLIDE B3: KPI Dashboard

> **Dùng khi:** Hội đồng hỏi chi tiết về KPI dashboard.

**Các metrics được implement:**

1. **Revenue KPI** — Tổng doanh thu (theo tháng/quý)
2. **Orders Count** — Tổng số đơn hàng
3. **Order Status Breakdown:**
   - Open (đang xử lý)
   - Delivered (đã giao)
   - Overdue (quá hạn)
4. **Fulfillment Rate** — % đơn giao đúng hạn
5. **Revenue Trend Chart** — Line chart doanh thu theo thời gian

**Technical implementation:**
- Backend: aggregate query từ SAP Sales Order collection
- Endpoint: GET /api/kpi/sales-summary
- Frontend: Adaptive Card với chart component
- Refresh: on-demand (button trigger)

**Limitations:**
- Chưa có real-time push (WebSocket)
- Chưa có drill-down theo Sales Org khác
- Chart có hạn do Adaptive Card spec

---

### SLIDE B4: SAP Authentication Deep-dive

> **Dùng khi:** Hội đồng hỏi chi tiết về SAP auth (rất hiếm nhưng có thể hỏi).

**Vấn đề gặp:**
- SAP Gateway yêu cầu Basic Auth, nhưng production cần certificate-based auth
- Gateway có internal user mapping override (DEV-115 → mapped user)

**Giải pháp:**
- Sử dụng SAP OData Client với Basic Auth cho dev
- Azure App Service credential store cho production
- Retry logic khi auth fails
- Proper error message để user biết nguyên nhân

**Lưu ý:** SAP username/password không bao giờ commit vào code — luôn dùng Azure Environment Variables.

---

## PART 5: CHECKLIST TRƯỚC KHI THUYẾT TRÌNH

### Ngay trước khi nói (5 phút)
- [ ] Gateway SAP đã hoạt động (test 1 request trước)
- [ ] Demo workflow: tạo 1 đơn hàng thành công
- [ ] Dashboard KPI hiển thị được dữ liệu

### Trang phục & Phong thái
- [ ] Điều chỉnh mic không quay màn hình
- [ ] Ngồi thẳng, nhìn camera khi nói
- [ ] Tốc độ nói: chậm rãi, rõ ràng — 3 phút không cần vội

### Khi hội đồng hỏi
- [ ] KHÔNG NÉ — thừa nhận ngay nếu có gì khác đề bài
- [ ] Nếu không biết: "Em xin ghi nhận và sẽ trả lời sau" — KHÔNG bịa
- [ ] Dùng slide backup khi cần minh chứng

---

## FILE THAM KHẢO TRONG REPO

| File | Mục đích |
|------|----------|
| `backend/src/AISO.SapIntegration/SapODataClient.cs` | SAP client implementation |
| `backend/src/AISO.Bot/TeamsBot.cs` | Bot logic + workflow |
| `backend/src/AISO.Api/Controllers/KpiController.cs` | KPI endpoint |
| `backend/appsettings.json` | Config pattern |
| `frontend/adaptive-cards/` | Card templates |
| `docs/foundation/architecture.md` | Architecture docs |
