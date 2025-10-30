# run-all-tests.ps1
Write-Host "🧪 Exécution de tous les tests IrcChat" -ForegroundColor Cyan

# Tests unitaires API
Write-Host "`n📦 Tests unitaires API..." -ForegroundColor Yellow
dotnet test tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj `
    --configuration Release `
    --logger "console;verbosity=normal" `
    --collect:"XPlat Code Coverage"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Échec des tests API" -ForegroundColor Red
    exit 1
}

# Tests unitaires Client
Write-Host "`n📦 Tests unitaires Client..." -ForegroundColor Yellow
dotnet test tests/IrcChat.Client.Tests/IrcChat.Client.Tests.csproj `
    --configuration Release `
    --logger "console;verbosity=normal" `
    --collect:"XPlat Code Coverage"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Échec des tests Client" -ForegroundColor Red
    exit 1
}

# Tests E2E (nécessite que l'application soit démarrée)
Write-Host "`n🌐 Tests End-to-End..." -ForegroundColor Yellow
Write-Host "⚠️  Assurez-vous que l'application est démarrée sur https://localhost:7001" -ForegroundColor Yellow

$continueE2E = Read-Host "Continuer avec les tests E2E? (O/N)"

if ($continueE2E -eq "O" -or $continueE2E -eq "o") {
    dotnet test tests/IrcChat.E2E.Tests/IrcChat.E2E.Tests.csproj `
        --configuration Release `
        --logger "console;verbosity=normal"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Échec des tests E2E" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n✅ Tous les tests ont réussi!" -ForegroundColor Green

# Générer un rapport de couverture
Write-Host "`n📊 Génération du rapport de couverture..." -ForegroundColor Cyan

# Installation de ReportGenerator si nécessaire
dotnet tool install --global dotnet-reportgenerator-globaltool

# Générer le rapport
reportgenerator `
    -reports:"**/coverage.cobertura.xml" `
    -targetdir:"TestResults/CoverageReport" `
    -reporttypes:"Html;Badges"

Write-Host "📊 Rapport de couverture généré dans TestResults/CoverageReport/" -ForegroundColor Green

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