# AISO-Teams Backend

ASP.NET Core 8 backend for the AISO-Teams bot — an AI-powered Microsoft Teams chatbot for SAP Sales Order management.

This document explains how to set up your local development environment and run the bot.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [1. Repository Setup](#1-repository-setup)
- [2. Host OS Environment](#2-host-os-environment)
  - [Windows (recommended path)](#windows-recommended-path)
  - [macOS](#macos)
  - [Linux](#linux)
- [3. Docker Environment (for PostgreSQL + Redis)](#3-docker-environment-for-postgresql--redis)
  - [Windows: WSL Ubuntu](#windows-wsl-ubuntu)
  - [macOS: Docker Desktop or OrbStack](#macos-docker-desktop-or-orbstack)
  - [Linux: Native Docker](#linux-native-docker)
- [4. Solution Build](#4-solution-build)
- [5. Database Setup](#5-database-setup)
- [6. Bot Configuration](#6-bot-configuration)
- [7. Run and Test the Bot Locally](#7-run-and-test-the-bot-locally)
- [Troubleshooting](#troubleshooting)
- [Daily Workflow](#daily-workflow)
- [Useful Commands Reference](#useful-commands-reference)
- [What's Next (Coming Soon)](#whats-next-coming-soon)

---

## Overview

The backend is a **modular monolith** in .NET 8 with 8 projects:

| Project | Purpose |
|---|---|
| `AISO.Api` | ASP.NET Core Web API entry point |
| `AISO.Bot` | Microsoft Bot Framework integration |
| `AISO.AiOrchestration` | Azure OpenAI function calling |
| `AISO.SapIntegration` | SAP OData client |
| `AISO.Scheduling` | Hangfire background jobs |
| `AISO.Reporting` | QuestPDF PDF generation |
| `AISO.Persistence` | EF Core + PostgreSQL |
| `AISO.Domain` | Shared domain models |

### Recommended development setup

- **Bot service**: runs on your host OS (Windows, macOS, or Linux) using `dotnet run`
- **PostgreSQL + Redis**: run in Docker containers
- **Reasoning**: Bot Framework Emulator + .NET tooling work best on host OS; Docker handles infrastructure

This setup is what the team leader used during initial scaffolding. See [Troubleshooting](#troubleshooting) for issues encountered along the way.

---

## Prerequisites

### Hardware
- **RAM**: 8 GB minimum, 16 GB recommended
- **Disk**: 20 GB free space
- **Architecture**: x64 (Intel/AMD) or ARM64 (Apple Silicon)

### Accounts you need
- **GitHub account** with SSH key set up — see GitHub's [SSH setup guide](https://docs.github.com/en/authentication/connecting-to-github-with-ssh)
- **Microsoft account** (any Outlook/Hotmail/personal Microsoft email)
- **Azure account** — free tier or Azure for Students
- Repository access — ask the team leader (`LongTran2003`)

### Software you'll install
- **Git** (latest)
- **.NET 8 SDK**
- **Docker** (Engine + Compose v2)
- **VS Code** or **Visual Studio 2022** (Community Edition is fine)
- **Bot Framework Emulator** (for local bot testing — installed later)
- **WSL2** with Ubuntu (Windows users only)

---

## 1. Repository Setup

### Set up SSH key with GitHub

GitHub no longer accepts password authentication. You must use SSH or a Personal Access Token. SSH is recommended.

```bash
# Generate SSH key (skip if you already have one)
ssh-keygen -t ed25519 -C "your-email@example.com"

# Add public key to GitHub
# 1. Copy the public key:
cat ~/.ssh/id_ed25519.pub

# 2. Go to GitHub: Settings → SSH and GPG keys → New SSH key
# 3. Paste, save

# Test connection
ssh -T git@github.com
# Expected: "Hi <username>! You've successfully authenticated..."
```

### Clone the repository

```bash
# Clone using SSH
git clone git@github.com:LongTran2003/aiso-teams.git
cd aiso-teams

# Check out develop branch (this is the default integration branch)
git checkout develop
git pull
```

The `develop` branch is where all feature branches merge. Never commit directly to `main` or `develop` — always create a feature branch and open a Pull Request.

---

## 2. Host OS Environment

Choose your platform below.

### Windows (recommended path)

This is the setup used by the team leader during initial scaffolding.

#### Install .NET 8 SDK

Download from [Microsoft's .NET download page](https://dotnet.microsoft.com/download/dotnet/8.0).

Choose **SDK x64** (or ARM64 for ARM machines).

Verify installation:

```powershell
dotnet --version
# Expected: 8.0.xxx
```

#### Install Visual Studio 2022 (recommended) or VS Code

**Visual Studio 2022 Community** (free):
- Download from [visualstudio.microsoft.com](https://visualstudio.microsoft.com/)
- Workloads to install:
  - **ASP.NET and web development**
  - **.NET desktop development**
  - **Data storage and processing**

**VS Code** (lightweight alternative):
- Download from [code.visualstudio.com](https://code.visualstudio.com/)
- Required extensions:
  - C# Dev Kit (by Microsoft)
  - C# (by Microsoft)
  - .NET Install Tool (by Microsoft)
  - Docker (by Microsoft)

#### Install Git for Windows

Download from [git-scm.com](https://git-scm.com/download/win).

Default settings are fine. Make sure "Git Bash" is included — useful for shell commands later.

### macOS

#### Install .NET 8 SDK

```bash
# Option 1: Download installer from Microsoft
# https://dotnet.microsoft.com/download/dotnet/8.0

# Option 2: Using Homebrew
brew install --cask dotnet-sdk

# Verify
dotnet --version
# Expected: 8.0.xxx
```

#### Install IDE

**VS Code** (recommended for Mac):

```bash
brew install --cask visual-studio-code
```

Then install C# Dev Kit extension from the marketplace.

**Alternative**: Visual Studio for Mac is discontinued — use VS Code or JetBrains Rider.

#### Install Git

Usually pre-installed on macOS. Verify:

```bash
git --version
```

If not installed:

```bash
brew install git
```

### Linux

#### Install .NET 8 SDK

**Ubuntu 22.04 / 24.04 LTS** (recommended):

```bash
# Add Microsoft package signing key
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install SDK
sudo apt update
sudo apt install -y dotnet-sdk-8.0

# Verify
dotnet --version
```

**Ubuntu 26.04 (and other recent non-LTS versions)**: apt repository may not have .NET 8 yet. Use the install script:

```bash
# Install dependencies
sudo apt install -y libicu-dev libssl-dev

# Use the install script
curl -L https://aka.ms/install-dotnet.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Add to PATH (add to ~/.bashrc or ~/.zshrc)
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

# Verify
dotnet --version
```

#### Install VS Code

```bash
sudo snap install --classic code
```

Or download .deb from [code.visualstudio.com](https://code.visualstudio.com/).

---

## 3. Docker Environment (for PostgreSQL + Redis)

The bot needs PostgreSQL (data persistence) and Redis (cache) running locally. We use Docker Compose to manage them.

### Windows: WSL Ubuntu

**Why WSL instead of Docker Desktop?**
- Lighter resource footprint (no VM overhead)
- Faster file system performance
- Better for development; Docker Desktop is heavy

#### Install WSL2

Open **PowerShell as Administrator**:

```powershell
wsl --install
# This installs WSL2 with default Ubuntu (latest LTS)

# Restart Windows
```

After restart, Ubuntu installation continues automatically. Set up your Linux username and password when prompted.

#### Verify Ubuntu version

```bash
# Inside WSL Ubuntu
lsb_release -a
```

**Recommendation**: Use **Ubuntu 24.04 LTS** (the default LTS as of 2026). It has the best stability.

**Warning**: If WSL installs Ubuntu 26.04 or newer non-LTS, you may hit issues with package availability. Either:
- Install Ubuntu 24.04 explicitly: `wsl --install -d Ubuntu-24.04`
- Or proceed with newer Ubuntu and use workarounds (see [Troubleshooting](#troubleshooting))

#### Enable systemd in WSL

systemd is required for Docker to work as a service.

```bash
sudo nano /etc/wsl.conf
```

Add this content:

```ini
[boot]
systemd=true
```

Save (Ctrl+O, Enter, Ctrl+X). Then from PowerShell:

```powershell
wsl --shutdown
# Restart WSL by opening Ubuntu again
```

Verify systemd is running:

```bash
ps -p 1
# Expected: 1 ? 00:00:00 systemd
```

#### Install Docker Engine (NOT Docker Desktop)

```bash
# Remove any old docker packages
sudo apt remove -y docker docker-engine docker.io containerd runc

# Install dependencies
sudo apt update
sudo apt install -y ca-certificates curl gnupg

# Add Docker's GPG key
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Add repository
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Add your user to docker group (so you don't need sudo for every docker command)
sudo usermod -aG docker $USER

# Apply group change (or just log out and back in)
newgrp docker

# Verify
docker --version
docker compose version
```

#### (Optional) Install lazydocker

Terminal UI for Docker — much nicer than typing `docker ps` repeatedly.

```bash
curl https://raw.githubusercontent.com/jesseduffield/lazydocker/master/scripts/install_update_linux.sh | bash
```

Run with:

```bash
lazydocker
```

### macOS: Docker Desktop or OrbStack

**OrbStack** is the recommended Docker runtime for macOS — faster and lighter than Docker Desktop.

```bash
brew install --cask orbstack
```

Launch OrbStack. It will automatically install Docker Engine and Docker Compose.

**Alternative**: Docker Desktop

```bash
brew install --cask docker
# Launch Docker Desktop from Applications folder
```

Verify:

```bash
docker --version
docker compose version
```

### Linux: Native Docker

Linux can run Docker natively without WSL or VMs.

```bash
# Ubuntu / Debian (same as in WSL Ubuntu section)
sudo apt update
sudo apt install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
sudo usermod -aG docker $USER
newgrp docker
```

For Fedora / Arch / other distros — refer to [official Docker docs](https://docs.docker.com/engine/install/).

---

## 4. Solution Build

Navigate to the backend folder and build.

### From your host OS terminal

```bash
cd backend
dotnet restore
dotnet build
```

Expected output:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If you see errors, jump to [Troubleshooting](#troubleshooting).

### Project structure after build

```
backend/
├── src/
│   ├── AISO.Api/
│   ├── AISO.Bot/
│   ├── AISO.AiOrchestration/
│   ├── AISO.SapIntegration/
│   ├── AISO.Scheduling/
│   ├── AISO.Reporting/
│   ├── AISO.Persistence/
│   └── AISO.Domain/
├── tests/
│   └── (test projects)
├── docker-compose.yml
└── AISO.sln
```

---

## 5. Database Setup

### Start PostgreSQL and Redis containers

The `docker-compose.yml` file in `backend/` defines two services: `postgres` and `redis`.

```bash
# From backend/ folder
cd backend
docker compose up -d
```

Expected output:

```
[+] Running 2/2
 ✔ Container aiso-postgres  Started
 ✔ Container aiso-redis     Started
```

### Verify containers

```bash
docker compose ps
```

Expected:

```
NAME             IMAGE                STATUS              PORTS
aiso-postgres    postgres:16          Up (healthy)        0.0.0.0:5432->5432/tcp
aiso-redis       redis:7-alpine       Up                  0.0.0.0:6379->6379/tcp
```

### Default credentials (development only)

The `docker-compose.yml` uses these dev credentials:

| Service | Host | Port | User | Password | Database |
|---|---|---|---|---|---|
| PostgreSQL | localhost | 5432 | `aiso_user` | `aiso_dev_password` | `aiso_db` |
| Redis | localhost | 6379 | (none) | (none) | (none) |

**⚠️ These are development-only credentials.** Production uses Azure Key Vault — never commit real credentials.

### Test the connection

```bash
# Test PostgreSQL
docker exec -it aiso-postgres psql -U aiso_user -d aiso_db -c "SELECT version();"
# Should print PostgreSQL version

# Test Redis
docker exec -it aiso-redis redis-cli ping
# Should print: PONG
```

### Run EF Core migrations

Once `AISO.Persistence` has migrations defined (after Sprint 2):

```bash
cd backend
dotnet ef database update --project src/AISO.Persistence --startup-project src/AISO.Api
```

For now (Sprint 1), no migrations exist yet. Skip this step.

### Stopping containers

```bash
# Stop containers (data persists)
docker compose stop

# Stop AND remove containers (data persists in volumes)
docker compose down

# Stop, remove containers, AND delete data (full reset)
docker compose down -v
```

---

## 6. Bot Configuration

### Configuration files

Configuration is split across:

- `appsettings.json` — defaults, safe to commit
- `appsettings.Development.json` — dev overrides, **do not commit secrets here**
- User Secrets — for sensitive values (Azure AD secrets, etc.)
- Environment variables — for CI/CD and production

### Azure AD credentials

You'll need:
- **MicrosoftAppId** — bot's Azure AD App Registration ID
- **MicrosoftAppPassword** — client secret for the App Registration
- **MicrosoftAppTenantId** — tenant ID
- **MicrosoftAppType** — `SingleTenant` or `MultiTenant`

**Ask the team leader** for the development App Registration values. They are kept in a shared secure location (NOT in the repo).

### Set secrets locally using User Secrets

User Secrets is the recommended way to store dev credentials. Values are kept outside the repo on your machine.

```bash
cd backend/src/AISO.Api

# Initialize user secrets (once)
dotnet user-secrets init

# Set values
dotnet user-secrets set "MicrosoftAppId" "your-app-id-from-team-leader"
dotnet user-secrets set "MicrosoftAppPassword" "your-app-password-from-team-leader"
dotnet user-secrets set "MicrosoftAppTenantId" "your-tenant-id-from-team-leader"
dotnet user-secrets set "MicrosoftAppType" "SingleTenant"

# Connection strings
dotnet user-secrets set "ConnectionStrings:PostgresConnection" "Host=localhost;Port=5432;Database=aiso_db;Username=aiso_user;Password=aiso_dev_password"
dotnet user-secrets set "ConnectionStrings:RedisConnection" "localhost:6379"
```

### Note for Bot Framework Emulator testing

For initial Emulator testing **only**, you can leave Azure AD credentials empty:

```bash
dotnet user-secrets set "MicrosoftAppId" ""
dotnet user-secrets set "MicrosoftAppPassword" ""
```

Emulator can connect to bots without Azure AD. **You'll need real credentials before testing in actual Microsoft Teams.**

---

## 7. Run and Test the Bot Locally

### Run the bot

```bash
cd backend/src/AISO.Api
dotnet run
```

Expected output:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:3978
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

The bot's messaging endpoint is `http://localhost:3978/api/messages`.

### Install Bot Framework Emulator

Download from [GitHub Releases](https://github.com/microsoft/BotFramework-Emulator/releases/latest).

Install per your OS instructions.

### Test with Bot Framework Emulator

1. Open Bot Framework Emulator
2. Click **Open Bot**
3. Enter:
   - **Bot URL**: `http://localhost:3978/api/messages`
   - **Microsoft App ID**: leave empty (or use value from User Secrets if set)
   - **Microsoft App Password**: leave empty (or use value from User Secrets if set)
4. Click **Connect**

You should see the bot's welcome message. Type any message — the bot will echo it back (current Sprint 1 behavior).

### Successful test checklist

- ✅ Bot starts without errors
- ✅ Emulator connects to `http://localhost:3978/api/messages`
- ✅ Bot echoes messages
- ✅ PostgreSQL container running
- ✅ Redis container running

If all checked, your local setup is complete.

---

## Troubleshooting

Issues encountered during initial setup, with fixes. Update this section as new issues arise.

### Issue 1: Ubuntu 26.04 — no .NET 8 in apt

**Symptom**:
```
$ sudo apt install dotnet-sdk-8.0
E: Unable to locate package dotnet-sdk-8.0
```

**Cause**: Ubuntu 26.04 is too new; Microsoft's apt repository hasn't published packages for it.

**Fix**: Use the dotnet-install.sh script. Also install `libicu-dev` first.

```bash
sudo apt install -y libicu-dev libssl-dev
curl -L https://aka.ms/install-dotnet.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc
echo 'export DOTNET_ROOT="$HOME/.dotnet"' >> ~/.bashrc
source ~/.bashrc
```

**Better fix**: Use Ubuntu 24.04 LTS instead. Avoid non-LTS versions for development.

---

### Issue 2: Build error — `Microsoft.AspNetCore.Mvc.NewtonsoftJson` not found

**Symptom**:
```
error CS0246: The type or namespace name 'AddNewtonsoftJson' could not be found
```

**Cause**: Bot Framework SDK uses Newtonsoft.Json for serialization. The package needed isn't auto-included.

**Fix**:

```bash
cd backend/src/AISO.Api
dotnet add package Microsoft.AspNetCore.Mvc.NewtonsoftJson --version 8.0.0
```

---

### Issue 3: Build error — `CloudAdapter` constructor ambiguity

**Symptom**:
```
error CS0121: The call is ambiguous between the following methods or properties:
'CloudAdapter.CloudAdapter(BotFrameworkAuthentication, ILogger<IBotFrameworkHttpAdapter>)'
'CloudAdapter.CloudAdapter(BotFrameworkAuthentication, ILogger)'
```

**Cause**: The latest `Microsoft.Bot.Builder.Integration.AspNet.Core` package has overloaded constructors that the DI container cannot disambiguate.

**Fix**: Create a custom `AdapterWithErrorHandler` class that inherits from `CloudAdapter` and is unambiguous.

Create `backend/src/AISO.Bot/AdapterWithErrorHandler.cs`:

```csharp
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

namespace AISO.Bot;

public class AdapterWithErrorHandler : CloudAdapter
{
    public AdapterWithErrorHandler(
        BotFrameworkAuthentication auth,
        ILogger<IBotFrameworkHttpAdapter> logger)
        : base(auth, logger)
    {
        OnTurnError = async (turnContext, exception) =>
        {
            logger.LogError(exception, "Unhandled error: {Message}", exception.Message);
            await turnContext.SendActivityAsync("Sorry, something went wrong. Please try again.");
        };
    }
}
```

Then register in `Program.cs`:

```csharp
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
```

---

### Issue 4: WSL networking — bot in WSL not reachable from Windows browser

**Symptom**: Running the bot from WSL Ubuntu, but Bot Framework Emulator on Windows can't connect to `http://localhost:3978/api/messages`.

**Cause**: WSL2 mirrored networking mode is only available in Windows 11. On **Windows 10 22H2**, WSL uses NAT mode by default, and the bot port isn't auto-forwarded to Windows.

**Fix**: Run the bot from Windows native, keep WSL for Docker only. This is the recommended setup for Windows 10 users.

```
Windows:                          WSL Ubuntu:
- .NET solution                   - Docker Engine
- Bot Framework Emulator          - PostgreSQL container
- VS Code / Visual Studio         - Redis container
- IDE work
```

Connect from Windows bot → Docker containers via `localhost:5432` and `localhost:6379` (Docker exposes ports to Windows automatically).

**Alternative for Windows 11**: Enable WSL mirrored mode in `.wslconfig`:

```ini
[wsl2]
networkingMode=mirrored
```

---

### Issue 5: Port 3978 already in use

**Symptom**:
```
Unable to bind to https://localhost:3978: Address already in use
```

**Cause**: Another process is using port 3978 (perhaps an old bot instance).

**Fix**:

**Windows**:
```powershell
# Find process using port 3978
netstat -ano | findstr :3978

# Kill process by PID
taskkill /PID <pid> /F
```

**macOS / Linux**:
```bash
# Find process
lsof -i :3978

# Kill process
kill -9 <pid>
```

Or change the bot's port in `launchSettings.json` — but team standard is **port 3978** (Bot Framework default).

---

### Issue 6: Docker container can't be reached from host

**Symptom**: Bot says it can't connect to PostgreSQL at `localhost:5432`.

**Cause**: Docker port mapping not exposed correctly, OR you're trying to connect using container name instead of `localhost`.

**Fix**: Verify `docker-compose.yml` exposes ports correctly:

```yaml
services:
  postgres:
    image: postgres:16
    ports:
      - "5432:5432"   # host:container
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
```

From your host OS (Windows/Mac/Linux), always connect using `localhost` — Docker maps the container port to your host's port.

If using WSL on Windows, connect to `localhost` from BOTH Windows and inside WSL — Docker WSL2 integration makes this work.

---

### Issue 7: `dotnet ef` command not found

**Symptom**:
```
Could not execute because the specified command or file was not found.
Possible reasons for this include:
  * You misspelled a built-in dotnet command.
```

**Fix**: Install the EF Core CLI tool globally.

```bash
dotnet tool install --global dotnet-ef
```

Verify:

```bash
dotnet ef --version
```

---

### Issue 8: Permission denied on Docker commands (Linux/WSL)

**Symptom**:
```
permission denied while trying to connect to the Docker daemon socket
```

**Cause**: Your user is not in the `docker` group.

**Fix**:

```bash
sudo usermod -aG docker $USER

# Apply the group change without logging out
newgrp docker

# Or fully log out and log back in
```

---

### Issue 9: `libicu` errors at runtime

**Symptom**:
```
Process terminated. Couldn't find a valid ICU package installed
```

**Cause**: .NET 8 requires the ICU library for globalization (string formatting, date handling).

**Fix** (Linux/WSL):

```bash
sudo apt install -y libicu-dev
```

---

## Daily Workflow

Once setup is complete, your daily flow is:

```bash
# 1. Get latest develop
git checkout develop
git pull

# 2. Create your feature branch
git checkout -b feature/be-your-feature-description

# 3. Make sure containers are running
cd backend
docker compose up -d

# 4. Open your IDE, write code
code .

# 5. Run the bot
cd src/AISO.Api
dotnet run

# 6. Test in Bot Framework Emulator

# 7. Commit changes (follow conventional commits)
git add .
git commit -m "feat(be): add sales order query function"

# 8. Push and open Pull Request
git push -u origin feature/be-your-feature-description
# Go to GitHub and create PR to develop

# 9. End of day — stop containers (optional, frees memory)
cd backend
docker compose stop
```

See [Git Workflow](../docs/git-workflow.md) for branch naming and commit message rules. See [Naming Convention](../docs/AISO_Naming_Convention.docx) for code style.

---

## Useful Commands Reference

### Git

```bash
git status                              # See current state
git checkout develop && git pull        # Sync with remote develop
git checkout -b feature/be-xyz          # Create feature branch
git add . && git commit -m "feat: ..."  # Stage and commit
git push -u origin feature/be-xyz       # Push branch (first time)
git log --oneline -10                   # See last 10 commits
```

### .NET

```bash
dotnet --version                        # Verify .NET version
dotnet restore                          # Restore packages
dotnet build                            # Build solution
dotnet run --project src/AISO.Api       # Run bot
dotnet test                             # Run all tests
dotnet add package <PackageName>        # Add NuGet package
dotnet user-secrets list                # See current dev secrets
```

### EF Core (Persistence)

```bash
dotnet ef migrations add <Name> \
  --project src/AISO.Persistence \
  --startup-project src/AISO.Api        # Create new migration

dotnet ef database update \
  --project src/AISO.Persistence \
  --startup-project src/AISO.Api        # Apply migrations to DB

dotnet ef database drop \
  --project src/AISO.Persistence \
  --startup-project src/AISO.Api        # Drop the database (careful!)
```

### Docker

```bash
docker compose up -d                    # Start containers in background
docker compose stop                     # Stop containers (data kept)
docker compose down                     # Stop + remove containers (data kept)
docker compose down -v                  # Stop + remove containers + delete data
docker compose ps                       # See running containers
docker compose logs -f                  # Tail logs
docker compose logs -f postgres         # Tail one container's logs
docker exec -it aiso-postgres bash      # Shell into a container
```

---

## What's Next (Coming Soon)

This document currently covers **local development setup only**. As the project progresses, the following sections will be added:

- 🔜 **Setting up ngrok / Dev Tunnels** for a public bot endpoint
- 🔜 **Microsoft Teams app sideload** (manifest, icons, install in Teams)
- 🔜 **Azure App Service deployment** for production-like environment
- 🔜 **CI/CD pipeline** with GitHub Actions
- 🔜 **Application Insights and monitoring** setup

These sections will be added when the team is ready for those steps (planned for Sprint 1–3).

---

## Getting Help

- Stuck on setup? Ping the team leader (`@LongTran2003`) on Teams.
- Found a new bug or environment issue? Update the [Troubleshooting](#troubleshooting) section via PR — help future teammates.
- Architectural questions? See [Technical Specification](../docs/technical-specification.md) (when available).
- Bot Framework questions? Official [Microsoft docs](https://learn.microsoft.com/azure/bot-service/).

---

**Document version**: 1.0
**Last updated**: 2026-06-03
**Maintained by**: BE Lead (Trần Ngọc Quý Long)
