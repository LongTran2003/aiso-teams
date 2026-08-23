using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace AISO.Api.Controllers;

[Route("api/messages")]
[ApiController]
public class BotController : ControllerBase
{
    private readonly IBotFrameworkHttpAdapter _adapter;
    private readonly IBot _bot;
    private readonly ILogger<BotController> _logger;

    public BotController(
        IBotFrameworkHttpAdapter adapter,
        IBot bot,
        ILogger<BotController> logger)
    {
        _adapter = adapter;
        _bot = bot;
        _logger = logger;
    }

    [HttpPost]
    public async Task PostAsync(CancellationToken ct)
    {
        try
        {
            await _adapter.ProcessAsync(Request, Response, _bot, ct);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Adapter pipeline cancellation: typically happens on the very first
            // message after a cold start (App Service waking up, JIT warming,
            // SAP TLS handshake). Subsequent messages succeed.
            // Log at Warning so App Insights surfaces it under "failures" but
            // doesn't page anyone — it's a known warm-up race.
            _logger.LogWarning(
                ex,
                "Bot adapter pipeline canceled (likely cold start). " +
                "ConversationId={ConversationId}, RemoteIp={RemoteIp}",
                HttpContext.Items["CorrelationId"],
                HttpContext.Connection.RemoteIpAddress);
        }
    }
}