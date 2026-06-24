# AISO-Teams Frontend — Development Setup

This document explains how to set up, build, and sideload the Microsoft Teams app for AISO-Teams.

## Prerequisites

- Microsoft Teams account with permissions to "Upload custom apps" (Sideloading).
- Zip utility (built-in on Windows/Mac, or install via terminal).

## 1. Understanding the App Package

The Teams App package is simply a ZIP file containing:
1. `manifest.json`: Configuration for the bot, containing the Azure Bot ID.
2. `color.png` & `outline.png`: Icons used in the Teams interface.

## 2. Building the Package

To create the package that you will upload to Teams:

```bash
# Navigate to the frontend directory
cd frontend/teams-app

# Zip the manifest and icons into a package
# On Windows (PowerShell):
Compress-Archive -Path manifest.json, icons -DestinationPath ../aiso-bot.zip -Force

# On macOS/Linux:
zip -r ../aiso-bot.zip manifest.json icons/
```

## 3. Sideloading into Microsoft Teams

1. Open Microsoft Teams (Desktop or Web).
2. Go to the **Apps** section.
3. Click on **Manage your apps** at the bottom.
4. Click **Upload an app** -> **Upload a custom app**.
5. Select the `aiso-bot.zip` file you just generated.
6. Click **Add** to install the bot for yourself.

## 4. Testing Adaptive Cards

When designing new Adaptive Cards for the project:
1. Open the [Adaptive Cards Designer](https://adaptivecards.io/designer/).
2. Set the target version to **1.5**.
3. Create your layout and test your data bindings.
4. Save the JSON file into the `frontend/cards/` directory.

The backend `TeamsCardBuilder` will load these files at runtime and populate them with live SAP data.
