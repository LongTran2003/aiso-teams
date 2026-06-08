using Serilog.Context;

namespace AISO.Api.Middleware;

/// <summary>
/// Reads (or generates) a correlation ID for every incoming HTTP request,
/// pushes it into Serilog's <see cref="LogContext"/> so every downstream log
/// emitted on this request includes it, and echoes it back in the response
/// header so clients can correlate their request with our logs.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
