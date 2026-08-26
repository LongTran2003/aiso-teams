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
                customerKey,
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
                unit2 = "",
                unit3 = ""
            });
