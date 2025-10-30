# run-e2e-tests.ps1
Write-Host "🌐 Exécution des tests End-to-End" -ForegroundColor Cyan

# Vérifier si l'application est en cours d'exécution
$apiUrl = "https://localhost:7000"
$clientUrl = "https://localhost:7001"

Write-Host "Vérification de l'API sur $apiUrl..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$apiUrl/api/channels" -SkipCertificateCheck -ErrorAction Stop
    Write-Host "✅ API accessible" -ForegroundColor Green
}
catch {
    Write-Host "❌ API non accessible. Démarrez l'API d'abord." -ForegroundColor Red
    exit 1
}

Write-Host "Vérification du Client sur $clientUrl..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri $clientUrl -SkipCertificateCheck -ErrorAction Stop
    Write-Host "✅ Client accessible" -ForegroundColor Green
}
catch {
    Write-Host "❌ Client non accessible. Démarrez le Client d'abord." -ForegroundColor Red
    exit 1
}

Write-Host "`n🎭 Installation de Playwright..." -ForegroundColor Yellow
pwsh tests/IrcChat.E2E.Tests/bin/Debug/net9.0/playwright.ps1 install

Write-Host "`n🧪 Exécution des tests E2E..." -ForegroundColor Yellow
dotnet test tests/IrcChat.E2E.Tests/IrcChat.E2E.Tests.csproj `
    --configuration Debug `
    --logger "console;verbosity=detailed"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Tests E2E terminés avec succès!" -ForegroundColor Green
}
else {
    Write-Host "`n❌ Les tests E2E ont échoué" -ForegroundColor Red
}