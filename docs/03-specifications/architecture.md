# Technical Specification: Bot Architecture & Function Calling

**Status:** Draft
**Target Sprint:** Sprint 2
**Author:** BE Lead

## 1. Overview
The AISO-Teams bot bridges Microsoft Teams and SAP S/4HANA by utilizing Azure Bot Service for message handling and a separate Python FastAPI Microservice for Azure OpenAI function calling orchestration. 

This document outlines the architecture, data flow, and class interactions for processing user messages.

## 2. Architecture Components

- **Microsoft Teams Client:** The UI where users interact with the bot. Sends and receives activities (text, Adaptive Cards).
- **Azure Bot Service:** The channel registration that routes messages between Teams and the .NET Backend (`AISO.Api`).
- **.NET Backend (`AISO.Bot` & `AISO.AiOrchestration`):** 
  - Receives activities from Azure Bot Service.
  - Sends user text to the AI Microservice.
  - Receives an intent and an optional list of `ToolCalls` to execute.
  - Dispatches tool calls to specific `IFunction` implementations.
  - Sends responses back to Teams as Adaptive Cards.
- **AI Microservice:** Connects directly to Azure OpenAI. Contains the system prompt, handles conversation history, and decides which tools/functions the backend should execute based on the user's intent.

## 3. Data Flow (End-to-End)

1. User sends message: `"kiểm tra đơn hàng 5001"`.
2. Teams -> Azure Bot Service -> `POST /api/messages` -> `TeamsBot.OnMessageActivityAsync()`.
3. `TeamsBot` calls `IFunctionDispatcher.DispatchAsync(message)`.
4. `AiServiceDispatcher` sends an HTTP POST request to the AI Microservice.
5. AI Microservice calls Azure OpenAI GPT-4o. GPT-4o identifies the intent (`check_order`) and returns a tool call: `CheckOrderStatus({"order_id": "0000005001"})`.
6. AI Microservice returns `AiOrchestratorResponse` to the .NET Backend.
7. `AiServiceDispatcher` iterates over `ToolCalls`, looking up `IFunction` in `IFunctionRegistry` by name (`CheckOrderStatus`).
8. `CheckOrderStatusFunction.ExecuteAsync()` is invoked. It queries `ISapClient` (or `MockSapClient`) for the order data.
9. `ExecuteAsync()` returns `FunctionResult.Ok(data)`.
10. `AiServiceDispatcher` returns a `DispatchResult` containing the payload to `TeamsBot`.
11. `TeamsBot` checks the payload type. If it's a list of `SalesOrder`, it builds an Adaptive Card using `SoSummaryCardBuilder`.
12. `TeamsBot` sends the Adaptive Card back to the user via Bot Framework.

## 4. Class Design

### `IFunction`
The core interface for any executable action the AI can trigger.
```csharp
public interface IFunction
{
    string Name { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }
    Task<FunctionResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default);
}
```

### `FunctionRegistry`
Holds all registered functions (`GetSalesOrders`, `CheckOrderStatus`, `ReleaseOrder`, etc.) and provides case-insensitive lookup.

### `IFunctionDispatcher`
Responsible for interpreting a user string and returning a `DispatchResult`. 
- `AiServiceDispatcher`: Primary implementation calling the Python microservice.
- `KeywordFunctionDispatcher`: Fallback implementation using Regex for local testing/development without AI.

## 5. Security & Authorization (Sprint 3 Preview)
- **UserMapping Entity:** Created in `AISO.Domain` and persisted via Entity Framework. Links the `TeamsUserId` (from the Bot Framework Activity) to a `SapUsername`. This ensures the SAP OData calls can be executed under the correct authorization context.
- **User Secrets:** All sensitive variables (AppId, TenantId, Client Secret) are managed locally via `dotnet user-secrets` and mapped to Azure Key Vault or App Service environment variables in production.

## 6. Audit Logging
Every dispatched function call (whether successful or failed) is intercepted and logged into the `audit_logs` PostgreSQL table. This table tracks the `TeamsUserId`, `Action`, `ParametersJson`, `DurationMs`, and `ResultStatus` for traceability and compliance.
