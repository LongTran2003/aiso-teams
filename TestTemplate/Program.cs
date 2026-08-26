using System;
using AdaptiveCards.Templating;
class Program {
    static void Main() {
        var templateJson = @"{
            ""type"": ""AdaptiveCard"",
            ""body"": [
                {
                    ""type"": ""TextBlock"",
                    ""text"": ""Plant: ${Plant}  Unit: ${Unit}""
                }
            ]
        }";
        var template = new AdaptiveCardTemplate(templateJson);
        var data = new { Plant = """", Unit = (string)null };
        Console.WriteLine(template.Expand(data));
    }
}
