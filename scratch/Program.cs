using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var httpClient = new HttpClient();
        var username = "DEV-249";
        var password = "123@123Aa";
        var baseUrl = "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001/";
        var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
        httpClient.DefaultRequestHeaders.Authorization = authHeader;

        var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + "ValidMaterialPlant?sap-client=324&$top=5");
        var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Result: {(int)response.StatusCode}");
        Console.WriteLine(body);
    }
}