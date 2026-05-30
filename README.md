# AISO-Teams

AI-Powered Microsoft Teams Chatbot for SAP Sales Order Task Management.

Capstone Project — Spring 2026, FPT University.

## Team

- **Tran Ngoc Quy Long** (Leader, Backend) — SE173662
- **Mguyen Minh Quan** (SAP Dev) — SE183543
- **Tran Dang Minh Quan** (SAP Dev) — SE180398
- **Vu Ngoc Tien** (AI Engineer) — SE183132
- **Le Thi Thanh Thuy** (Frontend) — SE170111

Supervisors: Mr. Nguyen Ba Le

## Architecture

Modular monolith pattern, deployed to Azure App Service. See `/docs/architecture.md` for details.

**Stack:** ASP.NET Core 8, Azure OpenAI, SAP S/4HANA, Microsoft Teams, Redis, PostgreSQL.

## Folder structure

- `backend/` — .NET Core Bot Service (modular monolith)
- `ai/` — AI orchestration module (Python FastAPI or .NET)
- `frontend/` — Teams app manifest + Adaptive Card templates
- `sap/` — SAP ABAP code (synced via abapGit)
- `pdf-function/` — Azure Function for PDF generation
- `docs/` — Architecture, API contracts, config playbooks
- `infrastructure/` — Docker, CI/CD, deployment configs

## Getting started

See `docs/dev-setup.md` for environment setup instructions per role.

## Branching

- `main` — production, protected
- `develop` — integration, protected
- `feature/{role}-{description}` — feature branches

All work goes through Pull Request review.

## Project status

Currently in Sprint 1 (Foundation phase). See `docs/sprint-plan.md`.