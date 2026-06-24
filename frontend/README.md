# AISO-Teams Frontend

Microsoft Teams app package and Adaptive Card templates for the AISO-Teams bot.

## Modules

| Component | Purpose |
|---|---|
| `teams-app/manifest.json` | Teams app manifest definition |
| `teams-app/icons/` | App icons (color.png, outline.png) |
| `cards/` | Adaptive Card templates (JSON) used by the backend builder |

## Quick start

To build and package the Teams app for sideloading:

```bash
# 1. Navigate to the teams-app directory
cd frontend/teams-app

# 2. Create the zip package
zip -r ../aiso-bot.zip manifest.json icons/
```

Sideload `aiso-bot.zip` into Teams via "Upload custom app". Use the [Adaptive Cards Designer](https://adaptivecards.io/designer/) to design and test new card templates. Note that the backend `TeamsCardBuilder` will inject real data into these templates.

## CI

Currently, the Teams App package is built manually. Automated packaging and deployment CI will be added in future sprints.

## More docs

- [Frontend Development setup](../docs/foundation/frontend-dev-setup.md) — build, zip, and sideloading Teams app
- [Architecture](../docs/foundation/architecture.md)