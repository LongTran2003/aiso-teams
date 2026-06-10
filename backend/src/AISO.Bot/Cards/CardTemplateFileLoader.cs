namespace AISO.Bot.Cards;

internal static class CardTemplateFileLoader
{
    public static string LoadFromFrontendCards(string fileName)
    {
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
            $"Could not find frontend card template '{fileName}' under any parent of '{AppContext.BaseDirectory}'.");
    }
}