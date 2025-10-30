# start-test-env.ps1 - Démarre l'environnement de test
Write-Host "🚀 Démarrage de l'environnement de test" -ForegroundColor Cyan

# Démarrer l'API
Write-Host "`n📡 Démarrage de l'API..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd src/IrcChat.Api; dotnet run"
Start-Sleep -Seconds 5

# Démarrer le Client
Write-Host "🌐 Démarrage du Client..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd src/IrcChat.Client; dotnet run"
Start-Sleep -Seconds 5

Write-Host "`n✅ Environnement de test prêt!" -ForegroundColor Green
Write-Host "API: https://localhost:7000" -ForegroundColor Cyan
Write-Host "Client: https://localhost:7001" -ForegroundColor Cyan
Write-Host "`nAppuyez sur une touche pour exécuter les tests E2E..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Exécuter les tests E2E
.\run-e2e-tests.ps1