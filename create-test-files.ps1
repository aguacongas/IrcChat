# create-test-files.ps1
# Script pour créer les fichiers de test squelettes pour une nouvelle fonctionnalité

param(
    [Parameter(Mandatory=$true)]
    [string]$FeatureName,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Api", "Client", "Both")]
    [string]$Target = "Both"
)

Write-Host "📝 Création des fichiers de test pour: $FeatureName" -ForegroundColor Cyan
Write-Host ""

function Create-ApiEndpointsTest {
    param([string]$Feature)
    
    $path = "tests/IrcChat.Api.Tests/Integration/${Feature}EndpointsTests.cs"
    
    if (Test-Path $path) {
        Write-Host "⚠️  Le fichier existe déjà: $path" -ForegroundColor Yellow
        return
    }
    
    $content = @"
// tests/IrcChat.Api.Tests/Integration/${Feature}EndpointsTests.cs
using FluentAssertions;
using IrcChat.Shared.Models;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class ${Feature}EndpointsTests(ApiWebApplicationFactory factory) 
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get${Feature}_ShouldReturnSuccess()
    {
        // Arrange
        
        // Act
        var response = await _client.GetAsync("/api/${Feature.ToLower()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create${Feature}_WithValidData_ShouldCreateResource()
    {
        // Arrange
        
        // Act
        
        // Assert
    }

    [Fact]
    public async Task Create${Feature}_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        
        // Act
        
        // Assert
    }

    [Fact]
    public async Task Delete${Feature}_WithValidId_ShouldDeleteResource()
    {
        // Arrange
        
        // Act
        
        // Assert
    }
}
"@
    
    New-Item -ItemType File -Path $path -Force | Out-Null
    Set-Content -Path $path -Value $content
    Write-Host "✅ Créé: $path" -ForegroundColor Green
}

function Create-ApiServiceTest {
    param([string]$Feature)
    
    $path = "tests/IrcChat.Api.Tests/Services/${Feature}ServiceTests.cs"
    
    if (Test-Path $path) {
        Write-Host "⚠️  Le fichier existe déjà: $path" -ForegroundColor Yellow
        return
    }
    
    $content = @"
// tests/IrcChat.Api.Tests/Services/${Feature}ServiceTests.cs
using FluentAssertions;
using IrcChat.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class ${Feature}ServiceTests
{
    private readonly Mock<ILogger<${Feature}Service>> _loggerMock;
    private readonly ${Feature}Service _service;

    public ${Feature}ServiceTests()
    {
        _loggerMock = new Mock<ILogger<${Feature}Service>>();
        _service = new ${Feature}Service(_loggerMock.Object);
    }

    [Fact]
    public async Task MethodName_WithValidInput_ShouldReturnExpectedResult()
    {
        // Arrange
        
        // Act
        
        // Assert
    }

    [Fact]
    public async Task MethodName_WithInvalidInput_ShouldThrowException()
    {
        // Arrange
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.MethodName(null));
    }
}
"@
    
    New-Item -ItemType File -Path $path -Force | Out-Null
    Set-Content -Path $path -Value $content
    Write-Host "✅ Créé: $path" -ForegroundColor Green
}

function Create-ClientPageTest {
    param([string]$Feature)
    
    $path = "tests/IrcChat.Client.Tests/Pages/${Feature}Tests.cs"
    
    if (Test-Path $path) {
        Write-Host "⚠️  Le fichier existe déjà: $path" -ForegroundColor Yellow
        return
    }
    
    $content = @"
// tests/IrcChat.Client.Tests/Pages/${Feature}Tests.cs
using Bunit;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class ${Feature}Tests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<HttpClient> _httpClientMock;

    public ${Feature}Tests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _httpClientMock = new Mock<HttpClient>();

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_httpClientMock.Object);
    }

    [Fact]
    public void ${Feature}_WhenRendered_ShouldDisplayContent()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        // Act
        var cut = RenderComponent<${Feature}>();

        // Assert
        cut.Markup.Should().Contain("expected content");
    }

    [Fact]
    public async Task ${Feature}_OnAction_ShouldTriggerExpectedBehavior()
    {
        // Arrange
        var cut = RenderComponent<${Feature}>();

        // Act
        var button = cut.Find("button");
        await cut.InvokeAsync(() => button.Click());

        // Assert
    }
}
"@
    
    New-Item -ItemType File -Path $path -Force | Out-Null
    Set-Content -Path $path -Value $content
    Write-Host "✅ Créé: $path" -ForegroundColor Green
}

function Create-ClientComponentTest {
    param([string]$Feature)
    
    $path = "tests/IrcChat.Client.Tests/Components/${Feature}Tests.cs"
    
    if (Test-Path $path) {
        Write-Host "⚠️  Le fichier existe déjà: $path" -ForegroundColor Yellow
        return
    }
    
    $content = @"
// tests/IrcChat.Client.Tests/Components/${Feature}Tests.cs
using Bunit;
using FluentAssertions;
using IrcChat.Client.Components;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ${Feature}Tests : TestContext
{
    [Fact]
    public void ${Feature}_WhenRendered_ShouldDisplayCorrectly()
    {
        // Arrange & Act
        var cut = RenderComponent<${Feature}>(parameters => parameters
            .Add(p => p.Property, "value"));

        // Assert
        cut.Markup.Should().Contain("expected");
    }

    [Fact]
    public async Task ${Feature}_OnInteraction_ShouldTriggerEvent()
    {
        // Arrange
        var eventTriggered = false;
        var cut = RenderComponent<${Feature}>(parameters => parameters
            .Add(p => p.OnEvent, () => eventTriggered = true));

        // Act
        var element = cut.Find("button");
        await cut.InvokeAsync(() => element.Click());

        // Assert
        eventTriggered.Should().BeTrue();
    }
}
"@
    
    New-Item -ItemType File -Path $path -Force | Out-Null
    Set-Content -Path $path -Value $content
    Write-Host "✅ Créé: $path" -ForegroundColor Green
}

# Créer les dossiers s'ils n'existent pas
if ($Target -eq "Api" -or $Target -eq "Both") {
    New-Item -ItemType Directory -Path "tests/IrcChat.Api.Tests/Integration" -Force | Out-Null
    New-Item -ItemType Directory -Path "tests/IrcChat.Api.Tests/Services" -Force | Out-Null
}

if ($Target -eq "Client" -or $Target -eq "Both") {
    New-Item -ItemType Directory -Path "tests/IrcChat.Client.Tests/Pages" -Force | Out-Null
    New-Item -ItemType Directory -Path "tests/IrcChat.Client.Tests/Components" -Force | Out-Null
}

# Créer les fichiers de test
Write-Host "Création des fichiers..." -ForegroundColor Yellow
Write-Host ""

if ($Target -eq "Api" -or $Target -eq "Both") {
    Create-ApiEndpointsTest -Feature $FeatureName
    Create-ApiServiceTest -Feature $FeatureName
}

if ($Target -eq "Client" -or $Target -eq "Both") {
    Create-ClientPageTest -Feature $FeatureName
    Create-ClientComponentTest -Feature $FeatureName
}

Write-Host ""
Write-Host "✅ Fichiers de test créés avec succès!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Prochaines étapes:" -ForegroundColor Cyan
Write-Host "   1. Remplir les tests avec la logique appropriée" -ForegroundColor Gray
Write-Host "   2. Lancer les tests: .\test-new-feature.ps1 -FeatureName $FeatureName" -ForegroundColor Gray
Write-Host "   3. Vérifier la couverture de code" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Astuce: Demande à Claude de compléter les tests automatiquement!" -ForegroundColor Cyan