# Backend (.NET Core Bot Service)

Modular monolith ASP.NET Core application. See `/docs/architecture.md` for design.

## Quick start

```bash
cd backend
docker-compose up -d         # Start PostgreSQL + Redis
cd src/AISO.Api
dotnet run
```

Bot listens on `http://localhost:5000`.

## Solution structure

- `AISO.Api` — Web API entry point
- `AISO.Bot` — Bot Framework integration
- `AISO.AiOrchestration` — LLM function calling
- `AISO.SapIntegration` — SAP OData client
- `AISO.Scheduling` — Hangfire jobs
- `AISO.Reporting` — PDF generation
- `AISO.Persistence` — Database access
- `AISO.Domain` — Domain models