# AISO-Teams AI Module — Development Setup

This document explains how to set up your local development environment for the AI module.

## Prerequisites

- Python 3.10+
- pip (Python package installer)
- Git

## 1. Local Environment Setup

If you are running the AI orchestration as a standalone Python FastAPI service, follow these steps:

```bash
# Navigate to the AI directory
cd ai

# Create a virtual environment
python -m venv .venv

# Activate the virtual environment
# On Windows:
.venv\Scripts\activate
# On macOS/Linux:
source .venv/bin/activate

# Install dependencies
pip install -r requirements.txt
```

## 2. Configuration

You will need the following environment variables (set them in a `.env` file in the `ai/` folder):

```ini
AZURE_OPENAI_API_KEY=your_api_key
AZURE_OPENAI_ENDPOINT=your_endpoint
```

## 3. Run the Service

Start the FastAPI development server:

```bash
uvicorn main:app --reload --port 8000
```

The API will be available at `http://localhost:8000`. You can access the Swagger UI at `http://localhost:8000/docs`.

## Integration with Backend

Currently, the .NET Backend directly implements AI Orchestration (in `AISO.AiOrchestration`). If you are working on the C# side, refer to the [Backend Development Setup](./backend-dev-setup.md).
