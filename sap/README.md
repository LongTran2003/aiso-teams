# SAP Code

ABAP development synchronized via abapGit. See `/docs/sap-setup.md` for connection instructions.

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