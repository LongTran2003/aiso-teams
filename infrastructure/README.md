# AISO-Teams Infrastructure

Deployment and infrastructure-as-code assets for the AISO-Teams project.

## Structure

- `docker/` — Local dev services orchestration.
- `ci/` — Reserved for CI/CD pipeline configuration (active GitHub Actions workflows live in `.github/workflows/`).

## Quick start

To start the local database and cache (PostgreSQL 16 + Redis 7), run the following command from the repository root:

```bash
docker compose -f infrastructure/docker/docker-compose.yml up -d
```

Deployment configs (Azure App Service, etc.) will be added as that work begins (Sprint 4+).

## More docs

Please refer to the `docs/foundation/dev-setup.md` for full environment setup, and the `docs/` folder for comprehensive documentation.
