# AI Module

LLM function calling and prompt orchestration.

## Quick start (if Python FastAPI)

```bash
cd ai
python -m venv .venv
source .venv/bin/activate    # Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

## Quick start (if .NET in-process)

AI module is part of backend solution. See `backend/src/AISO.AiOrchestration/`.

## Structure

- `prompts/` — System prompts and few-shot examples
- `functions/` — Function definitions (JSON schemas)
- `tests/` — Test cases (NL query → expected function call)