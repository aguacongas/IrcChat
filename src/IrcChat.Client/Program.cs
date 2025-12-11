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
    .AddSingleton<IRequestAuthenticationService, RequestAuthenticationService>()
    .AddSingleton<IEmojiService>(sp =>
    {
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        http.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        return new EmojiService(http, sp.GetRequiredService<ILogger<EmojiService>>());
    });

var host = builder.Build();

// Initialisation asynchrone NON-BLOQUANTE du service emoji
// Le service se charge en arrière-plan pendant que l'app démarre
_ = Task.Run(async () =>
{
    try
    {
        var emojiService = host.Services.GetRequiredService<IEmojiService>();
        await emojiService.InitializeAsync();
    }
    catch (Exception ex)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erreur lors du chargement des emojis");
    }
});

await host.RunAsync();