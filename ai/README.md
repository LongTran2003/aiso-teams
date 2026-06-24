# AISO-Teams AI Module

LLM function calling and prompt orchestration for the AISO-Teams bot.

## Structure

- `prompts/` — System prompts and few-shot examples
- `functions/` — Function definitions (JSON schemas)
- `tests/` — Test cases (NL query → expected function call)

## Quick start (if Python FastAPI)

```bash
cd ai
python -m venv .venv
source .venv/bin/activate    # Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

## Quick start (if .NET in-process)

The AI module is currently integrated as part of the backend solution. See `backend/src/AISO.AiOrchestration/`.

## More docs

Please refer to the `docs/` folder at the root of the repository for comprehensive documentation.