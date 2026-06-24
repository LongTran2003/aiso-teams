# AISO-Teams AI Module

LLM function calling and prompt orchestration for the AISO-Teams bot.

## Modules

| Folder | Purpose |
|---|---|
| `prompts/` | System prompts and few-shot examples |
| `functions/` | Function definitions (JSON schemas) |
| `tests/` | Test cases (NL query → expected function call) |

## Quick start

The AI module is currently integrated as part of the backend solution in `backend/src/AISO.AiOrchestration/`. However, if running a standalone Python FastAPI environment:

```bash
# 1. Setup virtual environment
cd ai
python -m venv .venv
source .venv/bin/activate    # Windows: .venv\Scripts\activate

# 2. Install dependencies
pip install -r requirements.txt

# 3. Run the server
uvicorn main:app --reload --port 8000
```

## CI

Automated tests for AI prompt validation and function calling will be integrated into the CI pipeline in upcoming sprints.

## More docs

- [Architecture](../docs/foundation/architecture.md)
- [Sprint plan](../docs/planning/sprint-plan.md)