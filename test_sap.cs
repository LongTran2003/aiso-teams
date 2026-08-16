using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

class Program
{
    static void Main()
    {
        string raw = ""M"";
        Console.WriteLine(ParseSapErrorMessage(raw, 500));
    }
    
    // (copy ParseSapErrorMessage here)
    private static string ParseSapErrorMessage(string errorBody, int statusCode)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(errorBody);
            if (!doc.RootElement.TryGetProperty(""error"", out var errorObj))
            {
                return TruncateRaw(errorBody, statusCode);
            }
            var candidates = new List<string>();
            CollectMessage(errorObj, ""message"", candidates);
            if (errorObj.TryGetProperty(""details"", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                    CollectMessage(detail, ""message"", candidates);
            }
            if (errorObj.TryGetProperty(""innererror"", out var inner) && inner.TryGetProperty(""errordetails"", out var innerDetails) && innerDetails.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in innerDetails.EnumerateArray())
                    CollectMessage(detail, ""message"", candidates);
            }
            var code = errorObj.TryGetProperty(""code"", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var message = candidates.Where(m => !string.IsNullOrWhiteSpace(m)).OrderByDescending(m => m.Length).FirstOrDefault();
            if (string.Equals(code, ""RAISE_SHORTDUMP"", StringComparison.OrdinalIgnoreCase))
            {
                return ""ABAP Short Dump... "";
            }
            if (!string.IsNullOrWhiteSpace(message))
                return message;
            return $""SAP error {code}: {statusCode}"";
        }
        catch
        {
        }
        return TruncateRaw(errorBody, statusCode);
    }
    private static void CollectMessage(JsonElement parent, string propertyName, List<string> sink)
    {
        if (!parent.TryGetProperty(propertyName, out var messageEl)) return;
        if (messageEl.ValueKind == JsonValueKind.String)
        {
            var s = messageEl.GetString();
            if (!string.IsNullOrWhiteSpace(s)) sink.Add(s.Trim());
            return;
        }
        if (messageEl.ValueKind == JsonValueKind.Object && messageEl.TryGetProperty(""value"", out var valueEl) && valueEl.ValueKind == JsonValueKind.String)
        {
            var s = valueEl.GetString();
            if (!string.IsNullOrWhiteSpace(s)) sink.Add(s.Trim());
        }
    }
    private static string TruncateRaw(string raw, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(raw)) return $""HTTP {statusCode}"";
        return raw.Length > 200 ? raw.Substring(0, 200) + ""..."" : raw;
    }
}
