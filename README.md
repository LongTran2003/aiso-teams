# AISO-Teams

AI-Powered Microsoft Teams Chatbot for SAP Sales Order Task Management.

Capstone Project — Spring 2026, FPT University.

## Team

- **Tran Ngoc Quy Long** (Leader, Backend) — SE173662
- **Nguyen Minh Quan** (SAP Dev) — SE183543
- **Tran Dang Minh Quan** (SAP Dev) — SE180398
- **Vu Ngoc Tien** (Frontend) — SE183132
- **Le Thi Thanh Thuy** (AI Engineer) — SE170111

Supervisors: Mr. Nguyen Ba Le

## Architecture

Modular monolith pattern, deployed to Azure App Service. See [`docs/foundation/architecture.md`](docs/foundation/architecture.md) for details.

**Stack:** ASP.NET Core 8, Azure OpenAI, SAP S/4HANA, Microsoft Teams, Redis, PostgreSQL.

## Folder structure

- `backend/` — .NET Core Bot Service (modular monolith)
- `ai/` — AI orchestration module (Python FastAPI)
- `frontend/` — Teams app manifest + Adaptive Card templates
- `sap/` — SAP ABAP code (synced via abapGit)
- `pdf-function/` — Azure Function for PDF generation
- `docs/` — Architecture, API contracts, config playbooks
- `infrastructure/` — Docker, CI/CD, deployment configs

## Getting started

Backend development setup (toolchain, Docker services, run & debug) is documented in
[`backend/README.md`](backend/README.md). Other module guides live under their respective
folders and in [`docs/`](docs/).

## Branching

- `main` — production, protected
- `develop` — integration, protected
- `feature/{role}-{description}` — feature branches

All work goes through Pull Request review.

## Project status

Sprint 2 complete — backend vertical slice (Teams bot → mock SAP → Adaptive Card),
observability (audit logging, health checks, Serilog), and CI (GitHub Actions) are merged.
See [`docs/planning/sprint-plan.md`](docs/planning/sprint-plan.md).