using AISO.AiOrchestration;
using AISO.AiOrchestration.Functions;
using AISO.AiOrchestration.Stub;
using AISO.Bot;
using AISO.Persistence;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;

var builder = WebApplication.CreateBuilder(args);

// --- ASP.NET Core basics ---
builder.Services.AddHttpClient();
builder.Services.AddControllers().AddNewtonsoftJson();

// --- Bot Framework authentication + adapter ---
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
builder.Services.AddTransient<IBot, TeamsBot>();

// --- Persistence (EF Core + PostgreSQL) ---
builder.Services.AddPersistence(builder.Configuration);

// --- SAP Integration ---
// Sprint 2: mock client with seeded Global Bike data.
// Sprint 3: replaced by a real OData client calling SAP via Cloud Connector.
builder.Services.AddSingleton<ISapClient, MockSapClient>();

// --- AI Orchestration ---
// Register every IFunction implementation; FunctionRegistry collects them.
builder.Services.AddSingleton<IFunction, GetSalesOrdersFunction>();
builder.Services.AddSingleton<IFunctionRegistry, FunctionRegistry>();

// Sprint 2: keyword-stub dispatcher.
// Sprint 3: replaced by Azure OpenAI function-calling dispatcher (AI team).
builder.Services.AddSingleton<IFunctionDispatcher, KeywordFunctionDispatcher>();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
