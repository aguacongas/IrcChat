# test-new-feature.ps1
# Script pour vérifier les tests d'une nouvelle fonctionnalité

param(
    [Parameter(Mandatory=$true)]
    [string]$FeatureName,
    
    [Parameter(Mandatory=$false)]
    [switch]$Watch,
    
    [Parameter(Mandatory=$false)]
    [switch]$Coverage
)

Write-Host "🧪 Tests pour la fonctionnalité: $FeatureName" -ForegroundColor Cyan
Write-Host ""

# Définir les chemins des fichiers de test
$apiTestPath = "tests/IrcChat.Api.Tests/Integration/${FeatureName}EndpointsTests.cs"
$apiServiceTestPath = "tests/IrcChat.Api.Tests/Services/${FeatureName}ServiceTests.cs"
$clientPageTestPath = "tests/IrcChat.Client.Tests/Pages/${FeatureName}Tests.cs"
$clientComponentTestPath = "tests/IrcChat.Client.Tests/Components/${FeatureName}Tests.cs"

# Vérifier quels fichiers de test existent
$testFiles = @()

if (Test-Path $apiTestPath) {
    $testFiles += "✅ $apiTestPath"
}
if (Test-Path $apiServiceTestPath) {
    $testFiles += "✅ $apiServiceTestPath"
}
if (Test-Path $clientPageTestPath) {
    $testFiles += "✅ $clientPageTestPath"
}
if (Test-Path $clientComponentTestPath) {
    $testFiles += "✅ $clientComponentTestPath"
}

if ($testFiles.Count -eq 0) {
    Write-Host "⚠️  Aucun fichier de test trouvé pour '$FeatureName'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Fichiers attendus:" -ForegroundColor Yellow
    Write-Host "  - $apiTestPath"
    Write-Host "  - $apiServiceTestPath"
    Write-Host "  - $clientPageTestPath"
    Write-Host "  - $clientComponentTestPath"
    Write-Host ""
    Write-Host "💡 Astuce: Demande à Claude de créer les tests avec:" -ForegroundColor Cyan
    Write-Host "   'Implémente $FeatureName avec tests'" -ForegroundColor White
    exit 1
}

Write-Host "📝 Fichiers de test trouvés:" -ForegroundColor Green
$testFiles | ForEach-Object { Write-Host "   $_" -ForegroundColor Gray }
Write-Host ""

# Construire la commande de test
$testCommand = "dotnet test"

if ($Coverage) {
    $testCommand += " --collect:`"XPlat Code Coverage`""
    Write-Host "📊 Mode couverture de code activé" -ForegroundColor Cyan
}

# Filtrer par nom de fonctionnalité si possible
$filter = "FullyQualifiedName~$FeatureName"
$testCommand += " --filter `"$filter`""

Write-Host "🚀 Lancement des tests..." -ForegroundColor Green
Write-Host "Commande: $testCommand" -ForegroundColor Gray
Write-Host ""

if ($Watch) {
    Write-Host "👀 Mode watch activé - les tests se relanceront automatiquement" -ForegroundColor Cyan
    Write-Host ""
    
    # Lancer en mode watch
    $apiWatchCommand = "dotnet watch test tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj --filter `"$filter`""
    $clientWatchCommand = "dotnet watch test tests/IrcChat.Client.Tests/IrcChat.Client.Tests.csproj --filter `"$filter`""
    
    # Démarrer les deux en parallèle
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $apiWatchCommand
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $clientWatchCommand
    
    Write-Host "✅ Tests lancés en mode watch dans des fenêtres séparées" -ForegroundColor Green
}
else {
    # Exécuter les tests
    Invoke-Expression $testCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ Tous les tests sont passés!" -ForegroundColor Green
        
        if ($Coverage) {
            Write-Host ""
            Write-Host "📊 Génération du rapport de couverture..." -ForegroundColor Cyan
            
            # Installer ReportGenerator si nécessaire
            dotnet tool install --global dotnet-reportgenerator-globaltool 2>$null
            
            # Générer le rapport
            reportgenerator `
                -reports:"**/coverage.cobertura.xml" `
                -targetdir:"TestResults/CoverageReport" `
                -reporttypes:"Html;Badges"
            
            Write-Host "📊 Rapport de couverture généré dans TestResults/CoverageReport/" -ForegroundColor Green
            Write-Host "💡 Ouvrez TestResults/CoverageReport/index.html dans votre navigateur" -ForegroundColor Cyan
        }
    }
    else {
        Write-Host ""
        Write-Host "❌ Certains tests ont échoué" -ForegroundColor Red
        Write-Host "💡 Corrigez les tests et relancez le script" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "📋 Checklist de la fonctionnalité:" -ForegroundColor Cyan
Write-Host "   [ ] Tous les tests passent" -ForegroundColor Gray
Write-Host "   [ ] Couverture ≥ 80% pour le backend" -ForegroundColor Gray
Write-Host "   [ ] Couverture ≥ 70% pour le frontend" -ForegroundColor Gray
Write-Host "   [ ] Tests de régression OK" -ForegroundColor Gray
Write-Host "   [ ] Documentation mise à jour" -ForegroundColor Gray