import re

with open('backend/src/AISO.Bot/Cards/Builders/TeamsCardBuilder.cs', 'r', encoding='utf-8') as f:
    content = f.read()

new_method = """    public static Attachment BuildCreateOrderStep3Card(
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
            });"""

pattern = re.compile(r'    public static Attachment BuildCreateOrderStep3Card\([^)]+\)\s*=>\s*CardTemplateFileLoader\.BuildAdaptiveCardAttachment\([\s\S]*?\}\);', re.MULTILINE)
content = pattern.sub(new_method, content)

with open('backend/src/AISO.Bot/Cards/Builders/TeamsCardBuilder.cs', 'w', encoding='utf-8', newline='\n') as f:
    f.write(content)
