# watch-tests.ps1
Write-Host "👀 Mode watch pour les tests" -ForegroundColor Cyan

$testProject = Read-Host "Quel projet tester? (api/client/all)"

switch ($testProject.ToLower()) {
    "api" {
        dotnet watch test tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj
    }
    "client" {
        dotnet watch test tests/IrcChat.Client.Tests/IrcChat.Client.Tests.csproj
    }
    "all" {
        Write-Host "Démarrage du watch sur les deux projets..." -ForegroundColor Yellow
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet watch test tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj"
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet watch test tests/IrcChat.Client.Tests/IrcChat.Client.Tests.csproj"
    }
    default {
        Write-Host "Option invalide. Utiliser: api, client ou all" -ForegroundColor Red
    }
}