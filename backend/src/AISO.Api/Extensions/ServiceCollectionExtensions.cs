using AISO.AiOrchestration;
using AISO.AiOrchestration.Functions;
using AISO.AiOrchestration.Logging;
using AISO.AiOrchestration.Services;
using AISO.AiOrchestration.Stub;
using AISO.Bot;
using AISO.Bot.Dialogs;
using AISO.Bot.Services;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Polly;

namespace AISO.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiOrchestration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register all AI Functions
        services.AddSingleton<IFunction, GetSalesOrdersFunction>();
        services.AddSingleton<IFunction, CheckOrderStatusFunction>();
        services.AddSingleton<IFunction, ReleaseOrderFunction>();
        services.AddSingleton<IFunction, RejectOrderFunction>();
        services.AddSingleton<IFunction, ForwardOrderFunction>();
        services.AddSingleton<IFunction, CreateOrderFunction>();
        services.AddSingleton<IFunction, UpdateOrderReferenceFunction>();

        // KPI functions
        services.AddSingleton<IFunction, GetKpiSummaryFunction>();
        services.AddSingleton<IFunction, GetKpiByCustomerFunction>();
        services.AddSingleton<IFunction, GetKpiByProductFunction>();
        services.AddSingleton<IFunction, GetOverdueOrdersFunction>();

        services.AddSingleton<IFunctionRegistry, FunctionRegistry>();

        // AI Service integration: register HTTP client for AI microservice.
        // Set AiService:UseKeywordFallback=true in config to bypass AI service
        // and use keyword matching (e.g. when AI service is not running).
        services.Configure<AiServiceOptions>(
            configuration.GetSection(AiServiceOptions.SectionName));

        var useKeywordFallback = configuration
            .GetValue<bool>("AiService:UseKeywordFallback", false);

        if (useKeywordFallback)
        {
            services.AddSingleton<IFunctionDispatcher, KeywordFunctionDispatcher>();
        }
        else
        {
            services.AddHttpClient<AiServiceClient>();
            services.AddSingleton<IFunctionDispatcher, AiServiceDispatcher>();
        }

        // Decorate IFunctionDispatcher with structured logging.
        services.Decorate<IFunctionDispatcher, LoggingFunctionDispatcher>();

        return services;
    }

    public static IServiceCollection AddBotServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
        services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

        // Create the storage we'll be using for User and Conversation state.
        services.AddSingleton<IStorage, MemoryStorage>();
        services.AddSingleton<UserState>();
        services.AddSingleton<ConversationState>();

        // Add Redis distributed cache
        var redisConnStr = configuration.GetConnectionString("Redis");
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnStr;
            options.InstanceName = "AisoBot_";
        });

        if (!string.IsNullOrEmpty(redisConnStr))
        {
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnStr));
        }

        // Register the SSO Dialog and User Mapping Service
        services.AddTransient<UserMappingService>();
        services.AddTransient<SsoDialog>();

        // Register the Bot
        services.AddTransient<IBot, TeamsBot>();

        return services;
    }

    public static IServiceCollection AddSapIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SapOptions>(configuration.GetSection(SapOptions.SectionName));

        // Register SapTokenManager
        services.AddHttpClient<ISapTokenManager, SapTokenManager>((sp, client) =>
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
        services.AddHttpClient<ISapClient, SapClient>((sp, client) =>
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

        return services;
    }

    public static IServiceCollection AddCustomHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: configuration.GetConnectionString("Postgres")!,
                name: "postgres",
                tags: new[] { "db", "ready" })
            .AddRedis(
                redisConnectionString: configuration.GetConnectionString("Redis")!,
                name: "redis",
                tags: new[] { "cache", "ready" });
        return services;
    }
}
