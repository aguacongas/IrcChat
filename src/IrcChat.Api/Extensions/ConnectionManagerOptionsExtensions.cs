using IrcChat.Api.Services; // Assurez-vous que cela corresponde au namespace de ConnectionManagerOptions

namespace IrcChat.Api.Extensions; // Utilisez un namespace approprié

public static class ConnectionManagerOptionsExtensions
{
    /// <summary>
    /// Récupère l'ID de l'instance en utilisant la priorité suivante:
    /// 1. ConnectionManagerOptions.InstanceId
    /// 2. Variable d'environnement "HOSTNAME"
    /// 3. Environment.MachineName
    /// </summary>
    /// <param name="options">Les options de configuration.</param>
    /// <returns>L'ID de l'instance.</returns>
    public static string GetInstanceId(this ConnectionManagerOptions options)
    {
        return options.InstanceId 
            ?? Environment.GetEnvironmentVariable("HOSTNAME") 
            ?? Environment.MachineName;
    }
}