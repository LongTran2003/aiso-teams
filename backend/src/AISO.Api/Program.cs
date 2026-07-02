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

    // --- Bot Framework authentication + adapter ---
    builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
    builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

    // Create the storage we'll be using for User and Conversation state.
    builder.Services.AddSingleton<IStorage, MemoryStorage>();
    builder.Services.AddSingleton<UserState>();
    builder.Services.AddSingleton<ConversationState>();

    // Add Redis distributed cache
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "AisoBot_";
    });

    // Register the SSO Dialog and User Mapping Service
    builder.Services.AddTransient<AISO.Bot.Services.UserMappingService>();
    builder.Services.AddTransient<AISO.Bot.Dialogs.SsoDialog>();

    // Register the Bot
    builder.Services.AddTransient<IBot, TeamsBot>();

    // --- Persistence (EF Core + PostgreSQL) + Audit logger ---
    builder.Services.AddPersistence(builder.Configuration);

    // --- SAP Integration ---
    // Sprint 2: mock client with seeded Global Bike data.
    // Sprint 3: replaced by a real OData client calling SAP via Cloud Connector.
    builder.Services.Configure<SapOptions>(builder.Configuration.GetSection(SapOptions.SectionName));

    // Register SapTokenManager
    builder.Services.AddHttpClient<ISapTokenManager, SapTokenManager>((sp, client) =>
    {
        var sapOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SapOptions>>().Value;
        if (!string.IsNullOrEmpty(sapOptions.BaseUrl))
        {
            client.BaseAddress = new Uri(sapOptions.BaseUrl);
        }

        if (!string.IsNullOrEmpty(sapOptions.Username) && !string.IsNullOrEmpty(sapOptions.Password))
        {
            var authHeader = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{sapOptions.Username}:{sapOptions.Password}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        }

        client.Timeout = TimeSpan.FromSeconds(sapOptions.TimeoutSeconds > 0 ? sapOptions.TimeoutSeconds : 30);
    })
    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));

    // Register SapClient
    builder.Services.AddHttpClient<ISapClient, SapClient>((sp, client) =>
    {
        var sapOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SapOptions>>().Value;
        if (!string.IsNullOrEmpty(sapOptions.BaseUrl))
        {
            client.BaseAddress = new Uri(sapOptions.BaseUrl);
        }

        if (!string.IsNullOrEmpty(sapOptions.Username) && !string.IsNullOrEmpty(sapOptions.Password))
        {
            var authHeader = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{sapOptions.Username}:{sapOptions.Password}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        }

        client.Timeout = TimeSpan.FromSeconds(sapOptions.TimeoutSeconds > 0 ? sapOptions.TimeoutSeconds : 30);
    })
    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));

    // --- AI Orchestration ---
    builder.Services.AddAiOrchestration(builder.Configuration);

    // --- Health Checks ---
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            connectionString: builder.Configuration.GetConnectionString("Postgres")!,
            name: "postgres",
            tags: new[] { "db", "ready" })
        .AddRedis(
            redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
            name: "redis",
            tags: new[] { "cache", "ready" });

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
