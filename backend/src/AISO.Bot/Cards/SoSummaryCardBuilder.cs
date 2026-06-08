using System.Reflection;
using AdaptiveCards.Templating;
using AISO.Domain.SalesOrders;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace AISO.Bot.Cards;

/// <summary>
/// Builds an Adaptive Card attachment from a list of Sales Orders using
/// the embedded JSON template (Cards/Templates/SoSummaryCard.json).
/// </summary>
public static class SoSummaryCardBuilder
{
    private const string TemplateResourceName = "AISO.Bot.Cards.Templates.SoSummaryCard.json";

    private static readonly string TemplateJson = LoadEmbedded(TemplateResourceName);

    public static Attachment Build(IReadOnlyList<SalesOrder> orders)
    {
        var data = new
        {
            count = orders.Count,
            orders = orders.Select(o => new
            {
                soNumber = o.SoNumber,
                customerName = o.CustomerName,
                orderDate = o.OrderDate.ToString("dd MMM yyyy"),
                formattedValue = $"{o.NetValue:N0} {o.Currency}",
                status = o.Status.ToString(),
                statusColor = StatusToColor(o.Status),
                salesOrg = o.SalesOrg
            }).ToList()
        };

        var template = new AdaptiveCardTemplate(TemplateJson);
        var cardJson = template.Expand(data);

        return new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(cardJson)
        };
    }

    private static string StatusToColor(SalesOrderStatus s) => s switch
    {
        SalesOrderStatus.Blocked => "Attention",
        SalesOrderStatus.Open or SalesOrderStatus.PartiallyDelivered => "Warning",
        SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced => "Good",
        _ => "Default"
    };

    private static string LoadEmbedded(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. " +
                $"Check that Build Action is 'Embedded Resource'. Available: " +
                string.Join(", ", asm.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
