# AISO-Teams SAP Module

SAP S/4HANA ABAP development and integration artifacts, synchronized via abapGit.

## Structure

- `abap/cds-views/` — CDS view definitions
- `abap/amdp/` — AMDP procedures
- `abap/rap/` — RAP service definitions and behaviors
- `abap/tables/` — Z-table definitions
- `postman/` — Postman collections for OData testing

## abapGit workflow

1. In SAP GUI, run tcode `ZABAPGIT`
2. Configure online repo: `https://github.com/<org>/aiso-teams.git` with sub-path `/sap/abap/`
3. After activating ABAP code, Push from abapGit
4. Standard Git workflow from there

## More docs

Please refer to the `docs/sap-management/` and `docs/specifications/` folders at the root of the repository for comprehensive documentation and SAP configuration instructions.