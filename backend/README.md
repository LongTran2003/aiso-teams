# AISO-Teams Backend

ASP.NET Core 8 backend for the AISO-Teams bot — an AI-powered Microsoft Teams chatbot
for SAP Sales Order management. Built as a **modular monolith**.

## Modules

| Project | Purpose |
|---|---|
| `AISO.Api` | ASP.NET Core Web API entry point + DI wiring |
| `AISO.Bot` | Microsoft Bot Framework integration + Adaptive Cards |
| `AISO.AiOrchestration` | Function-calling abstractions + dispatcher (Azure OpenAI in Sprint 3) |
| `AISO.SapIntegration` | SAP client (mock now; OData via Cloud Connector in Sprint 3) |
| `AISO.Persistence` | EF Core + PostgreSQL (audit logs, user mappings) |
| `AISO.Domain` | Shared domain models |
| `AISO.Scheduling` | Background jobs (placeholder) |
| `AISO.Reporting` | PDF/export generation (placeholder) |

## Quick start

Full environment setup (toolchain, Docker, IDE, troubleshooting) lives in
[`docs/foundation/backend-dev-setup.md`](../docs/foundation/backend-dev-setup.md). Once your environment is ready:

```bash
# 1. Start PostgreSQL + Redis (compose file lives in infrastructure/docker/)
docker compose -f ../infrastructure/docker/docker-compose.yml up -d   # from backend/, or run inside infrastructure/docker/

# 2. Build and test (from backend/)
dotnet build
dotnet test

# 3. Run the bot
cd src/AISO.Api
dotnet run            # listens on http://localhost:3978
```

Configuration for local dev (Postgres + Redis connection strings) goes in
`src/AISO.Api/appsettings.Development.json` (git-ignored — never commit secrets).
Test the bot with the [Bot Framework Emulator](https://github.com/microsoft/BotFramework-Emulator/releases/latest)
against `http://localhost:3978/api/messages`.

## CI

Every push/PR to `develop`/`main` runs the **Backend CI** workflow
([`.github/workflows/backend-ci.yml`](../.github/workflows/backend-ci.yml)):
build (Release) → `dotnet format --verify-no-changes` → `dotnet test`. Run the same checks
locally before pushing:

```bash
dotnet build --configuration Release
dotnet format --verify-no-changes --severity warn
dotnet test --configuration Release --no-build
```

## More docs

- [Development setup](../docs/foundation/backend-dev-setup.md) — environment, Docker, run & debug, troubleshooting
- [Architecture](../docs/foundation/architecture.md)
- [Git workflow](../docs/foundation/git-workflow.md)
- [Sprint plan](../docs/planning/sprint-plan.md)
 
