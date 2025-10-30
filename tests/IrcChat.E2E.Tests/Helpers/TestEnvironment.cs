// tests/IrcChat.E2E.Tests/Helpers/TestEnvironment.cs
using System.Diagnostics;

namespace IrcChat.E2E.Tests.Helpers;

public class TestEnvironment : IDisposable
{
    private Process? _apiProcess;
    private Process? _clientProcess;

    public async Task<bool> StartAsync()
    {
        try
        {
            // Démarrer l'API
            _apiProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --project src/IrcChat.Api/IrcChat.Api.csproj",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            _apiProcess.Start();
            await Task.Delay(5000); // Attendre que l'API démarre

            // Démarrer le Client
            _clientProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --project src/IrcChat.Client/IrcChat.Client.csproj",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            _clientProcess.Start();
            await Task.Delay(5000); // Attendre que le client démarre

            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    public void Dispose()
    {
        _apiProcess?.Kill(true);
        _apiProcess?.Dispose();
        
        _clientProcess?.Kill(true);
        _clientProcess?.Dispose();

        GC.SuppressFinalize(this);
    }
}