using IrcChat.Client;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configuration des paramètres API depuis appsettings.json
builder.Services.Configure<ApiSettings>(apiSessing => builder.Configuration.GetSection("Api").Bind(apiSessing));

// Récupérer l'URL de base depuis la configuration
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7000";

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

builder.Services.AddScoped<ChatService>()
    .AddScoped<AuthStateService>()
    .AddScoped<LocalStorageService>()
    .AddScoped<UnifiedAuthService>()
    .AddScoped<OAuthClientService>();

await builder.Build().RunAsync();
