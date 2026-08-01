# Kịch bản demo Mentor Review 2 — AISO-Teams

> Mục tiêu: chứng minh **RBAC 3 role + maker-checker** (Phase B: BE enforce) end-to-end trên MS Teams thật.
> Thời lượng gợi ý: **12–15 phút** (+ 2 phút backup nếu lỗi).

Liên quan: [#157](https://github.com/LongTran2003/aiso-teams/issues/157) · [#158](https://github.com/LongTran2003/aiso-teams/issues/158)

---

## 0. Chuẩn bị (trước ngày demo ≥ 1 ngày)

### 0.1 Deploy & DB

| # | Việc | Owner | Done |
|---|---|---|---|
| P1 | Merge + deploy BE/Bot bản RBAC (`feature/be-rbac-role-gating`) | BE | [ ] |
| P2 | Deploy AI (có schema `RequestRelease`, `ApproveOrder`, …) | AI | [ ] |
| P3 | Chạy migration Postgres (`Role`, `SalesOrg`, `order_approvals`, **`sap_link_assignments`**) | BE | [ ] |
| P4 | Smoke: bot online, login OK | BE | [ ] |

### 0.2 Seed tài khoản test (Postgres)

**Bước A — gán SAP ID trước khi tester link** (`sap_link_assignments`):

| Role | Teams email | `SapUserId` | `SalesOrg` |
|---|---|---|---|
| **Employee** | email Teams của Thuý / Tiến | `DEV-024` | `TV01` (khớp VKORG SO) |
| **Manager** | email Teams của Long | `DEV-249` | `TV01` |
| **Admin** | email Teams Admin | `DEV-xxx` | `null` |

```sql
INSERT INTO public.sap_link_assignments
  ("Id", "SapUserId", "TeamsEmail", "TeamsUserId", "Role", "SalesOrg", "CreatedAt", "UpdatedAt")
VALUES
  (gen_random_uuid(), 'DEV-024', 'lethuy@aisoteam.onmicrosoft.com', NULL, 'Employee', 'TV01', NOW(), NOW()),
  (gen_random_uuid(), 'DEV-249', 'your-manager@aisoteam.onmicrosoft.com', NULL, 'Manager', 'TV01', NOW(), NOW());
-- Adjust emails to real M365 accounts. One SapUserId and one email each (unique indexes).
```

> Bot **chỉ** cho link đúng SAP ID đã gán cho email/Teams user đó — không gắn ID người khác.

**Bước B — sau khi đã link**, có thể chỉnh lại role/org trên `user_mappings` nếu cần:

```sql
-- Kiểm tra VKORG đang pending
SELECT "SoNumber", "SalesOrg", "Status" FROM public.order_approvals WHERE "Status" = 'Pending';

UPDATE public.user_mappings
SET "Role" = 'Employee', "SalesOrg" = 'TV01', "UpdatedAt" = NOW()
WHERE "SapUserId" = 'DEV-024';

UPDATE public.user_mappings
SET "Role" = 'Manager', "SalesOrg" = 'TV01', "UpdatedAt" = NOW()
WHERE "SapUserId" = 'DEV-249';
```

| Role | Teams user (người demo) | `SapUserId` (ví dụ) | `Role` | `SalesOrg` |
|---|---|---|---|---|
| **Employee** | Le Thi Thanh Thuy / Vu Ngoc Tien | `DEV-024` | `Employee` | khớp VKORG order (vd `TV01`) |
| **Manager** | Tran Long | `DEV-249` | `Manager` | **cùng VKORG order** (vd `TV01`) |
| **Admin** | Người C / cùng máy khác | `DEV-xxx` | `Admin` | `null` |

> **Quan trọng:** `SalesOrg` Manager phải khớp `order_approvals.SalesOrg` (lấy từ SAP VKORG của SO). Sai org → `Show pending approvals` ra empty dù đã RequestRelease.
### 0.3 Dữ liệu SAP

| # | Cần có | Ghi chú |
|---|---|---|
| D1 | ≥ **3 SO Open** thuộc owner Employee (`zaiso_so_map` = SAP ID Employee) | Mỗi bước 1 order riêng |
| D2 | Ít nhất 1 SO Open thuộc **cùng VKORG** Manager (`UE00`) | Để Approve trong scope |
| D3 | (Tuỳ chọn) 1 SO đã Delivered | Demo lỗi nghiệp vụ SAP |
| D4 | Ghi sẵn số SO: `SO_A`, `SO_B`, `SO_C` | Dán vào script khi rehearse |

### 0.4 Backup

| # | Việc | Done |
|---|---|---|
| B1 | Video demo quay sẵn (full flow maker-checker) | [ ] |
| B2 | Screenshot Adaptive Card KPI / list / success | [ ] |

---

## 1. Kịch bản demo theo thời gian (live)

### Act 0 — Mở đầu (1 phút)

Nói ngắn: *Bot MS Teams → AI → .NET BE → SAP S/4. Hôm nay demo phân quyền Employee / Manager / Admin và luồng maker-checker phê duyệt release.*

---

### Act 1 — Employee (maker) · ~4 phút

**Đăng nhập bằng tài khoản Employee.**

| # | Hành động / câu lệnh | Kỳ vọng | Pass |
|---|---|---|---|
| E1 | `hi` → login SAP ID Employee | Map OK | [ ] |
| E2 | `Show my sales orders` | Card **My sales orders** — chỉ đơn `OwnerSapUser` = user đã link | [ ] |
| E3 | `Show detail of order <SO_A>` | Chi tiết đúng số SO | [ ] |
| E4 | `Show KPI summary` | **Thẻ KPI** (không chỉ text success) | [ ] |
| E5 | `Approve order <SO_A>` / `Release order <SO_A>` | **`NOT_AUTHORIZED`** — Employee không release trực tiếp | [ ] |
| E6 | `Request release for order <SO_A>` *hoặc* bấm Confirm Release trên card | Success **`ReleaseRequested`**; order **chưa** released trên SAP | [ ] |
| E7 | `Request release for order <SO_A>` lần 2 | Lỗi: đã có pending request | [ ] |
| E8 | `Show pending approvals` | **`NOT_AUTHORIZED`** (Manager+) | [ ] |
| E9 | `View audit log` | **`NOT_AUTHORIZED`** (Admin) | [ ] |

**Câu nói demo:** *Employee chỉ đề xuất; Manager mới duyệt.*

---

### Act 2 — Manager (checker) · ~5 phút

**Logout Employee → login Manager** (`logout` rồi map lại / máy khác).

| # | Hành động / câu lệnh | Kỳ vọng | Pass |
|---|---|---|---|
| M1 | Login Manager | Role Manager trong DB | [ ] |
| M2 | `Show pending approvals` / `Đơn chờ duyệt` | Thấy `SO_A` (requested by Employee, sales org UE00) | [ ] |
| M3 | `Approve order <SO_A>` | Success **`Approved`** / Released; số SO **không UNKNOWN** | [ ] |
| M4 | Kiểm tra nhanh SAP / `Check status of order <SO_A>` | Order đã release (block gỡ / status đổi) | [ ] |
| M5 | Employee (hoặc Manager) `Request release <SO_B>` trước → Manager: `Reject approval for order <SO_B>` | **`ApprovalRejected`**; SAP **không** release | [ ] |
| M6 | (Tuỳ chọn) Approve order **VKORG khác** scope Manager | `NOT_AUTHORIZED` / message sai sales org | [ ] |

**Câu nói demo:** *Maker-checker: request ở BE, approve mới gọi SAP `releaseOrder`.*

---

### Act 3 — Admin · ~2 phút

**Login Admin.**

| # | Hành động / câu lệnh | Kỳ vọng | Pass |
|---|---|---|---|
| A1 | `View audit log` | Danh sách audit gần đây (Denied / Success / …) | [ ] |
| A2 | `Show pending approvals` | Thấy pending (Admin = all orgs) | [ ] |
| A3 | (Tuỳ chọn) Approve 1 pending ngoài VKORG | Thành công (Admin bypass scope) | [ ] |

---

### Act 4 — Workflow còn lại + lỗi thật · ~3 phút

Dùng **Employee hoặc Manager** tùy ownership.

| # | Hành động | Kỳ vọng | Pass |
|---|---|---|---|
| W1 | `Reject order <SO_C Open>` → chọn reason → Confirm | Success, số SO đúng | [ ] |
| W2 | `Forward order <SO…>` → chọn recipient → Send | Message hiện **SAP User ID** người nhận | [ ] |
| W3 | Reject order đã Delivered (NL hoặc card) | VALIDATION: not allowed after delivery; không còn thẻ SAP_ERROR “material cannot be changed” | [ ] |
| W4 | `logout` | Đăng xuất OK | [ ] |

---

### Act 5 — Kết / Roadmap (30 giây)

Nói: *Phase B = BE enforce roles (demo hôm nay). Phase A tiếp theo = principal propagation / PFCG per-user trên SAP (#157).*

---

## 2. Ma trận quyền (để hỏi đáp)

| Lệnh | Employee | Manager | Admin |
|---|:---:|:---:|:---:|
| Query / KPI | ✅ | ✅ | ✅ |
| `RequestRelease` | ✅ | ✅* | ✅* |
| `ReleaseOrder` (trực tiếp) | ❌ | ✅ | ✅ |
| `ApproveOrder` / `RejectApproval` | ❌ | ✅ (VKORG) | ✅ (all) |
| `GetPendingApprovals` | ❌ | ✅ | ✅ |
| `ViewAuditLog` | ❌ | ❌ | ✅ |
| Reject / Forward (owner) | ✅ | ✅ | ✅ |

\* Manager/Admin cũng có thể request; demo tập trung Employee = maker.

---

## 3. Troubleshooting nhanh khi live

| Triệu chứng | Việc kiểm tra |
|---|---|
| Employee vẫn release được | Migration/role chưa seed → cột `Role` vẫn `Employee`? Deploy đúng bản? |
| `NOT_AUTHORIZED` sai role | `user_mappings.Role` đúng chưa? Logout/login lại |
| Approve: no pending | Employee đã `RequestRelease` chưa? Đúng số SO? Manager `SalesOrg` khớp `order_approvals.SalesOrg` (vd `TV01`) chưa? |
| Approve: sai sales org | Manager `SalesOrg` khớp order (`TV01`/`ND01`/`FG01`) |
| KPI chỉ text success | Bản fix KPI card (#156) đã deploy? |
| Overdue 404 | Known (#154) — **không demo** lệnh này |
| Filter by status | Known (#153) — tránh demo “show Open only” nếu chưa fix |
| SAP auth / dump | Có video backup; báo Phase B BE OK, SAP infra |

---

## 4. Checklist “go / no-go” sáng demo

- [ ] 3 role seed xong, login từng role verify `GetRole` hành vi (E5 deny)
- [ ] `SO_A` pending sẵn **hoặc** rehearse E6 nhanh
- [ ] Manager approve `SO_A` thành công trên SAP
- [ ] AI nhận diện `RequestRelease` / `ApproveOrder` / `GetPendingApprovals`
- [ ] Video backup sẵn trên máy trình chiếu

---

*Cập nhật: Sprint 5 — sau Phase B RBAC + maker-checker (`feature/be-rbac-role-gating`).*
