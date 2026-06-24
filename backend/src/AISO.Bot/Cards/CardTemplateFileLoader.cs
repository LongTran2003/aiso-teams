using System.Reflection;

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
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // 2) Fallback: walk up from AppContext.BaseDirectory looking for frontend/cards/
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var current = baseDirectory; current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "cards", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new FileNotFoundException(
            $"Could not find card template '{fileName}' as embedded resource '{resourceName}' or under any parent of '{AppContext.BaseDirectory}'.");
    }
}
