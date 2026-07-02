using AISO.AiOrchestration;
using AISO.AiOrchestration.Functions;
using AISO.AiOrchestration.Logging;
using AISO.AiOrchestration.Services;
using AISO.AiOrchestration.Stub;
using AISO.Api.Extensions;
using AISO.Api.Middleware;
using AISO.Bot;
using AISO.Persistence;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Serilog;
using Polly;

// --- Bootstrap Serilog before host is built so startup errors are captured ---
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replace the default logging provider with Serilog, reading config
    // from "Serilog" section in appsettings.
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    // --- ASP.NET Core basics ---
    builder.Services.AddHttpClient();
    builder.Services.AddControllers().AddNewtonsoftJson();

    var aiConnStr = builder.Configuration.GetConnectionString("ApplicationInsights");
    if (!string.IsNullOrEmpty(aiConnStr))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = aiConnStr;
        });
    }

    // --- Application Services Setup ---
    builder.Services
        .AddBotServices(builder.Configuration)
        .AddPersistence(builder.Configuration)
        .AddSapIntegration(builder.Configuration)
        .AddAiOrchestration(builder.Configuration)
        .AddCustomHealthChecks(builder.Configuration);

    var app = builder.Build();

    // --- HTTP pipeline ---
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    Log.Information("AISO-Teams Bot starting up");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "AISO-Teams Bot terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
