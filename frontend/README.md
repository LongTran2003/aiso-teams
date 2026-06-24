# AISO-Teams Frontend

Microsoft Teams app package and Adaptive Card templates for the AISO-Teams bot.

## Structure

- `teams-app/manifest.json` — Teams app manifest
- `teams-app/icons/` — App icons (color.png, outline.png)
- `cards/` — Adaptive Card templates (JSON) used by the backend builder

## Build app package

```bash
cd frontend/teams-app
zip -r ../aiso-bot.zip manifest.json icons/
```

Sideload `aiso-bot.zip` into Teams via "Upload custom app".

## Card templates

Use [Adaptive Cards Designer](https://adaptivecards.io/designer/) to design and test. Note that the backend `TeamsCardBuilder` will inject real data into these templates before sending them to Teams.

## More docs

Please refer to the `docs/` folder at the root of the repository for comprehensive documentation.