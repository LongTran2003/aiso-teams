# Phase A — BE/FE wire to SAP role actions

> Branch: `feature/phase-a-sap-role-enforce` · Epic [#157](https://github.com/LongTran2003/aiso-teams/issues/157)  
> **Scope of this branch: BE (+ AI schemas / FE cards if needed) only.** SAP ABAP is owned by Quân.

## Split of ownership

| Side | Owner | Work |
|------|-------|------|
| **BE / FE** | Long (+ FE) | Call SAP RAP actions `approveOrder` / `rejectApproval` / `forceRelease` / `forceCancel` / `reassignOwner`; pass `SapUserId` as `REQUESTING_TEAMS_USER` so `get_user_role` works; keep Phase B Postgres gates |
| **SAP** | Quân | Seed `ZAISO_USER_ROLE`; make `approveOrder` / `force*` perform real BAPI (not audit-only); activate/publish |

## What BE already does on this branch

- `ApproveOrder` → `ISapClient.ApproveOrderAsync` → OData `approveOrder` (not `releaseOrder`)
- `RejectApproval` → also calls SAP `rejectApproval` (audit + role gate) then clears Postgres pending
- New functions: `ForceRelease`, `ForceCancel`, `ReassignOwner` + AI JSON schemas
- Unit tests for SapClient action URLs / payloads

## What Quân must deliver (SAP)

1. Seed `ZAISO_USER_ROLE` (`sap_user` = same IDs as `user_mappings.SapUserId`):

| sap_user | role | note |
|----------|------|------|
| DEV-024 | EMPLOYEE | |
| DEV-249 | MANAGER | or Admin demo user as needed |
| (Admin) | ADMIN | for force* |

2. Ensure RAP actions are not audit-only:
   - `approveOrder` / `forceRelease` → real release path (same BAPI as `releaseOrder`, **without** ownership check)
   - `forceCancel` → real cancel/reject path + reason required
   - `rejectApproval` → role gate + audit (no SO reject) is OK

3. Activate/publish behaviour + confirm OData action names match BE:
   - `.../approveOrder`, `rejectApproval`, `forceRelease`, `forceCancel`, `reassignOwner`

## Out of scope (later)

- Principal propagation / per-user Basic Auth
- PFCG `AUTHORITY-CHECK` replacing `ZAISO_USER_ROLE` SELECT
