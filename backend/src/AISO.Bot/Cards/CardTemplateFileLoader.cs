using System.Reflection;
using AdaptiveCards.Templating;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace AISO.Bot.Cards;

internal static class CardTemplateFileLoader
{
    /// <summary>
    /// Loads a card template JSON by name.
    /// Strategy:
    ///   1) Try embedded resource (works on Azure after publish)
    ///   2) Try filesystem under frontend/cards/ (works in local dev)
    /// </summary>
    public static string LoadFromFrontendCards(string fileName)
    {
        // 1) Try embedded resource first (reliable on Azure)
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"AISO.Bot.Cards.Templates.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // 2) Fallback: walk up from AppContext.BaseDirectory looking for frontend/cards/
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var current = baseDirectory; current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "cards", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate, System.Text.Encoding.UTF8);
            }
        }

        throw new FileNotFoundException(
            $"Could not find card template '{fileName}' as embedded resource '{resourceName}' or under any parent of '{AppContext.BaseDirectory}'.");
    }

    public static Attachment BuildAdaptiveCardAttachment(string fileName, object? data = null)
    {
        var templateJson = LoadFromFrontendCards(fileName);
        var template = new AdaptiveCardTemplate(templateJson);
        var cardJson = data is null ? template.Expand(new { }) : template.Expand(data);

        return new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(cardJson)
        };
    }

    /// <summary>
    /// Builds an Adaptive Card attachment from a raw object graph (no template file needed).
    /// Used for dynamically generated cards like the cascading Step 1.
    /// </summary>
    public static Attachment BuildAdaptiveCardFromObject(object cardData)
    {
        var cardJson = JsonConvert.SerializeObject(cardData, Formatting.None);
        return new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(cardJson)
        };
    }
}
