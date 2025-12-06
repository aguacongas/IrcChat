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
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];

builder.Services.AddHttpClient("IrcChat.Api")
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl!))
    .AddHttpMessageHandler<CredentialsHandler>();

builder.Services.AddScoped(sp =>
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("IrcChat.Api"))
    .AddScoped<CredentialsHandler>()
    .AddScoped<IChatService, ChatService>()
    .AddScoped<ILocalStorageService, LocalStorageService>()
    .AddScoped<IUnifiedAuthService, UnifiedAuthService>()
    .AddScoped<IOAuthClientService, OAuthClientService>()
    .AddScoped<IPrivateMessageService, PrivateMessageService>()
    .AddScoped<IDeviceDetectorService, DeviceDetectorService>()
    .AddScoped<IIgnoredUsersService, IgnoredUsersService>()
    .AddScoped<IActiveChannelsService, ActiveChannelsService>()
    .AddScoped<IChannelUnreadCountService, ChannelUnreadCountService>()
    .AddScoped<INotificationSoundService, NotificationSoundService>()
    .AddSingleton<IRequestAuthenticationService, RequestAuthenticationService>();

await builder.Build().RunAsync();