using System.Text.Json;
using AISO.AiOrchestration.Services;
using AISO.AiOrchestration.Stub;
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
    private readonly KeywordFunctionDispatcher _keyword;
    private readonly ILogger<AiServiceDispatcher> _logger;

    public AiServiceDispatcher(
        AiServiceClient aiClient,
        IFunctionRegistry registry,
        KeywordFunctionDispatcher keyword,
        ILogger<AiServiceDispatcher> logger)
    {
        _aiClient = aiClient;
        _registry = registry;
        _keyword = keyword;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(
        string userMessage,
        string requestingSapUser,
        UserRole role,
        CancellationToken ct = default)
    {
        // Help shortcuts / exact Admin commands must not depend on LLM tool calling.
        if (IsDeterministicShortcut(userMessage))
        {
            var shortcut = await _keyword.DispatchAsync(userMessage, requestingSapUser, role, ct);
            if (shortcut.Handled)
            {
                _logger.LogInformation(
                    "Using keyword shortcut for deterministic admin/ops intent → {Function}",
                    shortcut.FunctionName);
                return shortcut;
            }
        }

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
        var requestedName = NormalizeFunctionAlias(toolCall.FunctionName);

        _logger.LogInformation(
            "AI selected function {FunctionName} with {ArgCount} arguments",
            requestedName, toolCall.Arguments.Count);

        // Look up the function in our registry
        var function = _registry.GetByName(requestedName);
        if (function is null)
        {
            _logger.LogWarning(
                "AI requested function '{FunctionName}' which is not registered in BE. " +
                "Available: [{Available}]",
                requestedName,
                string.Join(", ", _registry.All.Select(f => f.Name)));

            return new DispatchResult
            {
                Handled = false,
                FunctionName = requestedName,
                Reason = $"Function '{requestedName}' is not registered. " +
                         "Check AI function schemas match BE function names."
            };
        }

        // Convert the AI arguments dict to a JsonElement for IFunction.ExecuteAsync
        var argsJson = JsonSerializer.Serialize(toolCall.Arguments);

        // Maker-checker: AI often maps "request release" → ReleaseOrder.
        // Employees cannot release; rewrite to RequestRelease before the role gate.
        if (role == UserRole.Employee
            && string.Equals(function.Name, "ReleaseOrder", StringComparison.OrdinalIgnoreCase))
        {
            var requestRelease = _registry.GetByName("RequestRelease");
            if (requestRelease is not null)
            {
                _logger.LogInformation(
                    "Remapping ReleaseOrder → RequestRelease for Employee (maker-checker)");
                function = requestRelease;
            }
        }

        // Admin force*: LLM often picks RejectOrder / ReleaseOrder instead.
        if (LooksLikeForceCancel(userMessage)
            && string.Equals(function.Name, "RejectOrder", StringComparison.OrdinalIgnoreCase))
        {
            var forceCancel = _registry.GetByName("ForceCancel");
            if (forceCancel is not null)
            {
                _logger.LogInformation("Remapping RejectOrder → ForceCancel for force-cancel intent");
                function = forceCancel;
                argsJson = EnsureForceReasonArg(argsJson, "Admin force cancel via Teams");
            }
        }
        else if (LooksLikeForceRelease(userMessage)
                 && (string.Equals(function.Name, "ReleaseOrder", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(function.Name, "ApproveOrder", StringComparison.OrdinalIgnoreCase)))
        {
            var forceRelease = _registry.GetByName("ForceRelease");
            if (forceRelease is not null)
            {
                _logger.LogInformation("Remapping {From} → ForceRelease for force-release intent", function.Name);
                function = forceRelease;
                argsJson = EnsureForceReasonArg(argsJson, "Admin force release via Teams");
            }
        }
        else if (LooksLikeRejectApproval(userMessage)
                 && string.Equals(function.Name, "RejectOrder", StringComparison.OrdinalIgnoreCase))
        {
            var rejectApproval = _registry.GetByName("RejectApproval");
            if (rejectApproval is not null)
            {
                _logger.LogInformation("Remapping RejectOrder → RejectApproval for reject-approval intent");
                function = rejectApproval;
            }
        }
        else if (LooksLikeCancelOrder(userMessage)
                 && string.Equals(function.Name, "RejectOrder", StringComparison.OrdinalIgnoreCase)
                 && !LooksLikeForceCancel(userMessage)
                 && !LooksLikeRejectApproval(userMessage))
        {
            var cancelOrder = _registry.GetByName("CancelOrder");
            if (cancelOrder is not null)
            {
                _logger.LogInformation("Remapping RejectOrder → CancelOrder for cancel-order intent");
                function = cancelOrder;
            }
        }

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

    /// <summary>
    /// Exact Help / Admin shortcuts where LLM hallucinations (e.g. text "GetAuditLog") are common.
    /// </summary>
    public static bool IsDeterministicShortcut(string userMessage)
    {
        var text = userMessage.Trim().ToLowerInvariant();
        return text.Contains("audit log")
               || text.Contains("auditlog")
               || text.Contains("getauditlog")
               || text.Contains("view audit")
               || text.Contains("show audit")
               || text.Contains("list user")
               || text.Contains("show user")
               || text.Contains("bot user")
               || text.Contains("manage user")
               || text.Contains("manage users")
               || text.Contains("set role")
               || text.Contains("set sales org")
               || text.Contains("danh sách user")
               || text.Contains("danh sach user")
               || text.Contains("nhật ký audit")
               || text.Contains("nhat ky audit")
               || text.Contains("force cancel")
               || text.Contains("forcecancel")
               || text.Contains("force-cancel")
               || text.Contains("force release")
               || text.Contains("forcerelease")
               || text.Contains("force-release")
               || text.Contains("reject approval")
               || text.Contains("rejectapproval")
               || text.Contains("cancel order")
               || text.Contains("cancel so")
               || (text.Contains("hủy") && text.Contains("đơn"))
               || (text.Contains("huỷ") && text.Contains("đơn"))
               || text.Contains("pending approval")
               || text.Contains("pending approvals")
               || text.Contains("show pending")
               || text.Contains("chờ duyệt")
               || text.Contains("cho duyet")
               || text.Contains("danh sách chờ duyệt")
               || text.Contains("danh sach cho duyet")
               || text.Contains("update reference")
               || text.Contains("update po reference")
               || text.Contains("change reference")
               || (text.Contains("cập nhật") && text.Contains("reference"))
               || (text.Contains("cap nhat") && text.Contains("reference"))
               || text.Contains("edit order")
               || text.Contains("edit so")
               || text.Contains("edit sales order")
               || (text.Contains("sửa") && text.Contains("đơn"))
               || (text.Contains("sua") && text.Contains("don"))
               || text.Contains("cập nhật đơn")
               || text.Contains("cap nhat don")
               || text.Contains("từ chối duyệt")
               || text.Contains("tu choi duyet")
               || text.Contains("từ chối phê duyệt")
               || text.Contains("tu choi phe duyet")
               || text.Contains("my sales")
               || text.Contains("my order")
               || text.Contains("của tôi")
               || text.Contains("cua toi")
               || text.Contains("my profile")
               || text.Contains("my info")
               || text.Contains("my account")
               || text.Contains("thông tin của tôi")
               || text.Contains("thong tin cua toi")
               || text.Contains("hồ sơ của tôi")
               || text.Contains("ho so cua toi")
               || text.Contains("overdue")
               || text.Contains("quá hạn")
               || text.Contains("qua han")
               || text.Contains("giao trễ")
               || text.Contains("giao tre");
    }

    /// <summary>Common LLM / API misnomers for registered BE functions.</summary>
    public static string NormalizeFunctionAlias(string functionName) =>
        functionName.Trim() switch
        {
            var n when n.Equals("GetAuditLog", StringComparison.OrdinalIgnoreCase) => "ViewAuditLog",
            var n when n.Equals("GetAuditLogs", StringComparison.OrdinalIgnoreCase) => "ViewAuditLog",
            var n when n.Equals("AuditLog", StringComparison.OrdinalIgnoreCase) => "ViewAuditLog",
            _ => functionName
        };

    public static bool LooksLikeForceCancel(string userMessage)
    {
        var text = userMessage.Trim().ToLowerInvariant();
        return text.Contains("force cancel")
               || text.Contains("forcecancel")
               || text.Contains("force-cancel");
    }

    public static bool LooksLikeCancelOrder(string userMessage)
    {
        if (LooksLikeForceCancel(userMessage) || LooksLikeRejectApproval(userMessage))
            return false;

        var text = userMessage.Trim().ToLowerInvariant();
        return text.Contains("cancel order")
               || text.Contains("cancel so")
               || text.Contains("cancel sales order")
               || (text.Contains("hủy") && text.Contains("đơn"))
               || (text.Contains("huỷ") && text.Contains("đơn"));
    }

    public static bool LooksLikeForceRelease(string userMessage)
    {
        var text = userMessage.Trim().ToLowerInvariant();
        return text.Contains("force release")
               || text.Contains("forcerelease")
               || text.Contains("force-release");
    }

    public static bool LooksLikeRejectApproval(string userMessage)
    {
        var text = userMessage.Trim().ToLowerInvariant();
        return text.Contains("reject approval")
               || text.Contains("rejectapproval")
               || text.Contains("từ chối duyệt")
               || text.Contains("tu choi duyet")
               || text.Contains("từ chối phê duyệt")
               || text.Contains("tu choi phe duyet")
               || text.Contains("không duyệt")
               || text.Contains("khong duyet")
               || (text.Contains("reject") && text.Contains("approval"));
    }

    /// <summary>
    /// ForceCancel/ForceRelease need <c>reason</c>; LLM RejectOrder args use <c>reason_code</c>.
    /// </summary>
    public static string EnsureForceReasonArg(string argsJson, string defaultReason)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return JsonSerializer.Serialize(new { reason = defaultReason });

            var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in root.EnumerateObject())
            {
                map[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }

            if (!map.TryGetValue("reason", out var reasonObj)
                || reasonObj is not string reasonStr
                || string.IsNullOrWhiteSpace(reasonStr))
            {
                var fromCode = map.TryGetValue("reason_code", out var code) ? code as string : null;
                map["reason"] = string.IsNullOrWhiteSpace(fromCode) ? defaultReason : fromCode;
            }

            return JsonSerializer.Serialize(map);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { reason = defaultReason });
        }
    }
}
