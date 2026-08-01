# E2E test scenarios — MS Teams

End-to-end checklist for AISO-Teams (bot → BE → AI → SAP).
Use for manual Teams testing and demo rehearsal.

## Language policy

| Layer | Rule |
|---|---|
| **Bot replies / Adaptive Cards** | **English only** (Teams client translate if needed) |
| **User input** | English **or** Vietnamese natural language is OK |
| **Canonical smoke** | Prefer Help card English samples (stable for demo) |
| **Avoid** | Mixing EN+VI in one message (e.g. `show orders open của TV01`) |

> Lifecycle writes (release / reject / forward / approve) still show a **confirm card** before SAP.

---

## 0. Prep

| # | Task | Notes |
|---|---|---|
| 0.1 | BE + Bot deployed (RBAC + maker-checker + Help/error UX) | `develop` |
| 0.2 | Postgres migration + role seed | Employee / Manager+SalesOrg / Admin |
| 0.3 | AI hot-load `ai/functions/*.json` (salesOrg includes `TV01`/`FU24`) | |
| 0.4 | SAP RAP errors return business messages (not dump) | |
| 0.5 | ≥ 3 **Open** SOs owned by Employee | One SO per mutating command |
| 0.6 | Know SAP User ID + role per tester | |

**Timed demo:** [`demo-test-script.md`](./demo-test-script.md).

**Order status reminders:**
- Release / Approve need Open (or blocked) orders.
- Employee cannot release directly → `request release`; Manager `approve order`.
- Reject Open = happy path; Reject Delivered / Partially Delivered = **VALIDATION** (button hidden; NL blocked before SAP).
- List / recent orders exclude SOs with `HasInvalidMaterial = X` by default. View-by-id still works and shows a warning banner.
- Stable demo SO (valid materials): `0000012912`.
- Use a **fresh SO** per mutating step.

---

## 1. Login / logout / cancel

| # | Input (EN or VI) | Expected bot UI (EN) |
|---|---|---|
| 1.1 | `hi` (unlinked) | Login / link SAP User ID flow |
| 1.2 | Valid SAP ID (e.g. `DEV-249`) | Welcome + linked message in EN |
| 1.3 | `help` / `hướng dẫn` / `trợ giúp` | Help card: role flow steps + 3 shortcuts + short EN samples (no AI function dump) |
| 1.4 | `logout` / `đăng xuất` | Text: `Signed out of your SAP account. Type hi to sign in again.` |
| 1.5 | Business command while unlinked | Login required (not crash) |
| 1.6 | `cancel` / `thoát` mid-flow | Text: `Cancelled the current flow. You can start again.` |

---

## 2. Sales order query

| # | Input examples | Expected |
|---|---|---|
| 2.1 | `recent orders` · `xem danh sách đơn hàng` · `show all open orders` | SO list card titled **Sales orders** (no owner filter) |
| 2.1b | `show my sales orders` · `đơn hàng của tôi` · `xem danh sách đơn hàng của tôi` | List titled **My sales orders**; only SOs where `OwnerSapUser` = linked SAP user |
| 2.2 | `show orders of customer 1000` | Filtered list or empty card |
| 2.3 | `show order 13122` · `xem chi tiết đơn 9` | Detail card (customer, value, status, items) |
| 2.4 | `check order 5001` | Detail / status path |
| 2.5 | Unknown SO (`show order 9999999999`) | **Not found** error card (not plain text crash) |
| 2.6 | `show open orders of TV01` · `show open orders of FU24` | List filtered by org/status (or empty) |

---

## 3. KPI / overdue

| # | Input examples | Expected |
|---|---|---|
| 3.1 | `show revenue kpi` · `show kpi summary` · `tổng quan KPI` | KPI summary / revenue card |
| 3.2 | `kpi by customer 1000` · `KPI theo khách 1000` | KPI by customer card (or empty) |
| 3.3 | `kpi by product MAT-01` · `KPI theo sản phẩm …` | KPI by product card (or empty) |
| 3.4 | `show overdue orders` · `đơn hàng quá hạn` | Overdue list card (+ View details) or empty |

---

## 4. Maker-checker release

| # | Role | Input | Expected |
|---|---|---|---|
| 4.1 | Employee | `release order <SO>` | **Not authorized** card |
| 4.2 | Employee | `request release 13122` / confirm on card | Success **Release requested** |
| 4.3 | Employee | `show pending approvals` | Not authorized |
| 4.4 | Manager | `show pending approvals` | Pending list (correct VKORG) |
| 4.5 | Manager | `approve order <SO>` | Approved & released; SO number correct |
| 4.6 | Manager | `reject approval <SO>` | Approval rejected; SAP not released |
| 4.7 | Manager | Approve SO **other SalesOrg** | Denied / validation |
| 4.8 | Manager/Admin | `release order <SO>` direct | Released; SO number correct |
| 4.9 | Admin | `view audit log` | Audit card |
| 4.10 | — | Not owner | Validation: owned by another user |

---

## 5. Reject order

| # | Input | Expected |
|---|---|---|
| 5.1 | Reject Open → reason → Confirm | Success **Order rejected** / status **Cancelled**; SO number correct |
| 5.2 | Reject Delivered / Partially Delivered | **VALIDATION** card (delivery started); Reject button hidden on detail |
| 5.3 | Confirm without reason | Reason required |
| 5.4 | Confirm → Cancel | No SAP reject |

---

## 6. Forward order

| # | Input | Expected |
|---|---|---|
| 6.1 | Forward → recipient → Send | Success forwarded; recipient = SAP User ID |
| 6.2 | Display name on card | SAP User ID (e.g. `DEV-249`), not Teams display confusion |
| 6.3 | Cancel confirm | No forward |

---

## 7. Errors & edge cases

| # | Case | Expected |
|---|---|---|
| 7.1 | Vague / out of scope | Polite handling; no crash |
| 7.2 | SAP business/system error | Error card + Error code + Details (SAP text) + Show help |
| 7.3 | Rapid successive commands | No stuck dialog |
| 7.4 | Bad order id format | Reasonable validation / not found |

---

## 8. Bilingual NL regression (Help ↔ tester)

Smoke **canonical EN** first (from Help). Then spot-check **VI** input still routes to the same intent. Bot UI stays EN.

| Intent | Canonical EN (Help) | VI input (should still work) | Pass? |
|---|---|---|---|
| Help | `help` | `trợ giúp` (if mapped) / `help` | [ ] |
| List | `recent orders` | `xem đơn hàng gần đây` | [ ] |
| Open + org | `show open orders of TV01` | `hiển thị đơn hàng mở của TV01` | [ ] |
| Detail | `show order 13122` | `xem đơn 13122` | [ ] |
| Overdue | `show overdue orders` | `xem đơn hàng quá hạn` | [ ] |
| KPI summary | `show revenue kpi` | `xem KPI doanh thu` | [ ] |
| KPI customer | `kpi by customer 1000` | `KPI theo khách 1000` | [ ] |
| KPI product | `kpi by product MAT-01` | `KPI theo sản phẩm MAT-01` | [ ] |
| Request release | `request release 13122` | `xin duyệt đơn 13122` | [ ] |
| Reject | `reject order 13122` | `hủy đơn 13122` | [ ] |
| Pending | `show pending approvals` | `xem đơn chờ duyệt` | [ ] |
| Logout | `logout` | `đăng xuất` | [ ] |
| Cancel flow | `cancel` | `thoát` | [ ] |

**Anti-case (expect weak/empty/fail — document, do not use in demo):**
- `show orders open của TV01` (mixed EN+VI)

---

## 9. Tester findings smoke

| Tester item | Smoke command / action | Pass? |
|---|---|---|
| Unknown / invalid SO | `show order 9999999999` | [ ] |
| KPI by customer | `kpi by customer 1000` | [ ] |
| KPI by product | `kpi by product MAT-01` | [ ] |
| Overdue SOs | `show overdue orders` | [ ] |
| Reject Open after confirm | Detail → Reject → reason → Confirm | [ ] |
| Reject Delivered blocked | Delivered SO: no Reject button / VALIDATION on NL | [ ] |
| Cancel / logout / unauth | `cancel`, `logout`, command while logged out | [ ] |
| Bilingual NL regression | Section 8 matrix | [ ] |

---

## 10. Short demo checklist

- [ ] Seed 3 roles
- [ ] Employee: `help` + `recent orders` + KPI card
- [ ] Employee: direct release → Not authorized
- [ ] Employee: request release → pending
- [ ] Manager: pending → approve → SAP released, SO correct
- [ ] Manager: reject approval on another SO
- [ ] Admin: audit log
- [ ] Reject Open + Forward (SO / recipient correct)
- [ ] Optional: 1 real SAP error (delivered reject)
- [ ] Logout (EN ack)

---

*Updated: Help EN samples, error-card EN UX, reject SAP propagation, KPI by customer/product cards, salesOrg TV01/FU24.*
