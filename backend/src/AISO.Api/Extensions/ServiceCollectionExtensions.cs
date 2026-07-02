using AISO.AiOrchestration;
using AISO.AiOrchestration.Functions;
using AISO.AiOrchestration.Logging;
using AISO.AiOrchestration.Services;
using AISO.AiOrchestration.Stub;

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

        // Sprint 4 — KPI functions
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
}
