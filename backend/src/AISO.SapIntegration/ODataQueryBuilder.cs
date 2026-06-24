using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AISO.SapIntegration;

/// <summary>
/// A simple fluent builder for OData queries.
/// </summary>
public class ODataQueryBuilder
{
    private readonly string _entitySet;
    private readonly List<string> _filters = new();
    private readonly List<string> _expands = new();
    private int? _top;
    private int? _skip;
    private readonly Dictionary<string, string> _customParams = new();

    public ODataQueryBuilder(string entitySet)
    {
        _entitySet = entitySet;
    }

    public ODataQueryBuilder Filter(string field, string op, string? value, bool isString = true)
    {
        if (string.IsNullOrWhiteSpace(value)) return this;

        var formattedValue = isString ? $"'{value}'" : value;
        _filters.Add($"{field} {op} {formattedValue}");
        return this;
    }

    public ODataQueryBuilder FilterRaw(string rawFilter)
    {
        if (!string.IsNullOrWhiteSpace(rawFilter))
        {
            _filters.Add(rawFilter);
        }
        return this;
    }

    public ODataQueryBuilder Top(int top)
    {
        _top = top;
        return this;
    }

    public ODataQueryBuilder Skip(int skip)
    {
        if (skip > 0)
        {
            _skip = skip;
        }
        return this;
    }

    public ODataQueryBuilder Expand(string expand)
    {
        if (!string.IsNullOrWhiteSpace(expand))
        {
            _expands.Add(expand);
        }
        return this;
    }

    public ODataQueryBuilder AddCustomParam(string key, string value)
    {
        _customParams[key] = value;
        return this;
    }

    public string Build()
    {
        var sb = new StringBuilder(_entitySet);
        var hasQuery = false;

        void AppendParam(string key, string value)
        {
            sb.Append(hasQuery ? "&" : "?");
            sb.Append($"{key}={value}");
            hasQuery = true;
        }

        // Apply custom params first (e.g., sap-client)
        foreach (var param in _customParams)
        {
            AppendParam(param.Key, param.Value);
        }

        if (_filters.Any())
        {
            AppendParam("$filter", Uri.EscapeDataString(string.Join(" and ", _filters)));
        }

        if (_expands.Any())
        {
            AppendParam("$expand", Uri.EscapeDataString(string.Join(",", _expands)));
        }

        if (_top.HasValue)
        {
            AppendParam("$top", _top.Value.ToString());
        }

        if (_skip.HasValue)
        {
            AppendParam("$skip", _skip.Value.ToString());
        }

        // Add format=json as standard
        AppendParam("$format", "json");

        return sb.ToString();
    }
}
