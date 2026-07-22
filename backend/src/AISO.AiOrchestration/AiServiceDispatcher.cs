using System.Text.Json;
using AISO.AiOrchestration.Services;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration;

/// <summary>
/// Dispatcher that delegates intent detection to the AI microservice (Python/FastAPI).
/// Flow:
///   1. Send user message → AI service
///   2. Receive tool_calls (function name + arguments)
///   3. Look up the function in <see cref="IFunctionRegistry"/>
///   4. Execute the function with the AI-extracted parameters
///   5. Return the result to the bot layer
///
/// Falls back to <c>Handled = false</c> when:
/// - The AI service returns no tool_calls (general query / chitchat)
/// - The function name returned by AI is not registered in BE
/// - The AI service is unreachable
/// </summary>
public sealed class AiServiceDispatcher : IFunctionDispatcher
{
    private readonly AiServiceClient _aiClient;
    private readonly IFunctionRegistry _registry;
    private readonly ILogger<AiServiceDispatcher> _logger;

    public AiServiceDispatcher(
        AiServiceClient aiClient,
        IFunctionRegistry registry,
        ILogger<AiServiceDispatcher> logger)
    {
        _aiClient = aiClient;
        _registry = registry;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(
        string userMessage,
        string requestingSapUser,
        UserRole role,
        CancellationToken ct = default)
    {
        AiOrchestratorResponse aiResponse;

        try
        {
            aiResponse = await _aiClient.OrchestrateAsync(userMessage, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service call failed, returning unhandled result");
            return new DispatchResult
            {
                Handled = false,
                Reason = $"AI service unavailable: {ex.Message}"
            };
        }

        // If AI returned no tool_calls → it's a general/chitchat response.
        // Return the AI reply text as a "handled" conversational response.
        if (aiResponse.ToolCalls.Count == 0)
        {
            _logger.LogInformation(
                "AI returned no tool_calls (intent={Intent}), returning text reply",
                aiResponse.Intent);

            return new DispatchResult
            {
                Handled = true,
                FunctionName = "ai_text_reply",
                Result = FunctionResult.Ok(aiResponse.Reply)
            };
        }

        // Process the first tool call (primary intent).
        // Multi-tool-call support can be added later.
        var toolCall = aiResponse.ToolCalls[0];

        _logger.LogInformation(
            "AI selected function {FunctionName} with {ArgCount} arguments",
            toolCall.FunctionName, toolCall.Arguments.Count);

        // Look up the function in our registry
        var function = _registry.GetByName(toolCall.FunctionName);
        if (function is null)
        {
            _logger.LogWarning(
                "AI requested function '{FunctionName}' which is not registered in BE. " +
                "Available: [{Available}]",
                toolCall.FunctionName,
                string.Join(", ", _registry.All.Select(f => f.Name)));

            return new DispatchResult
            {
                Handled = false,
                FunctionName = toolCall.FunctionName,
                Reason = $"Function '{toolCall.FunctionName}' is not registered. " +
                         "Check AI function schemas match BE function names."
            };
        }

        // Convert the AI arguments dict to a JsonElement for IFunction.ExecuteAsync
        var argsJson = JsonSerializer.Serialize(toolCall.Arguments);
        using var argsDoc = JsonDocument.Parse(argsJson);

        // Role-based access control (Phase B): block the action before any side effect.
        if (!RolePolicy.CanExecute(role, function.Name))
        {
            var requiredRole = RolePolicy.RequiredRole(function.Name);
            _logger.LogWarning(
                "Access denied: user (role {Role}) attempted {FunctionName} which requires {RequiredRole}",
                role, function.Name, requiredRole);

            return new DispatchResult
            {
                Handled = true,
                Denied = true,
                FunctionName = function.Name,
                ParametersJson = argsJson,
                Result = FunctionResult.Fail(
                    $"You do not have permission to perform this action. " +
                    $"'{function.Name}' requires the {requiredRole} role, but your role is {role}.")
            };
        }

        _logger.LogInformation(
            "Executing function {FunctionName} with parameters: {Parameters}",
            function.Name, argsJson);

        try
        {
            var result = await function.ExecuteAsync(argsDoc.RootElement, requestingSapUser, ct);

            return new DispatchResult
            {
                Handled = true,
                FunctionName = function.Name,
                Result = result,
                ParametersJson = argsJson
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while executing function {FunctionName}", function.Name);
            return new DispatchResult
            {
                Handled = true,
                FunctionName = function.Name,
                Result = FunctionResult.Fail(ex.Message),
                ParametersJson = argsJson
            };
        }
    }
}


