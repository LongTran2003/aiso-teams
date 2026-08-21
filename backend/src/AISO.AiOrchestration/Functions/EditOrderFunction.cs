using System.Text.Json;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Prepare full edit form (header PO/date + one line I/U/D).
/// SAP call runs on Adaptive Card <c>edit_so_confirm</c>.
/// Employee: own SO only. Manager/Admin: any SO (Manager scoped by SalesOrg when set).
/// </summary>
public sealed class EditOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<EditOrderFunction> _logger;

    public EditOrderFunction(
        ISapClient sap,
        IUserScopeLookup scope,
        ILogger<EditOrderFunction> logger)
    {
        _sap = sap;
        _scope = scope;
        _logger = logger;
    }

    public string Name => "EditOrder";

    public string Description =>
        "Edit an existing sales order in SAP (PO reference, requested delivery date, and one line add/update/delete). " +
        "Returns a confirmation form — does not change SAP until the user confirms.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier."
            },
            "new_reference": {
              "type": "string",
              "description": "Optional draft PO / customer reference."
            },
            "req_delivery_date": {
              "type": "string",
              "description": "Optional requested delivery date (yyyy-MM-dd)."
            },
            "line_op": {
              "type": "string",
              "description": "Optional line operation: none, U, I, or D."
            },
            "item_no": { "type": "string" },
            "material": { "type": "string" },
            "qty": { "type": "number" },
            "plant": { "type": "string" },
            "unit": { "type": "string" }
          },
          "required": ["order_id"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderId = ReadString(parameters, "order_id");
        if (string.IsNullOrWhiteSpace(orderId))
            return FunctionResult.Fail("Missing required parameter: order_id", "VALIDATION");

        try
        {
            var existing = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (existing is null)
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.", "NOT_FOUND");

            if (SalesOrderWorkflow.BlocksReject(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Edit"),
                    "VALIDATION");
            }

            if (existing.HasInvalidMaterial)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Edit"),
                    "VALIDATION");
            }

            var role = await _scope.GetRoleBySapUserAsync(requestingSapUser, ct);
            var authError = await ValidateEditAuthorizationAsync(existing, requestingSapUser, role, ct);
            if (authError is not null)
                return FunctionResult.Fail(authError, "VALIDATION");

            var first = existing.Items?.FirstOrDefault();
            var draftRef = ReadString(parameters, "new_reference");
            if (string.IsNullOrWhiteSpace(draftRef))
                draftRef = existing.CustomerReference ?? string.Empty;

            var draftDate = ReadString(parameters, "reqDeliveryDate");
            if (string.IsNullOrWhiteSpace(draftDate))
            {
                draftDate = existing.RequestedDeliveryDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            }

            var explicitQty = parameters.TryGetProperty("qty", out var qtyEl) && qtyEl.ValueKind == JsonValueKind.Number;
            var explicitMaterial = ReadString(parameters, "material") != null;

            var rawLineOp = ReadString(parameters, "lineOperation");
            var lineOp = rawLineOp switch
            {
                "Update quantity / material" => "U",
                "Add line" => "I",
                "Delete line" => "D",
                _ => explicitQty || explicitMaterial ? "U" : "none"
            };

            var itemNo = ReadString(parameters, "itemNumber");
            var targetItem = existing.Items?.FirstOrDefault(); // Default

            if (!string.IsNullOrWhiteSpace(itemNo))
            {
                targetItem = existing.Items?.FirstOrDefault(i =>
                    string.Equals(i.ItemNumber?.TrimStart('0'), itemNo.TrimStart('0'), StringComparison.OrdinalIgnoreCase))
                    ?? targetItem;
            }
            else
            {
                itemNo = targetItem?.ItemNumber?.TrimStart('0') ?? "10";
            }

            if (string.IsNullOrWhiteSpace(itemNo))
                itemNo = "10";

            var material = explicitMaterial ? ReadString(parameters, "material") : (targetItem?.Material ?? string.Empty);
            var plant = ReadString(parameters, "plant") ?? "1010";
            var unit = ReadString(parameters, "unit") ?? targetItem?.Unit ?? "PC";

            decimal qty = targetItem?.Quantity ?? 1m;
            if (explicitQty)
                qty = qtyEl.GetDecimal();

            if (lineOp is "I" or "U" && qty <= 0)
            {
                return FunctionResult.Fail(
                    $"Quantity must be greater than 0 for line {lineOp switch { "I" => "insert", _ => "update" }}.",
                    "VALIDATION");
            }

            if (lineOp is "I" && string.IsNullOrWhiteSpace(material))
            {
                return FunctionResult.Fail(
                    "Material is required when inserting a new line item.",
                    "VALIDATION");
            }

            var linesSummary = existing.Items is { Count: > 0 }
                ? string.Join("; ", existing.Items.Select(i =>
                    $"{TrimItem(i.ItemNumber)} · {i.Material} x {i.Quantity:0} {i.Unit}"))
                : "No line items";

            _logger.LogInformation(
                "EditOrder confirm step: so={SoNumber} by={User} role={Role}",
                existing.SoNumber, requestingSapUser, role);

            return FunctionResult.Ok(new ConfirmEditOrderResponse(
                existing.SoNumber,
                existing.CustomerReference ?? string.Empty,
                draftRef.Trim(),
                existing.RequestedDeliveryDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                draftDate.Trim(),
                lineOp,
                itemNo?.Trim() ?? string.Empty,
                material?.Trim().ToUpperInvariant() ?? string.Empty,
                qty,
                plant?.Trim() ?? string.Empty,
                unit?.Trim().ToUpperInvariant() ?? string.Empty,
                linesSummary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EditOrder prepare failed for {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to prepare edit order: {ex.Message}", "ACTION_FAILED");
        }
    }

    private async Task<string?> ValidateEditAuthorizationAsync(
        SalesOrder order,
        string requestingSapUser,
        UserRole role,
        CancellationToken ct)
    {
        if (role >= UserRole.Admin)
            return null;

        if (role >= UserRole.Manager)
        {
            var delegationInfo = await _scope.GetDelegationInfoAsync(requestingSapUser, ct);
            var effectiveUserForOrg = !string.IsNullOrWhiteSpace(delegationInfo.DelegatorSapUser)
                ? delegationInfo.DelegatorSapUser
                : requestingSapUser;
            var managerOrg = await _scope.GetSalesOrgBySapUserAsync(effectiveUserForOrg, ct);
            if (!string.IsNullOrWhiteSpace(managerOrg)
                && !string.IsNullOrWhiteSpace(order.SalesOrg)
                && !string.Equals(managerOrg, order.SalesOrg, StringComparison.OrdinalIgnoreCase))
            {
                return $"Order {order.SoNumber} belongs to sales org {order.SalesOrg}; your scope is {managerOrg}.";
            }

            return null;
        }

        if (!SalesOrderWorkflow.IsCurrentOwner(order.OwnerSapUser, requestingSapUser)
            && !string.IsNullOrWhiteSpace(order.OwnerSapUser))
        {
            return SalesOrderWorkflow.BuildNotOwnerBlockedMessage("Edit", order.OwnerSapUser);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static string TrimItem(string? itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber))
            return "—";
        var t = itemNumber.TrimStart('0');
        return string.IsNullOrEmpty(t) ? "0" : t;
    }
}

/// <summary>Payload telling the bot to show <c>confirm-edit-order.json</c>.</summary>
public sealed record ConfirmEditOrderResponse(
    string SoNumber,
    string CurrentReference,
    string NewReference,
    string CurrentReqDate,
    string NewReqDate,
    string LineOp,
    string ItemNumber,
    string Material,
    decimal Qty,
    string Plant,
    string Unit,
    string LinesSummary);
