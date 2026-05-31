using AISO.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;

var builder = WebApplication.CreateBuilder(args);

// HTTP client + Controllers
builder.Services.AddHttpClient();
builder.Services.AddControllers().AddNewtonsoftJson();

// Bot Framework authentication
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Use custom adapter để tránh constructor ambiguity
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Register bot
builder.Services.AddTransient<IBot, TeamsBot>();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();