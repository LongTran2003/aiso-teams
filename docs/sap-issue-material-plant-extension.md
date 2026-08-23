# SAP Test Data Issue: Material-Plant Extension Missing

## Summary
Several materials listed in `ValidMaterialSales` (ZSO_VMS entity) for SalesOrg **1000** / DistrChannel **10** / Division **00** are **not extended to plant 1010**, causing `createSalesOrder` (BAPI_SALESORDER_CREATEFROMDAT2) to fail with:

```
VALIDATION: Material not extended to plant 1010
```

This blocks the AISO Teams chatbot "Create Sales Order" flow from completing end-to-end during testing.

## Affected Materials (test system, per SAP log)
The following materials appear in `ValidMaterialSales` for SalesOrg 1000 / DistrChannel 10 but are missing in `ValidMaterialPlant` for plant `1010`:

| Material   | Description (approx.) | SalesOrg | DistrChannel | Plant issue |
|------------|-----------------------|----------|---------------|------------|
| 000000000000000110 | VF6 (VinFast VF6) | 1000 | 10 | Missing extension for plant 1010 |
| 000000000000000111 | VF8 (VinFast VF8) | 1000 | 10 | Missing extension for plant 1010 |
| 000000000000000113 | VF9 (VinFast VF9) | 1000 | 10 | Missing extension for plant 1010 |

> If the bot is currently coded with default plant `1010` for the test scenario, the same issue likely applies to any other materials that appear in `ValidMaterialSales` but not in `ValidMaterialPlant` for plant 1010.

## Reproduction Steps
1. Bot: "Create sales order" → SalesArea = **1000 / 10 / 00**, Customer = any valid customer
2. Step 3: pick Material `VF6 (000000000000000110)` with qty=1, plant=1010, unit=EA
3. Click **Create order**

## Expected
Order is created successfully (or rejected with a clear, expected business error).

## Actual
SAP returns:
```
VALIDATION - Material not extended to plant 1010
```

## Open Questions for SAP Team
1. Which `SalesOrg` / `DistrChannel` do plants `DL53`, `DL21`, `DL00` belong to? The bot currently supports sales orgs `TV01, FU24, UE00, UW00, DN00, DS00` (see `GetSalesOrdersFunction` enum). Need the correct SalesOrg so the "Create sales order" flow can target the right area.
2. Are VF6 / VF8 / VF9 (`000000000000000110/111/113`) extended to plants `DL53`, `DL21`, or `DL00`? Please confirm:
   - Which `PLANT` exists in `VALID_MATERIAL_PLANT` for each material.
   - Which `SALESORG` + `DISTR_CHANNEL` combination is valid for these materials.

## Suggested SAP Action
In **MM02 → Sales view**, extend each affected material to plant `1010` for SalesOrg `1000` / DistrChannel `10` / Division `00`, OR
in **MM02 → Plant / Storage Location**, create the plant extension for `1010` so the material is enabled for sales-order line items targeting that plant.

Confirm the following:
1. `VALID_MATERIAL_SALES` row exists for each material.
2. `VALID_MATERIAL_PLANT` row exists for each material with `PLANT = '1010'`.
3. MRP / sales views are complete (Sales Org/Distribution Channel/Plant combinations all extended).

## Workaround Applied (Bot Side)
Until SAP data is fixed, the bot has two layered workarounds:

1. **Material filter (Step 3)** – only show materials that have at least one `ValidMaterialPlant` row, so users don't pick materials that will fail the plant check.
2. **Mock switch (now disabled)** – set environment variable `AISO__Sap__UseMock=true` (or `Sap:UseMock` in appsettings) to bypass the real SAP tenant entirely and serve canned mock data. Useful for end-to-end demos while the test system is being fixed. **Currently disabled – we now use the real SAP tenant with valid plants DL53/DL21/DL00.**

`docs/sap-issue-material-plant-extension.md` tracks the underlying SAP issue.

## Labels
- Component: SAP S/4HANA test system
- Type: Data inconsistency / Missing extension
- Priority: Medium (blocks testing)
- Owner: SAP Basis / MM team