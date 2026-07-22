using AISO.SapIntegration;
using Xunit;

namespace AISO.UnitTests;

public class ODataQueryBuilderTests
{
    [Fact]
    public void Build_WithNoOptions_AppendsFormatJson()
    {
        var url = new ODataQueryBuilder("SalesOrder").Build();

        Assert.Equal("SalesOrder?$format=json", url);
    }

    [Fact]
    public void Build_CustomParamComesBeforeFilterAndFormat()
    {
        var url = new ODataQueryBuilder("SalesOrder")
            .AddCustomParam("sap-client", "324")
            .Build();

        Assert.Equal("SalesOrder?sap-client=324&$format=json", url);
    }

    [Fact]
    public void Build_WithTop_AddsTopParam()
    {
        var url = new ODataQueryBuilder("SalesOrder")
            .Top(25)
            .Build();

        Assert.Contains("$top=25", url);
    }

    [Fact]
    public void Filter_WithEmptyValue_IsIgnored()
    {
        var url = new ODataQueryBuilder("SalesOrder")
            .Filter("Customer", "eq", "")
            .Build();

        Assert.DoesNotContain("$filter", url);
    }

    [Fact]
    public void Filter_WithValue_IsUrlEscapedAndQuoted()
    {
        var url = new ODataQueryBuilder("SalesOrder")
            .Filter("Customer", "eq", "1000")
            .Build();

        // "Customer eq '1000'" URL-escaped -> spaces become %20, quotes %27
        Assert.Contains("$filter=Customer%20eq%20%271000%27", url);
    }

    [Fact]
    public void Filter_MultipleFilters_AreJoinedWithAnd()
    {
        var url = new ODataQueryBuilder("SalesOrder")
            .Filter("Customer", "eq", "1000")
            .Filter("SalesOrg", "eq", "UE00")
            .Build();

        // The literal " and " keyword must be present (escaped as %20and%20).
        Assert.Contains("%20and%20", url);
    }

    [Fact]
    public void Skip_WithZero_IsIgnored()
    {
        var url = new ODataQueryBuilder("SalesOrder")
            .Skip(0)
            .Build();

        Assert.DoesNotContain("$skip", url);
    }
}
