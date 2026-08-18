using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;

var handler = new HttpClientHandler { UseCookies = true };
var client = new HttpClient(handler);
var creds = Convert.ToBase64String(Encoding.ASCII.GetBytes("DEV-249:123@123Aa"));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
client.DefaultRequestHeaders.Add("X-CSRF-Token", "Fetch");

var getUrl = "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001/?sap-client=324";
var response = await client.GetAsync(getUrl);
var token = response.Headers.GetValues("X-CSRF-Token").GetEnumerator();
token.MoveNext();
var csrf = token.Current;

client.DefaultRequestHeaders.Remove("X-CSRF-Token");
client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);

async Task TestPost(string testName, object payload, bool isUnbound = false) {
    Console.WriteLine($"\n--- Running Test: {testName} ---");
    var postUrl = isUnbound 
        ? "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.delegateApproval?sap-client=324&$format=json"
        : "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001/UserRole/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.delegateApproval?sap-client=324&$format=json";

    var json = JsonSerializer.Serialize(payload);
    Console.WriteLine($"Payload: {json}");
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var postResponse = await client.PostAsync(postUrl, content);
    var result = await postResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"STATUS: {postResponse.StatusCode}");
    Console.WriteLine($"BODY: {result}");
}

await TestPost("Normal MaxAmount string", new {
    REQUESTING_TEAMS_USER = "DEV-249",
    DELEGATE_USER = "DEV-031",
    SALES_ORG = "",
    VALID_FROM = "2026-08-18",
    VALID_TO = "2026-08-20",
    REASON = "Test",
    MAX_AMOUNT = "500000"
});

await TestPost("Normal MaxAmount number", new {
    REQUESTING_TEAMS_USER = "DEV-249",
    DELEGATE_USER = "DEV-031",
    SALES_ORG = "",
    VALID_FROM = "2026-08-18",
    VALID_TO = "2026-08-20",
    REASON = "Test",
    MAX_AMOUNT = 500000.0m
});

await TestPost("CamelCase parameters", new {
    requestingTeamsUser = "DEV-249",
    delegateUser = "DEV-031",
    salesOrg = "",
    validFrom = "2026-08-18",
    validTo = "2026-08-20",
    reason = "Test",
    maxAmount = "500000"
});

await TestPost("DateTimeOffset ValidFrom/To", new {
    REQUESTING_TEAMS_USER = "DEV-249",
    DELEGATE_USER = "DEV-031",
    SALES_ORG = "",
    VALID_FROM = "2026-08-18T00:00:00Z",
    VALID_TO = "2026-08-20T00:00:00Z",
    REASON = "Test",
    MAX_AMOUNT = "500000"
});

await TestPost("Unbound Action Root URL", new {
    REQUESTING_TEAMS_USER = "DEV-249",
    DELEGATE_USER = "DEV-031",
    SALES_ORG = "",
    VALID_FROM = "2026-08-18",
    VALID_TO = "2026-08-20",
    REASON = "Test",
    MAX_AMOUNT = "500000"
}, true);
