# Frontend (Teams App)

Microsoft Teams app with Adaptive Card templates.

## Structure

- `teams-app/manifest.json` — Teams app manifest
- `teams-app/icons/` — App icons (color.png, outline.png)
- `cards/` — Adaptive Card templates (JSON)

## Build app package

```bash
cd frontend/teams-app
zip -r ../aiso-bot.zip manifest.json icons/
```

Sideload `aiso-bot.zip` into Teams via "Upload custom app".

## Card templates

Use [Adaptive Cards Designer](https://adaptivecards.io/designer/) to design and test.