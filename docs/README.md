# AISO Teams Bot - Documentation Hub

Chào mừng đến với thư mục tài liệu (`docs/`) của dự án AISO Teams Bot. Nơi đây lưu trữ toàn bộ các tài liệu kỹ thuật, quy trình, đặc tả và báo cáo đồ án của nhóm.

## 📂 Cấu trúc thư mục (Folder Structure)

Để dễ dàng tìm kiếm và quản lý, các tài liệu được phân loại vào các thư mục sau:

- **`/planning/`**: Kế hoạch Sprint, Task list, Roadmap của dự án.
- **`/foundation/`**: Các tài liệu nền tảng, kiến trúc tổng thể, quy chuẩn code (Naming Convention) và luồng Git.
- **`/business/`**: Tài liệu nghiệp vụ, phân tích yêu cầu từ phía người dùng/business.
- **`/specifications/`**: Đặc tả kỹ thuật chi tiết (API, Technical Design, SAP Integration).
- **`/sap-management/`**: Các tài liệu liên quan đến cấu hình hệ thống SAP, OData Services, ABAP.
- **`/testing/`**: Test plan, kịch bản test (Test Cases) và báo cáo kết quả QA.
- **`/user/`**: Hướng dẫn sử dụng cho người dùng cuối (User Manuals, FAQs).
- **`/thesis/`**: Các file báo cáo, đồ án môn học, slide thuyết trình nộp cho trường.
- **`/api-contracts/`**: Các file định nghĩa giao tiếp API (Swagger/OpenAPI json/yaml) nếu có.
- **`/config/`**: Các file cấu hình mẫu hoặc hướng dẫn set up biến môi trường.

## 📝 Quy định viết Tài liệu (Conventions)

Để giữ cho thư mục `docs/` luôn gọn gàng và chuyên nghiệp, mọi thành viên vui lòng tuân thủ các quy tắc sau:

1. **Chuẩn hóa tên file và thư mục**:
   - Tất cả thư mục và file (kể cả `.md`, `.docx`, `.pdf`) đều phải đặt tên theo chuẩn **`kebab-case`** (chữ thường, cách nhau bằng dấu gạch ngang).
   - ❌ Sai: `My Document.md`, `API_Spec.docx`, `01-Planning`
   - ✅ Đúng: `my-document.md`, `api-spec.docx`, `planning`

2. **Định dạng tài liệu chính**:
   - Ưu tiên sử dụng định dạng **Markdown (`.md`)** cho các tài liệu kỹ thuật để dễ dàng review trên GitHub và quản lý version control.
   - Các file Word (`.docx`), Excel (`.xlsx`) chỉ nên dùng cho các báo cáo bắt buộc theo format của trường (như phần `thesis/` hoặc Deliverable Doc).

3. **Cập nhật tài liệu**:
   - Khi có sự thay đổi lớn về code (như đổi API, thêm biến môi trường), developer có trách nhiệm cập nhật file tài liệu tương ứng trong cùng một Pull Request.

## 🚀 Git Workflow (Nhắc nhở quan trọng)

> **TUYỆT ĐỐI KHÔNG PUSH CODE TRỰC TIẾP LÊN NHÁNH `develop` HOẶC `main`.**

- Dù chỉ là sửa/thêm một file tài liệu nhỏ trong `docs/`, bạn cũng bắt buộc phải:
  1. Tạo nhánh mới (Ví dụ: `docs/update-readme`).
  2. Commit và Push lên nhánh đó.
  3. Tạo **Pull Request (PR)** vào `develop` và nhờ reviewer approve.
- *Lưu ý:* Việc GitHub đôi khi cho phép push thẳng lên `develop` là do quyền **Administrator** của người tạo repo (bỏ qua Branch Protection). Quy định của nhóm là **tự giác tuân thủ quy trình PR**.

---
*Vui lòng xem thêm chi tiết về quy trình Git tại [git-workflow.md](./foundation/git-workflow.md).*
