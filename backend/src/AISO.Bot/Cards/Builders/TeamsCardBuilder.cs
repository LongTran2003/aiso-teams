using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using Microsoft.Bot.Schema;

namespace AISO.Bot.Cards.Builders;

internal static class TeamsCardBuilder
{
    public static Attachment BuildWelcomeCard(string username) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("welcome.json", new { username });

    public static Attachment BuildLinkSapAccountCard(
        string displayName,
        string? errorMessage = null,
        string? assignedSapUserId = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "link-sap-account.json",
            new
            {
                displayName,
                hasError = string.IsNullOrWhiteSpace(errorMessage) ? "false" : "true",
                errorMessage = errorMessage ?? string.Empty,
                hasAssignedId = string.IsNullOrWhiteSpace(assignedSapUserId) ? "false" : "true",
                assignedSapUserId = assignedSapUserId?.Trim().ToUpperInvariant() ?? string.Empty
            });

    public static Attachment BuildHelpCard(string? role = null)
    {
        var (parsedRole, roleLabel) = NormalizeRole(role);
        var commands = HelpCommandCatalog
            .ForRole(parsedRole)
            .Select(c => new
            {
                icon = c.Icon,
                en = c.En,
                vi = c.Vi,
                note = c.Note ?? string.Empty,
                flow = c.Flow,
            })
            .ToList();

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "help.json",
            new
            {
                role = roleLabel,
                hasCommands = commands.Count > 0 ? "true" : "false",
                commands
            });
    }

    private static (UserRole role, string label) NormalizeRole(string? role) =>
        role?.Trim().ToLowerInvariant() switch
        {
            "admin" => (UserRole.Admin, "Admin"),
            "manager" => (UserRole.Manager, "Manager"),
            "employee" => (UserRole.Employee, "Employee"),
            _ => (UserRole.Employee, "Employee"),
        };

    public static Attachment BuildEmptyCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("empty.json");

    public static Attachment BuildMyProfileCard(MyProfileResponse response) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "my-profile.json",
            new
            {
                sapUser = response.SapUser,
                displayName = string.IsNullOrWhiteSpace(response.SapUser) ? "(unknown)" : response.SapUser,
                role = response.Role.ToString(),
                salesOrg = string.IsNullOrWhiteSpace(response.SalesOrg) ? "(none)" : response.SalesOrg,
                email = string.IsNullOrWhiteSpace(response.Email) ? "(unlinked)" : response.Email,
                hasEmail = string.IsNullOrWhiteSpace(response.Email) ? "false" : "true",
                salesOrgValidFrom = response.SalesOrgValidFrom?.ToString("dd MMM yyyy") ?? "",
                salesOrgValidTo = response.SalesOrgValidTo?.ToString("dd MMM yyyy") ?? "",
                hasValidFrom = response.SalesOrgValidFrom.HasValue ? "true" : "false",
                hasValidTo = response.SalesOrgValidTo.HasValue ? "true" : "false",
                salesOrgStatus = DescribeSalesOrgStatus(response.SalesOrgIsActive),
                hasSalesOrgStatus = response.SalesOrgIsActive.HasValue ? "true" : "false",
                total = response.Counts.Total,
                open = response.Counts.Open,
                blocked = response.Counts.Blocked,
                partial = response.Counts.PartiallyDelivered,
                delivered = response.Counts.Delivered,
                invoiced = response.Counts.Invoiced,
                cancelled = response.Counts.Cancelled,
                approximateHint = response.Approximate
                    ? $"Counts are approximate — showing the {MyProfileFunction.MaxOrdersForStats} most recent orders. You may own more."
                    : $"Counts are exact ({response.Counts.Total} order(s) owned).",
                hasLoadError = string.IsNullOrEmpty(response.LoadError) ? "false" : "true",
                loadError = response.LoadError ?? string.Empty,
                hasTopOrders = response.TopOrders.Count > 0 ? "true" : "false",
                topOrders = response.TopOrders.Select(o => new
                {
                    soNumber = o.SoNumber,
                    customerLabel = string.IsNullOrWhiteSpace(o.CustomerName) ? o.CustomerId : $"{o.CustomerId} · {o.CustomerName}",
                    orderDate = o.OrderDate.ToString("dd MMM yyyy"),
                    status = o.Status.ToString(),
                    formattedNetValue = $"{o.NetValue:N0} {o.Currency}"
                }).ToList()
            });

    /// <summary>
    /// Renders the Sales-org validity status into a short label the card can
    /// colour: <c>Active</c>, <c>Expired</c>, <c>Pending</c>. Only called when
    /// <c>SalesOrgIsActive</c> has a value (the template hides the row otherwise).
    /// </summary>
    private static string DescribeSalesOrgStatus(bool? isActive)
        => isActive switch
        {
            true => "Active",
            false => "Expired or pending",
            _ => "Unknown",
        };

    public static Attachment BuildLoadingCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("loading.json");

    public static Attachment BuildSuccessCard(string salesOrderNumber, string status, string? detail = null)
    {
        var (headline, message, statusLabel, showPendingLink) = DescribeSuccess(status, detail);
        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "success.json",
            new
            {
                salesOrderNumber,
                status,
                headline,
                message,
                statusLabel,
                showPendingLink = showPendingLink ? "true" : "false"
            });
    }

    public static Attachment BuildErrorCard(string errorCode, string errorMessage, string? title = null, string? summary = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "error.json",
            new
            {
                errorCode,
                errorMessage = SanitizeErrorMessage(errorMessage),
                title = title ?? TitleForErrorCode(errorCode),
                summary = summary ?? SummaryForErrorCode(errorCode)
            });

    /// <summary>
    /// Strips raw JSON, stack traces, and other overly technical content
    /// from error messages before showing them to users.
    /// </summary>
    private static string SanitizeErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "An unexpected error occurred. Please try again or contact your admin.";

        var msg = message.Trim();

        // Strip anything after "Raw response:" or "Raw:"
        var rawIdx = msg.IndexOf("Raw response:", StringComparison.OrdinalIgnoreCase);
        if (rawIdx < 0)
            rawIdx = msg.IndexOf("Raw:", StringComparison.OrdinalIgnoreCase);
        if (rawIdx > 0)
            msg = msg[..rawIdx].TrimEnd(' ', '.', ',');

        // If the message looks like it's mostly JSON, replace it entirely
        if (msg.TrimStart().StartsWith("{") || msg.TrimStart().StartsWith("["))
            return "SAP could not complete this request. Please try again or contact your admin.";

        // If the message contains a stack trace (common in .NET exceptions), truncate it
        var stackIdx = msg.IndexOf("   at ", StringComparison.Ordinal);
        if (stackIdx < 0)
            stackIdx = msg.IndexOf("  at ", StringComparison.Ordinal);
        if (stackIdx > 0)
            msg = msg[..stackIdx].TrimEnd();

        // Ensure message isn't empty after stripping
        if (string.IsNullOrWhiteSpace(msg))
            return "An unexpected error occurred. Please try again or contact your admin.";

        return msg;
    }

    private static string TitleForErrorCode(string errorCode) => errorCode.ToUpperInvariant() switch
    {
        "NOT_FOUND" => "Not found",
        "NOT_LINKED" => "Account not linked",
        "VALIDATION" => "Invalid request",
        "NOT_AUTHORIZED" => "Not authorized",
        "UNAUTHENTICATED" => "Session expired",
        "SAP_ERROR" => "SAP error",
        "SAP_UNAVAILABLE" => "SAP unavailable",
        "COLD_START" => "Bot is starting up",
        _ => "Something went wrong"
    };

    private static string SummaryForErrorCode(string errorCode) => errorCode.ToUpperInvariant() switch
    {
        "NOT_FOUND" => "Nothing matched this request.",
        "NOT_LINKED" => "Link your SAP User ID before running this action.",
        "VALIDATION" => "Check the details below and try again.",
        "UNAUTHENTICATED" => "Your session expired or is not authenticated. Send any message to sign in again.",
        "SAP_ERROR" => "SAP could not complete this request.",
        "SAP_UNAVAILABLE" => "The SAP system is unreachable or your account does not have access. Please contact your administrator.",
        "COLD_START" => "The bot is warming up after a restart. Please send your message again in a few seconds — the next reply is usually faster.",
        _ => "The bot could not complete this request right now. Please try again in a moment."
    };

    public static Attachment BuildNotAuthorizedCard(string errorMessage, string currentRole, string requiredRole) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "not-authorized.json",
            new { errorMessage, currentRole, requiredRole });

    public static Attachment BuildConfirmRejectCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-reject.json",
            new
            {
                salesOrderNumber,
                reasons = SalesOrderRejectionReasons.All
                    .Select(r => new { title = r.Title, value = r.Code })
                    .ToList()
            });

    public static Attachment BuildConfirmCancelCard(
        string salesOrderNumber,
        string? reason = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-cancel.json",
            new
            {
                salesOrderNumber,
                reason = reason ?? string.Empty
            });

    public static Attachment BuildCreateOrderStep1Card(
        IReadOnlyList<SapSalesOrg> salesOrgs,
        string? selectedSalesOrg = null,
        IReadOnlyList<SapDistChannel>? distChannels = null,
        string? selectedDistChannel = null,
        IReadOnlyList<SapDivision>? divisions = null,
        string? selectedDivision = null)
    {
        var card = BuildStep1CardData(
            salesOrgs, selectedSalesOrg,
            distChannels, selectedDistChannel,
            divisions, selectedDivision);

        return CardTemplateFileLoader.BuildAdaptiveCardFromObject(card);
    }

    private static object BuildStep1CardData(
        IReadOnlyList<SapSalesOrg> salesOrgs,
        string? selectedSalesOrg,
        IReadOnlyList<SapDistChannel>? distChannels,
        string? selectedDistChannel,
        IReadOnlyList<SapDivision>? divisions,
        string? selectedDivision)
    {
        var orgChoices = salesOrgs
            .Select(o => new Dictionary<string, object> { ["title"] = $"{o.SalesOrg} — {o.SalesOrgName}", ["value"] = o.SalesOrg.ToUpperInvariant().Trim() })
            .ToList();

        var chanChoices = (distChannels ?? [])
            .Select(c => new Dictionary<string, object> { ["title"] = c.DistChannel, ["value"] = c.DistChannel.ToUpperInvariant().Trim() })
            .ToList();

        var divChoices = (divisions ?? [])
            .Select(d => new Dictionary<string, object> { ["title"] = d.Division, ["value"] = d.Division.ToUpperInvariant().Trim() })
            .ToList();

        var body = new List<object>
        {
            new Dictionary<string, object> {
                ["type"] = "Container",
                ["style"] = "Accent",
                ["bleed"] = true,
                ["items"] = new object[] {
                    new Dictionary<string, object> { ["type"] = "TextBlock", ["text"] = "Create sales order (Step 1 of 4)", ["weight"] = "Bolder", ["size"] = "Medium", ["color"] = "Accent", ["wrap"] = true }
                }
            },
            new Dictionary<string, object> { ["type"] = "TextBlock", ["text"] = "Sales Organization *", ["weight"] = "Bolder", ["size"] = "Small", ["spacing"] = "Medium", ["wrap"] = true },
            new Dictionary<string, object> {
                ["type"] = "Input.ChoiceSet",
                ["id"] = "salesOrg",
                ["label"] = "Sales Organization",
                ["style"] = "compact",
                ["isRequired"] = true,
                ["errorMessage"] = "Please select a Sales Organization",
                ["value"] = selectedSalesOrg ?? "",
                ["choices"] = orgChoices
            }
        };

        body.Add(new Dictionary<string, object> { ["type"] = "TextBlock", ["text"] = "Distribution Channel *", ["weight"] = "Bolder", ["size"] = "Small", ["spacing"] = "Medium", ["wrap"] = true });

        if (chanChoices.Count > 0)
        {
            body.Add(new Dictionary<string, object>
            {
                ["type"] = "Input.ChoiceSet",
                ["id"] = "distChannel",
                ["label"] = "Distribution Channel",
                ["style"] = "compact",
                ["isRequired"] = true,
                ["errorMessage"] = "Please select a Distribution Channel",
                ["value"] = selectedDistChannel ?? "",
                ["choices"] = chanChoices
            });
        }
        else
        {
            body.Add(new Dictionary<string, object>
            {
                ["type"] = "TextBlock",
                ["text"] = "(select Sales Organization to load)",
                ["size"] = "Small",
                ["isSubtle"] = true,
                ["wrap"] = true
            });
        }

        body.Add(new Dictionary<string, object> { ["type"] = "TextBlock", ["text"] = "Division *", ["weight"] = "Bolder", ["size"] = "Small", ["spacing"] = "Medium", ["wrap"] = true });

        if (divChoices.Count > 0)
        {
            body.Add(new Dictionary<string, object>
            {
                ["type"] = "Input.ChoiceSet",
                ["id"] = "division",
                ["label"] = "Division",
                ["style"] = "compact",
                ["isRequired"] = true,
                ["errorMessage"] = "Please select a Division",
                ["value"] = selectedDivision ?? "",
                ["choices"] = divChoices
            });
        }
        else
        {
            body.Add(new Dictionary<string, object>
            {
                ["type"] = "TextBlock",
                ["text"] = "(select Distribution Channel to load)",
                ["size"] = "Small",
                ["isSubtle"] = true,
                ["wrap"] = true
            });
        }

        return new Dictionary<string, object>
        {
            ["type"] = "AdaptiveCard",
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["version"] = "1.5",
            ["body"] = body,
            ["actions"] = new object[] {
                new Dictionary<string, object> {
                    ["type"] = "Action.Submit",
                    ["title"] = "Next",
                    ["style"] = "positive",
                    ["data"] = new Dictionary<string, object> {
                        ["action"] = "create_so_step1_submit"
                    }
                },
                new Dictionary<string, object> {
                    ["type"] = "Action.Submit",
                    ["title"] = "Cancel",
                    ["data"] = new Dictionary<string, object> {
                        ["msteams"] = new Dictionary<string, object> { ["type"] = "messageBack", ["displayText"] = "cancel", ["text"] = "cancel" }
                    }
                }
            }
        };
    }

    public static Attachment BuildCreateOrderStep2Card(
        string salesAreaLabel,
        string salesAreaKey,
        string salesOrg,
        string distChannel,
        string division,
        IReadOnlyList<ConfirmCreateChoice> customerChoices,
        IReadOnlyList<SapDocType>? docTypes = null,
        string? selectedDocType = null,
        string? currency = null,
        string? purchaseOrderRef = null,
        string? requestedDeliveryDate = null,
        string? shipToParty = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "create-so-step2.json",
            new
            {
                salesAreaLabel,
                salesAreaKey,
                salesOrg,
                distChannel,
                division,
                customerChoices,
                docTypeChoices = docTypes?.Select(d => new { title = $"{d.DocType} — {d.DocTypeName}", value = d.DocType }).ToList(),
                selectedDocType = selectedDocType ?? "",
                currency = currency ?? "USD",
                purchaseOrderRef = purchaseOrderRef ?? "",
                requestedDeliveryDate = requestedDeliveryDate ?? "",
                shipToParty = shipToParty ?? "",
                hasShipToParty = !string.IsNullOrWhiteSpace(shipToParty) ? "true" : "false",
                customer = ""
            });

    public static Attachment BuildCreateOrderStep3Card(
        string customerLabel,
        string customerKey,
        string salesAreaLabel,
        string salesAreaKey,
        IReadOnlyList<ConfirmCreateChoice> materialChoices,
        string? docType = null,
        string? currency = null,
        string? purchaseOrderRef = null,
        string? requestedDeliveryDate = null,
        string? shipToParty = null,
        string? salesOrg = null,
        string? distChannel = null,
        string? division = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "create-so-step3.json",
            new
            {
                customerLabel,
                    customer = customerKey,
                    salesAreaLabel,
                salesAreaKey,
                salesOrg = salesOrg ?? "",
                distChannel = distChannel ?? "",
                division = division ?? "",
                materialChoices,
                docType = docType ?? "TA",
                currency = currency ?? "USD",
                purchaseOrderRef = purchaseOrderRef ?? "",
                requestedDeliveryDate = requestedDeliveryDate ?? "",
                shipToParty = shipToParty ?? "",
                hasShipToParty = !string.IsNullOrWhiteSpace(shipToParty) ? "true" : "false",
                unit1 = "",
                unit2 = "",
                unit3 = ""
            });

    public static Attachment BuildCreateOrderStep4ReviewCard(
        string salesAreaLabel,
        string customerLabel,
        string? shipToParty,
        string docType,
        string currency,
        string? purchaseOrderRef,
        string? requestedDeliveryDate,
        IReadOnlyList<ConfirmCreateOrderLine> lineItems,
        string salesAreaKey,
        string salesOrg,
        string distChannel,
        string division,
        string customerKey,
        string customerId) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "create-so-step4.json",
            new
            {
                salesAreaLabel,
                customerLabel,
                shipToParty = string.IsNullOrWhiteSpace(shipToParty) ? "(default)" : shipToParty,
                docType,
                currency,
                purchaseOrderRef = string.IsNullOrWhiteSpace(purchaseOrderRef) ? "-" : purchaseOrderRef,
                requestedDeliveryDate = string.IsNullOrWhiteSpace(requestedDeliveryDate) ? "-" : requestedDeliveryDate,
                lineItems = lineItems.Select(l => new { l.Material, l.Qty }).ToList(),
                lineItemsJson = System.Text.Json.JsonSerializer.Serialize(lineItems),
                salesAreaKey,
                salesOrg,
                distChannel,
                division,
                customerKey,
                customerId
            });

    /// <summary>Backward-compatible overload (single material).</summary>
    public static Attachment BuildConfirmCreateOrderCard(
        string customer,
        string material,
        decimal qty,
        string salesOrg,
        string currency,
        string plant = "1010",
        string unit = "PC") =>
        BuildConfirmCreateOrderCard(
            new ConfirmCreateOrderResponse(
                customer,
                salesOrg,
                currency,
                plant,
                unit,
                new[] { new ConfirmCreateOrderLine(material, qty) }));

    public static Attachment BuildConfirmCreateOrderCard(ConfirmCreateOrderResponse draft)
    {
        var lines = NormalizeCreateLines(draft.Lines);

        string SlotMaterial(int i) =>
            i < lines.Count ? lines[i].Material : string.Empty;
        decimal SlotQty(int i) =>
            i < lines.Count ? lines[i].Qty : (i == 0 ? 1m : 0m);

        var salesOrg = string.IsNullOrWhiteSpace(draft.SalesOrg) ? "TV01" : draft.SalesOrg.Trim();
        var distChannel = string.IsNullOrWhiteSpace(draft.DistChannel) ? "10" : draft.DistChannel.Trim();
        var division = string.IsNullOrWhiteSpace(draft.Division) ? "00" : draft.Division.Trim();
        var salesAreaKey = $"{salesOrg}|{distChannel}|{division}";
        var customer = string.IsNullOrWhiteSpace(draft.Customer) ? "10100001" : draft.Customer.Trim();

        var salesAreaChoices = (draft.SalesAreaChoices ?? Array.Empty<ConfirmCreateChoice>())
            .Select(c => new { title = c.Title, value = c.Value })
            .ToList();
        if (salesAreaChoices.Count == 0)
        {
            salesAreaChoices.Add(new
            {
                title = $"{salesOrg} / {distChannel} / {division}",
                value = salesAreaKey
            });
        }

        var customerChoices = (draft.CustomerChoices ?? Array.Empty<ConfirmCreateChoice>())
            .Select(c => new { title = c.Title, value = c.Value })
            .ToList();
        if (customerChoices.Count == 0)
        {
            var fallbackKey = $"{customer}|{salesOrg}|{distChannel}|{division}";
            customerChoices.Add(new { title = $"{customer} ({salesOrg}/{distChannel}/{division})", value = fallbackKey });
            customer = fallbackKey;
        }

        var materialChoices = (draft.MaterialChoices ?? Array.Empty<ConfirmCreateChoice>())
            .Select(c => new { title = c.Title, value = c.Value })
            .ToList();

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-create.json",
            new
            {
                customer,
                salesOrg,
                distChannel,
                division,
                salesArea = salesAreaKey,
                salesAreaChoices,
                customerChoices,
                materialChoices,
                currency = string.IsNullOrWhiteSpace(draft.Currency) ? "USD" : draft.Currency,
                plant = string.IsNullOrWhiteSpace(draft.Plant) ? "1010" : draft.Plant,
                unit = string.IsNullOrWhiteSpace(draft.Unit) ? "PC" : draft.Unit,
                material1 = SlotMaterial(0),
                qty1 = SlotQty(0),
                material2 = SlotMaterial(1),
                qty2 = SlotQty(1),
                material3 = SlotMaterial(2),
                qty3 = SlotQty(2),
                material4 = SlotMaterial(3),
                qty4 = SlotQty(3),
                material5 = SlotMaterial(4),
                qty5 = SlotQty(4),
                material6 = SlotMaterial(5),
                qty6 = SlotQty(5),
                material7 = SlotMaterial(6),
                qty7 = SlotQty(6),
                material8 = SlotMaterial(7),
                qty8 = SlotQty(7)
            });
    }

    private static IReadOnlyList<ConfirmCreateOrderLine> NormalizeCreateLines(
        IReadOnlyList<ConfirmCreateOrderLine>? lines)
    {
        if (lines is null || lines.Count == 0)
            return new[] { new ConfirmCreateOrderLine("TG11", 1m) };

        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Material))
            .Take(CreateOrderFunction.MaxLineSlots)
            .Select(l => new ConfirmCreateOrderLine(
                l.Material.Trim().ToUpperInvariant(),
                l.Qty < 1 ? 1m : l.Qty,
                l.Plant,
                l.Unit))
            .ToList();
    }

    public static Attachment BuildConfirmUpdateReferenceCard(
        string salesOrderNumber,
        string currentReference,
        string newReference) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-update-reference.json",
            new
            {
                salesOrderNumber,
                currentReference = string.IsNullOrWhiteSpace(currentReference) ? "—" : currentReference,
                newReference = newReference ?? string.Empty
            });

    public static Attachment BuildConfirmEditOrderCard(
        string salesOrderNumber,
        string currentReference,
        string newReference,
        string currentReqDate,
        string newReqDate,
        string lineOp,
        string itemNumber,
        string material,
        decimal qty,
        string plant,
        string unit,
        string linesSummary) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-edit-order.json",
            new
            {
                salesOrderNumber,
                currentReference = string.IsNullOrWhiteSpace(currentReference) ? "—" : currentReference,
                newReference = newReference ?? string.Empty,
                currentReqDate = string.IsNullOrWhiteSpace(currentReqDate) ? "—" : currentReqDate,
                newReqDate = newReqDate ?? string.Empty,
                lineOp = string.IsNullOrWhiteSpace(lineOp) ? "none" : lineOp,
                itemNumber = itemNumber ?? string.Empty,
                material = material ?? string.Empty,
                qty,
                plant = plant ?? "1010",
                unit = unit ?? "PC",
                linesSummary = string.IsNullOrWhiteSpace(linesSummary) ? "—" : linesSummary
            });

    public static Attachment BuildConfirmReleaseCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-release.json", new { salesOrderNumber });

    public static Attachment BuildConfirmRequestReleaseCard(
        string salesOrderNumber,
        string? comment = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-request-release.json",
            new
            {
                salesOrderNumber,
                comment = comment ?? string.Empty
            });

    public static Attachment BuildConfirmApproveCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-approve.json", new { salesOrderNumber });

    public static Attachment BuildConfirmRejectApprovalCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-reject-approval.json", new { salesOrderNumber });

    public static Attachment BuildListDelegationsCard(IReadOnlyList<DelegationItem> delegations) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("list-delegations.json", new { delegations });

    public static Attachment BuildConfirmRevokeDelegationCard(string delegateUser, string? delegationId) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-revoke-delegation.json",
            new
            {
                delegateUser,
                delegationId = delegationId ?? string.Empty
            });

    public static Attachment BuildConfirmDelegateApprovalCard(
        string delegateUser,
        string validFromRaw,
        string validToRaw,
        string validFrom,
        string validTo,
        string reason,
        string maxAmountRaw,
        string maxAmount,
        string currency,
        IEnumerable<object>? managerChoices = null)
    {
        var choices = managerChoices ?? new object[]
        {
            new { title = "DEV-031 (Manager)", value = "DEV-031" },
            new { title = "DEV-025 (Manager)", value = "DEV-025" }
        };

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-delegate.json",
            new
            {
                delegateUser,
                validFromRaw,
                validToRaw,
                validFrom,
                validTo,
                reason,
                maxAmountRaw,
                maxAmount,
                currency,
                managerChoices = choices
            });
    }
    public static Attachment BuildConfirmForceCancelCard(
        string salesOrderNumber,
        string? reason = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-force-cancel.json",
            new
            {
                salesOrderNumber,
                reason = reason ?? string.Empty
            });

    public static Attachment BuildConfirmForceReleaseCard(
        string salesOrderNumber,
        string? reason = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-force-release.json",
            new
            {
                salesOrderNumber,
                reason = reason ?? string.Empty
            });

    public static Attachment BuildPendingApprovalsCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("pending-approvals.json", data);

    public static Attachment BuildPendingApprovalsCard(
        IReadOnlyList<OrderApprovalRequest> approvals,
        string? search = null,
        string? requester = null)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        var normalizedRequester = requester?.Trim() ?? string.Empty;

        var filtered = approvals
            .Where(approval =>
                (string.IsNullOrEmpty(normalizedSearch) ||
                 approval.SoNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                 approval.RequestedBySapUser.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                 (approval.Comment?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false)) &&
                (string.IsNullOrEmpty(normalizedRequester) ||
                 string.Equals(approval.RequestedBySapUser, normalizedRequester, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var data = new
        {
            count = filtered.Count,
            search = normalizedSearch,
            selectedRequester = normalizedRequester,
            requesterChoices = approvals
                .Select(approval => approval.RequestedBySapUser)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .Select(value => new { title = value, value })
                .ToList(),
            items = filtered.Select(approval => new
            {
                orderId = approval.SoNumber,
                requestedBy = approval.RequestedBySapUser,
                comment = approval.Comment ?? string.Empty,
                requestedAt = approval.RequestedAt.ToString("dd MMM yyyy HH:mm") + " UTC"
            }).ToList()
        };

        return BuildPendingApprovalsCard(data);
    }

    public static Attachment BuildAuditLogCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("audit-log.json", data);

    public static Attachment BuildBotUsersCard(IReadOnlyList<BotUserSummary> users)
    {
        var data = new
        {
            count = users.Count,
            users = users.Select(u => new
            {
                sapUserId = u.SapUserId,
                displayName = u.DisplayName,
                role = u.Role.ToString(),
                salesOrgLabel = string.IsNullOrWhiteSpace(u.SalesOrg) ? "no sales org" : u.SalesOrg
            }).ToList()
        };
        return CardTemplateFileLoader.BuildAdaptiveCardAttachment("bot-users.json", data);
    }

    public static Attachment BuildManageBotUserCard(BotUserSummary user)
    {
        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "manage-bot-user.json",
            new
            {
                sapUserId = user.SapUserId,
                displayName = user.DisplayName,
                role = user.Role.ToString(),
                salesOrg = user.SalesOrg ?? string.Empty
            });
    }

    public static Attachment BuildOverdueOrdersCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("overdue-orders.json", data);

    public static Attachment BuildKpiByCustomerCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-by-customer.json", data);

    public static Attachment BuildKpiByProductCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-by-product.json", data);

    public static Attachment BuildConfirmForwardCard(
        string salesOrderNumber,
        IEnumerable<(string Title, string Value)>? choices = null,
        string? senderName = null,
        string? selectedRecipient = null)
    {
        var recipientChoices = (choices ?? Array.Empty<(string Title, string Value)>())
            .Select(choice => new { title = choice.Title, value = choice.Value })
            .ToList();

        if (recipientChoices.Count == 0)
        {
            recipientChoices.Add(new { title = "No recipient available", value = string.Empty });
        }

        var selected = string.IsNullOrWhiteSpace(selectedRecipient)
            ? string.Empty
            : selectedRecipient.Trim();

        // Pre-select only when the suggestion matches a choice value (SAP User ID).
        if (!string.IsNullOrEmpty(selected)
            && !recipientChoices.Any(c =>
                string.Equals(c.value, selected, StringComparison.OrdinalIgnoreCase)))
        {
            selected = string.Empty;
        }

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-forward.json",
            new
            {
                salesOrderNumber,
                senderName = senderName ?? "Unknown user",
                selectedRecipient = selected,
                recipientChoices
            });
    }

    public static Attachment BuildKpiSummaryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-summary.json", data);

    public static Attachment BuildKpiRevenueCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-revenue.json", data);

    public static Attachment BuildKpiDeliveryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-delivery.json", data);

    public static Attachment BuildSalesOrderDetailCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("sales-order-detail.json", data);

    public static Attachment BuildSalesOrderDetailCard(
        SalesOrder order,
        UserRole? role = null,
        bool hasPendingApproval = false,
        string? pendingRequestedBySapUser = null,
        string? currentSapUser = null,
        string? pendingComment = null,
        OrderApprovalRequest? approval = null)
    {
        var isEmployee = role is null or UserRole.Employee;
        var isApprover = role is UserRole.Manager or UserRole.Admin;
        var canMutateLifecycle = !SalesOrderWorkflow.BlocksReleaseRejectForward(order.Status);
        var canReject = !SalesOrderWorkflow.BlocksReject(order.Status);
        var isOwner = SalesOrderWorkflow.IsCurrentOwner(order.OwnerSapUser, currentSapUser);
        var materialOk = !order.HasInvalidMaterial;
        var items = order.Items ?? Array.Empty<SalesOrderItem>();
        var pendingBy = string.IsNullOrWhiteSpace(pendingRequestedBySapUser)
            ? "a teammate"
            : pendingRequestedBySapUser.Trim();
        var owner = order.OwnerSapUser?.Trim();
        var hasOwner = !string.IsNullOrWhiteSpace(owner);
        var showActivePending = hasPendingApproval
            && SalesOrderWorkflow.ShowsPendingApprovalBanner(order.Status);
        var noteText = string.IsNullOrWhiteSpace(pendingComment)
            ? "N/A"
            : pendingComment.Trim();

        var orderLooksReleased = order.Status is SalesOrderStatus.Open
            or SalesOrderStatus.PartiallyDelivered
            or SalesOrderStatus.Delivered
            or SalesOrderStatus.Invoiced;
        // Latest decision already Approved (no pending) → treat as post-release lifecycle.
        var releaseApproved = approval?.Status == ApprovalStatus.Approved && !showActivePending;
        var showReleasedUx = releaseApproved && orderLooksReleased;

        // After approve: view-only for release/reject/forward (owner must not re-request by habit).
        var canMutateWhilePending = canMutateLifecycle
            && !hasPendingApproval
            && !releaseApproved
            && isOwner
            && materialOk;
        var canRejectWhilePending = canReject
            && !hasPendingApproval
            && !releaseApproved
            && isOwner
            && materialOk;

        var journey = ApprovalJourney.Build(
            approval,
            orderLooksReleased: showReleasedUx);
        var showJourney = journey.Count > 0 ? "true" : "false";

        var (statusLabel, statusColor, showHint, hint) = ResolveStatusPresentation(
            order.Status,
            showActivePending,
            showReleasedUx,
            releaseApproved && !orderLooksReleased);

        return BuildSalesOrderDetailCard(new
        {
            salesOrderNumber = order.SoNumber,
            customerDisplay = $"{DisplayOrNa(order.CustomerName)} ({DisplayOrNa(order.CustomerId)})",
            customerReference = DisplayOrNa(order.CustomerReference),
            salesOrgDivision = $"{DisplayOrNa(order.SalesOrg)} / {DisplayOrNa(order.Division)}",
            documentDate = order.OrderDate.ToString("dd MMM yyyy"),
            requestedDeliveryDate = order.RequestedDeliveryDate?.ToString("dd MMM yyyy") ?? "N/A",
            netAmount = $"{order.NetValue:N0}",
            currency = order.Currency,
            approvalStatus = statusLabel,
            statusColor,
            showApprovalHint = showHint ? "true" : "false",
            approvalHint = hint,
            hasItems = items.Count > 0 ? "true" : "false",
            showInvalidMaterial = order.HasInvalidMaterial ? "true" : "false",
            showOwnedBy = hasOwner ? "true" : "false",
            ownedBySapUser = hasOwner ? owner! : string.Empty,
            ownedByMessage = !hasOwner
                ? string.Empty
                : isOwner
                    ? "You currently own this order."
                    : "You can view this order, but Request release / Reject / Forward are limited to the owner.",
            showPendingEmployee = showActivePending && isEmployee ? "true" : "false",
            pendingEmployeeMessage = showActivePending && isEmployee
                ? $"Release requested by {pendingBy}. Waiting for a manager to approve — you can't change this order until then."
                : string.Empty,
            showPendingManager = showActivePending && isApprover ? "true" : "false",
            pendingManagerSubmittedBy = showActivePending && isApprover
                ? $"Submitted by {pendingBy}."
                : string.Empty,
            pendingManagerNote = showActivePending && isApprover
                ? $"Note for manager: {noteText}"
                : string.Empty,
            showReleasedBanner = showReleasedUx ? "true" : "false",
            releasedBannerTitle = showReleasedUx ? "Đơn đã duyệt — chờ vận chuyển" : string.Empty,
            releasedBannerMessage = string.Empty,
            showApprovalJourney = showJourney,
            journeySteps = journey.Select(s => new { title = s.Title, detail = s.Detail }).ToList(),
            showRequestRelease = isEmployee && canMutateWhilePending ? "true" : "false",
            showApprove = isApprover && canMutateLifecycle && showActivePending && materialOk ? "true" : "false",
            // Manager/Admin: cancel any cancellable SO (including while pending release).
            showCancel = isApprover && canReject && materialOk ? "true" : "false",
            showUpdateReference = canMutateWhilePending ? "true" : "false",
            // Owner (not pending) or Manager/Admin may open full edit.
            showEditOrder = canReject && materialOk
                && ((isOwner && !hasPendingApproval && !releaseApproved) || isApprover)
                ? "true" : "false",
            showReject = canRejectWhilePending ? "true" : "false",
            showForward = canMutateWhilePending ? "true" : "false",
            items = items.Select(item =>
            {
                var material = string.IsNullOrWhiteSpace(item.Material) ? "N/A" : item.Material.Trim();
                var description = string.IsNullOrWhiteSpace(item.Description) ? material : item.Description.Trim();
                var itemNumber = TrimItemNumber(item.ItemNumber);
                var unit = string.IsNullOrWhiteSpace(item.Unit) ? "EA" : item.Unit;
                var unitPrice = item.Quantity > 0
                    ? item.NetValue / item.Quantity
                    : item.NetValue;

                return new
                {
                    description,
                    itemCodeLabel = $"{itemNumber} · {material}",
                    quantity = item.Quantity.ToString("0"),
                    unit,
                    unitPriceLabel = $"{unitPrice:N0}/{unit}",
                    netValue = $"{item.NetValue:N0}",
                    currency = order.Currency
                };
            }).ToList()
        });
    }

    /// <summary>
    /// Header status + hint: keep domain Status, but make post-approve Open read as released.
    /// </summary>
    internal static (string Label, string Color, bool ShowHint, string Hint) ResolveStatusPresentation(
        SalesOrderStatus status,
        bool showActivePending,
        bool showReleasedUx,
        bool approvedButStillBlocked)
    {
        if (showActivePending)
            return (status.ToString(), StatusToColor(status), true, "Approval: Waiting");

        if (showReleasedUx)
        {
            var label = status == SalesOrderStatus.Open
                ? "Open (Released)"
                : $"{status} (Released)";
            return (label, "Good", true, "Đã duyệt");
        }

        if (approvedButStillBlocked)
            return (status.ToString(), StatusToColor(status), true, "Approved — SAP block may remain");

        return (status.ToString(), StatusToColor(status), false, string.Empty);
    }

    private static string TrimItemNumber(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber))
            return itemNumber;

        var trimmed = itemNumber.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }

    public static Attachment? BuildKpiCardForRequest(string message, IReadOnlyList<SalesOrder> orders, string? chartUrl)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var lowerMessage = message.ToLowerInvariant();
        var currency = orders.FirstOrDefault()?.Currency ?? "USD";
        var totalRevenue = orders.Sum(o => o.NetValue);
        var targetRevenue = totalRevenue + Math.Max(10000m, totalRevenue * 0.1m);

        if (lowerMessage.Contains("delivery"))
        {
            var deliveredCount = orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced);
            var delayedCount = orders.Count(o => o.Status is SalesOrderStatus.Blocked or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Open);
            var onTimeRate = orders.Count == 0 ? 0 : Math.Round((double)deliveredCount / orders.Count * 100, 0);

            return BuildKpiDeliveryCard(new
            {
                onTimeRate = $"{onTimeRate}%",
                delayedCount = delayedCount.ToString(),
                completedToday = deliveredCount.ToString(),
                deliveryProgress = (int)onTimeRate,
                chartUrl = chartUrl ?? string.Empty
            });
        }

        if (lowerMessage.Contains("revenue"))
        {
            return BuildKpiRevenueCard(new
            {
                period = "Current results",
                totalRevenue = $"{totalRevenue:N0} {currency}",
                growthRate = orders.Count > 5 ? "+12%" : "+8%",
                targetRevenue = $"{targetRevenue:N0} {currency}",
                chartUrl = chartUrl ?? string.Empty
            });
        }

        if (lowerMessage.Contains("kpi") || lowerMessage.Contains("summary"))
        {
            return BuildKpiSummaryCard(new
            {
                period = "Current results",
                revenueValue = $"{totalRevenue:N0} {currency}",
                orderCount = orders.Count,
                openOrders = orders.Count(o => o.Status == SalesOrderStatus.Open),
                deliveredOrders = orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced),
                overdueOrders = orders.Count(o => o.Status == SalesOrderStatus.Blocked),
                fulfillmentRate = orders.Count == 0 ? "0%" : $"{Math.Round((double)orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced) / orders.Count * 100, 1):0.0}%",
                chartUrl = chartUrl ?? string.Empty
            });
        }

        return null;
    }

    public static bool TryBuildWorkflowSuccessCard(JsonElement payload, string? functionName, out Attachment? card)
    {
        card = null;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!payload.TryGetProperty("order_id", out var orderIdElement) || orderIdElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(orderIdElement.GetString()))
        {
            return false;
        }

        var action = payload.TryGetProperty("action", out var actionElement) && actionElement.ValueKind == JsonValueKind.String
            ? actionElement.GetString()
            : functionName;

        string? detail = null;
        if (string.Equals(action, "Forwarded", StringComparison.OrdinalIgnoreCase)
            && payload.TryGetProperty("forward_to_user", out var forwardElement)
            && forwardElement.ValueKind == JsonValueKind.String)
        {
            detail = forwardElement.GetString();
        }

        card = BuildSuccessCard(orderIdElement.GetString()!, action ?? "Completed", detail);
        return true;
    }

    public static Attachment BuildSoSummaryCard(
        IReadOnlyList<SalesOrder> orders,
        string? title = null,
        IReadOnlyDictionary<string, OrderApprovalRequest?>? latestApprovalsBySo = null)
    {
        var data = new
        {
            title = string.IsNullOrWhiteSpace(title) ? "Sales orders" : title.Trim(),
            count = orders.Count,
            orders = orders.Select(o =>
            {
                OrderApprovalRequest? approval = null;
                latestApprovalsBySo?.TryGetValue(o.SoNumber, out approval);

                var showActivePending = approval?.Status == ApprovalStatus.Pending
                    && SalesOrderWorkflow.ShowsPendingApprovalBanner(o.Status);
                var orderLooksReleased = o.Status is SalesOrderStatus.Open
                    or SalesOrderStatus.PartiallyDelivered
                    or SalesOrderStatus.Delivered
                    or SalesOrderStatus.Invoiced;
                var releaseApproved = approval?.Status == ApprovalStatus.Approved && !showActivePending;
                var showReleasedUx = releaseApproved && orderLooksReleased;

                var (statusLabel, statusColor, showHint, hint) = ResolveStatusPresentation(
                    o.Status,
                    showActivePending,
                    showReleasedUx,
                    releaseApproved && !orderLooksReleased);

                return new
                {
                    soNumber = o.SoNumber,
                    customerName = string.IsNullOrWhiteSpace(o.CustomerName) ? "N/A" : o.CustomerName,
                    orderDate = o.OrderDate.ToString("dd MMM yyyy"),
                    formattedValue = $"{o.NetValue:N0} {o.Currency}",
                    status = statusLabel,
                    statusColor,
                    showStatusHint = showHint ? "true" : "false",
                    statusHint = hint,
                    salesOrg = o.SalesOrg
                };
            }).ToList()
        };

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment("so-summary.json", data);
    }

    private static string StatusToColor(SalesOrderStatus s) => s switch
    {
        SalesOrderStatus.Blocked or SalesOrderStatus.Cancelled => "Attention",
        SalesOrderStatus.Open or SalesOrderStatus.PartiallyDelivered => "Warning",
        SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced => "Good",
        _ => "Default"
    };

    private static string DisplayOrNa(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "N/A" : value;

    private static (string Headline, string Message, string StatusLabel, bool ShowPendingLink) DescribeSuccess(
        string status,
        string? detail = null) =>
        status switch
        {
            "ReleaseRequested" => (
                "Release requested",
                "Your request was submitted. A Manager in your sales organization must approve before SAP releases the order.",
                "Waiting for manager approval",
                false),
            "Approved" => (
                "Order approved",
                "The release request was approved and the sales order was released in SAP.",
                "Approved & released",
                true),
            "Released" => (
                "Order released",
                "The sales order was released successfully in SAP.",
                "Released",
                false),
            "ApprovalRejected" => (
                "Approval rejected",
                "The release request was declined. The sales order was not released.",
                "Approval rejected",
                true),
            "Rejected" => (
                "Order rejected",
                "All line items were rejected in SAP. The sales order is cancelled and can no longer be released, rejected again, or forwarded.",
                "Cancelled",
                false),
            "Forwarded" => (
                "Order forwarded",
                string.IsNullOrWhiteSpace(detail)
                    ? "Ownership was transferred. You no longer own this order."
                    : $"Ownership transferred to {detail.Trim()}. You no longer own this order.",
                "Forwarded",
                false),
            "UserAccessUpdated" => (
                "User access updated",
                string.IsNullOrWhiteSpace(detail)
                    ? "Bot role / sales org was updated for this SAP user."
                    : $"Access is now {detail.Trim()}. Changes apply on the next command for that user.",
                "Bot RBAC updated",
                false),
            "ForceCancelled" => (
                "Force cancel completed",
                string.IsNullOrWhiteSpace(detail)
                    ? "Admin force-cancelled the sales order in SAP."
                    : $"Admin force-cancelled the sales order. Reason: {detail.Trim()}",
                "Force cancelled",
                false),
            "ForceReleased" => (
                "Force release completed",
                string.IsNullOrWhiteSpace(detail)
                    ? "Admin force-released the sales order in SAP."
                    : $"Admin force-released the sales order. Reason: {detail.Trim()}",
                "Force released",
                false),
            _ => (
                "Action completed",
                "The requested action finished successfully.",
                status,
                false)
        };
}
