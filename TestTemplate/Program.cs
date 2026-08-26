using System;
using System.Collections.Generic;
using AdaptiveCards.Templating;

public record ConfirmCreateOrderLine(string Material, decimal Qty, string Plant = """", string Unit = """");

class Program {
    static void Main() {
        var templateJson = ""{ \""type\"": \""AdaptiveCard\"", \""body\"": [ { \""type\"": \""FactSet\"", \""facts\"": [ { \""$data\"": \""${lineItems}\"", \""title\"": \""${Material}\"", \""value\"": \""Qty ${Qty}  Plant: ${Plant}  Unit: ${Unit}\"" } ] } ] }"";
        var template = new AdaptiveCardTemplate(templateJson);
        var lineItems = new List<ConfirmCreateOrderLine> {
            new ConfirmCreateOrderLine(""123"", 5, """", """")
        };
        var data = new { lineItems = lineItems };
        Console.WriteLine(template.Expand(data));
    }
}
