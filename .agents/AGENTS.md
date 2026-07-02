# AISO Teams — Project Rules

## Code Style

- **Comments**: Keep comments short and only when truly necessary. Remove obvious/redundant comments.
- **No reformatting-only edits**: Never rewrite/reorder existing code blocks if there are no logic changes.
- **Line endings**: All C# files must use LF (`\n`), not CRLF. The CI runs `dotnet format --verify-no-changes` on Linux and will fail on CRLF files. After creating new `.cs` files on Windows, convert line endings before committing.
- **CI format**: Run `dotnet format` before committing C# code.

## Git & Branch

- Always create a feature branch before making changes: `git checkout -b feature/<name>`
- Follow conventional commit format: `feat(scope): ...`, `fix(scope): ...`, `docs(scope): ...`
- Push and create PR; do not commit directly to `develop` or `main`.

## Architecture

- KPI domain models live in `AISO.Domain/Kpi/`.
- All new `IFunction` handlers go in `AISO.AiOrchestration/Functions/`.
- Register every new `IFunction` in `backend/src/AISO.Api/Extensions/ServiceCollectionExtensions.cs` DI.
- `MockSapClient` must implement every method on `ISapClient` — keep mocks minimal (no debug logs beyond a single LogDebug line).
