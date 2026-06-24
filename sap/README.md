# AISO-Teams SAP Module

SAP S/4HANA ABAP development and integration artifacts for the AISO-Teams bot, synchronized via abapGit.

## Modules

| Folder / Resource | Purpose |
|---|---|
| `abap/cds-views/` | CDS view definitions for data modeling |
| `abap/amdp/` | AMDP procedures for complex data logic |
| `abap/rap/` | RAP service definitions and behaviors for OData endpoints |
| `abap/tables/` | Z-table definitions (custom tables) |
| `postman/` | Postman collections for OData API testing |

## Quick start

To sync and activate the ABAP code using abapGit:

1. In SAP GUI, run tcode `ZABAPGIT`
2. Configure online repo: `https://github.com/<org>/aiso-teams.git` with sub-path `/sap/abap/`
3. Pull the latest code and activate the ABAP objects.
4. After making changes and activating ABAP code, Push from abapGit.
5. Follow the standard Git workflow from there.

## CI

SAP ABAP code synchronization is primarily handled via abapGit rather than GitHub Actions. CI pipelines for SAP artifacts will be added if required in future sprints.

## More docs

- [SAP Setup Guide](../docs/sap-management/sap-dev-setup.md) — Connection instructions and system setup
- [Technical Specification](../docs/specifications/) — Integration specs and OData contract