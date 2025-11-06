// tests/IrcChat.Client.Tests/Services/UnifiedAuthServiceCompleteTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class UnifiedAuthServiceCompleteTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly LocalStorageService _localStorageService;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;

    public UnifiedAuthServiceCompleteTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>(MockBehavior.Strict);
        _localStorageService = new LocalStorageService(_jsRuntimeMock.Object);
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("https://localhost:7000");
    }

    [Fact]
    public async Task SetAuthStateAsync_ShouldSetAllPropertiesAndTriggerEvent()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        var eventTriggered = false;
        service.OnAuthStateChanged += () => eventTriggered = true;

        var userId = Guid.NewGuid();

        // Act
        await service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            "https://example.com/avatar.jpg",
            userId,
            ExternalAuthProvider.Google,
            isAdmin: true);

        // Assert
        service.Token.Should().Be("test-token");
        service.Username.Should().Be("testuser");
        service.Email.Should().Be("test@example.com");
        service.AvatarUrl.Should().Be("https://example.com/avatar.jpg");
        service.UserId.Should().Be(userId);
        service.IsReserved.Should().BeTrue();
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Google);
        service.IsAuthenticated.Should().BeTrue();
        service.IsAdmin.Should().BeTrue();
        eventTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearTokenButKeepUsername()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        var userId = Guid.NewGuid();
        await service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            "https://example.com/avatar.jpg",
            userId,
            ExternalAuthProvider.Google,
            isAdmin: true);

        var eventTriggered = false;
        service.OnAuthStateChanged += () => eventTriggered = true;

        // Act
        await service.LogoutAsync();

        // Assert
        service.Token.Should().BeNull();
        service.Email.Should().BeNull();
        service.AvatarUrl.Should().BeNull();
        service.UserId.Should().BeNull();
        service.IsAdmin.Should().BeFalse();
        service.IsAuthenticated.Should().BeFalse();

        // Le username et le statut réservé doivent être conservés
        service.Username.Should().Be("testuser");
        service.IsReserved.Should().BeTrue();
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Google);

        eventTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_WithAuthentication_ShouldCallApiAndClear()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var request = _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username");
        request.Respond(HttpStatusCode.OK);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        var eventTriggered = false;
        service.OnAuthStateChanged += () => eventTriggered = true;

        // Act
        await service.ForgetUsernameAndLogoutAsync();

        // Assert
        service.Username.Should().BeNull();
        service.Token.Should().BeNull();
        service.IsReserved.Should().BeFalse();
        service.ReservedProvider.Should().BeNull();
        service.Email.Should().BeNull();
        service.AvatarUrl.Should().BeNull();
        service.UserId.Should().BeNull();
        service.IsAdmin.Should().BeFalse();
        service.IsAuthenticated.Should().BeFalse();
        service.HasUsername.Should().BeFalse();
        eventTriggered.Should().BeTrue();

        _mockHttp.GetMatchCount(request)
            .Should().Be(1);
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_WithoutAuthentication_ShouldClearLocally()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetUsernameAsync("guestuser", isReserved: false);

        // Act
        await service.ForgetUsernameAndLogoutAsync();

        // Assert
        service.Username.Should().BeNull();
        service.HasUsername.Should().BeFalse();
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_WhenApiCallFails_ShouldStillClearLocally()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username")
            .Respond(HttpStatusCode.InternalServerError);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        // Act - Ne devrait pas lancer d'exception
        await service.ForgetUsernameAndLogoutAsync();

        // Assert - L'état local doit être nettoyé malgré l'erreur API
        service.Username.Should().BeNull();
        service.Token.Should().BeNull();
        service.HasUsername.Should().BeFalse();
    }

    [Fact]
    public async Task CanForgetUsername_WhenGuest_ShouldReturnTrue()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();
        await service.SetUsernameAsync("guestuser", isReserved: false);

        // Act & Assert
        service.CanForgetUsername.Should().BeTrue();
    }

    [Fact]
    public async Task CanForgetUsername_WhenReservedAndNotAuthenticated_ShouldReturnFalse()
    {
        // Arrange
        var authData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Username = "reserveduser",
            Token = (string?)null,
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            Email = (string?)null,
            AvatarUrl = (string?)null,
            UserId = (Guid?)null,
            IsAdmin = false
        });

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")))
            .ReturnsAsync(authData);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act & Assert
        service.CanForgetUsername.Should().BeFalse();
    }

    [Fact]
    public async Task CanForgetUsername_WhenReservedAndAuthenticated_ShouldReturnTrue()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetAuthStateAsync(
            "test-token",
            "reserveduser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        // Act & Assert
        service.CanForgetUsername.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithCorruptedData_ShouldHandleGracefully()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")))
            .ReturnsAsync("{ invalid json }");

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();

        // Assert - Ne devrait pas lancer d'exception
        service.HasUsername.Should().BeFalse();
        service.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task SetUsernameAsync_WithReservedProvider_ShouldSetCorrectly()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetUsernameAsync("reserveduser", isReserved: true, ExternalAuthProvider.Microsoft);

        // Assert
        service.Username.Should().Be("reserveduser");
        service.IsReserved.Should().BeTrue();
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Microsoft);
    }

    [Fact]
    public async Task LogoutAsync_ShouldSaveStateToLocalStorage()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        var setItemCalls = 0;
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Callback(() => setItemCalls++)
            .Returns(ValueTask.FromResult<IJSVoidResult>(null!));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        setItemCalls = 0; // Reset

        // Act
        await service.LogoutAsync();

        // Assert
        setItemCalls.Should().Be(1);
    }

    [Fact]
    public async Task MultipleInitializeAsync_ShouldOnlyInitializeOnce()
    {
        // Arrange
        var getItemCalls = 0;
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .Callback(() => getItemCalls++)
            .ReturnsAsync((string?)null);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();
        await service.InitializeAsync();
        await service.InitializeAsync();

        // Assert
        getItemCalls.Should().Be(1);
    }
}