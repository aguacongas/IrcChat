# run-unit-tests.ps1
Write-Host "🧪 Exécution des tests unitaires uniquement" -ForegroundColor Cyan

Write-Host "`n📦 Tests API..." -ForegroundColor Yellow
dotnet test tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj `
    --configuration Debug `
    --logger "console;verbosity=detailed"

Write-Host "`n📦 Tests Client..." -ForegroundColor Yellow
dotnet test tests/IrcChat.Client.Tests/IrcChat.Client.Tests.csproj `
    --configuration Debug `
    --logger "console;verbosity=detailed"