# infrastructure/

Deployment and infrastructure-as-code assets for AISO-Teams.

- `docker/` — local dev services. `docker-compose.yml` runs PostgreSQL 16 + Redis 7:
  ```bash
  docker compose -f infrastructure/docker/docker-compose.yml up -d   # from repo root
  ```
- `ci/` — reserved for CI/CD pipeline config (the active GitHub Actions workflows live in `.github/workflows/`).

Deployment configs (Azure App Service, etc.) will be added as that work begins (Sprint 4+).
