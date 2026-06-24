# AISO-Teams Infrastructure

Deployment and infrastructure-as-code assets for the AISO-Teams project.

## Modules

| Folder | Purpose |
|---|---|
| `docker/` | Local dev services orchestration (PostgreSQL, Redis) |
| `ci/` | Reserved for CI/CD pipeline configuration |

## Quick start

To start the local database and cache (PostgreSQL 16 + Redis 7), run the following command from the repository root:

```bash
# Start Docker compose services
docker compose -f infrastructure/docker/docker-compose.yml up -d
```

Deployment configs (Azure App Service, etc.) will be added as that work begins (Sprint 4+). Note that active GitHub Actions workflows live in the root `.github/workflows/` directory.

## CI

The infrastructure folder provides the foundational services required for backend CI runs. Specific IaC deployments will have their own GitHub Actions added later.

## More docs

- [Development setup](../docs/foundation/dev-setup.md) — environment, Docker, run & debug, troubleshooting
- [Architecture](../docs/foundation/architecture.md)
