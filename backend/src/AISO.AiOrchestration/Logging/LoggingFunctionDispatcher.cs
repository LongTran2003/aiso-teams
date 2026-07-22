using System.Diagnostics;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Logging;

/// <summary>
/// Decorator over <see cref="IFunctionDispatcher"/> that emits structured logs
/// at dispatch start, dispatch completion, and on exception. The inner dispatcher
/// (keyword stub today, Azure OpenAI in Sprint 3) is wrapped transparently.
/// </summary>
public sealed class LoggingFunctionDispatcher : IFunctionDispatcher
{
    private readonly IFunctionDispatcher _inner;
    private readonly ILogger<LoggingFunctionDispatcher> _logger;

    public LoggingFunctionDispatcher(
        IFunctionDispatcher inner,
        ILogger<LoggingFunctionDispatcher> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(
        string userMessage,
        string requestingSapUser,
        UserRole role,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation(
            "Dispatcher received user message: {UserMessage}",
            userMessage);

        try
        {
            var result = await _inner.DispatchAsync(userMessage, requestingSapUser, role, ct);
            sw.Stop();

            if (result.Denied)
            {
                _logger.LogWarning(
                    "Dispatcher denied function {Function} by RBAC; duration={DurationMs}ms",
                    result.FunctionName, sw.ElapsedMilliseconds);
            }
            else if (result.Handled)
            {
                _logger.LogInformation(
                    "Dispatcher selected function {Function}; success={Success}; duration={DurationMs}ms",
                    result.FunctionName, result.Result?.Success, sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "Dispatcher could not handle input; reason={Reason}; duration={DurationMs}ms",
                    result.Reason, sw.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Dispatcher threw exception after {DurationMs}ms",
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}


