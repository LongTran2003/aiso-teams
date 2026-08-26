using System;
using AdaptiveCards.Templating;
class Program {
    static void Main() {
        var templateJson = @"{
            ""type"": ""AdaptiveCard"",
            ""body"": [
                {
                    ""type"": ""TextBlock"",
                    ""text"": ""Hello"",
                    ""isVisible"": ""${hasShipToParty == 'true'}""
                }
            ]
        }";
        var template = new AdaptiveCardTemplate(templateJson);
        var data = new { hasShipToParty = "false" };
        Console.WriteLine(template.Expand(data));
    }
}
