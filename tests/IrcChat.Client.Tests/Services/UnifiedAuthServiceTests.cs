// tests/IrcChat.Client.Tests/Services/UnifiedAuthServiceCompleteTests.cs
using System.Net;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.Logging;
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
    private readonly IRequestAuthenticationService _requestAuthenticationService;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;

    public UnifiedAuthServiceCompleteTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>(MockBehavior.Strict);
        _localStorageService = new LocalStorageService(_jsRuntimeMock.Object);
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("https://localhost:7000");
        _mockHttp.When("/api/oauth/set-client-cookie")
            .Respond(HttpStatusCode.OK);
        _requestAuthenticationService = new RequestAuthenticationService();
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
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
        Assert.Equal("test-token", service.Token);
        Assert.Equal("testuser", service.Username);
        Assert.Equal("test@example.com", service.Email);
        Assert.Equal("https://example.com/avatar.jpg", service.AvatarUrl);
        Assert.Equal(userId, service.UserId);
        Assert.True(service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, service.ReservedProvider);
        Assert.True(service.IsAuthenticated);
        Assert.True(service.IsAdmin);
        Assert.True(eventTriggered);
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
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
        Assert.Null(service.Token);
        Assert.Null(service.Email);
        Assert.Null(service.AvatarUrl);
        Assert.Null(service.UserId);
        Assert.False(service.IsAdmin);
        Assert.False(service.IsAuthenticated);

        // Le username et le statut réservé doivent être conservés
        Assert.Equal("testuser", service.Username);
        Assert.True(service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, service.ReservedProvider);

        Assert.True(eventTriggered);
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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username");
        mockedRequest.Respond(HttpStatusCode.OK);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
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
        Assert.Null(service.Username);
        Assert.Null(service.Token);
        Assert.False(service.IsReserved);
        Assert.Null(service.ReservedProvider);
        Assert.Null(service.Email);
        Assert.Null(service.AvatarUrl);
        Assert.Null(service.UserId);
        Assert.False(service.IsAdmin);
        Assert.False(service.IsAuthenticated);
        Assert.False(service.HasUsername);
        Assert.True(eventTriggered);

        var matchCount = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, matchCount);
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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetUsernameAsync("guestuser", isReserved: false);

        // Act
        await service.ForgetUsernameAndLogoutAsync();

        // Assert
        Assert.Null(service.Username);
        Assert.False(service.HasUsername);
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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username")
            .Respond(HttpStatusCode.InternalServerError);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
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
        Assert.Null(service.Username);
        Assert.Null(service.Token);
        Assert.False(service.HasUsername);
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();
        await service.SetUsernameAsync("guestuser", isReserved: false);

        // Act & Assert
        Assert.True(service.CanForgetUsername);
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act & Assert
        Assert.False(service.CanForgetUsername);
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
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
        Assert.True(service.CanForgetUsername);
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();

        // Assert - Ne devrait pas lancer d'exception
        Assert.False(service.HasUsername);
        Assert.False(service.IsAuthenticated);
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetUsernameAsync("reserveduser", isReserved: true, ExternalAuthProvider.Microsoft);

        // Assert
        Assert.Equal("reserveduser", service.Username);
        Assert.True(service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Microsoft, service.ReservedProvider);
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
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
        Assert.Equal(1, setItemCalls);
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();
        await service.InitializeAsync();
        await service.InitializeAsync();

        // Assert
        Assert.Equal(1, getItemCalls);
    }

    [Fact]
    public async Task InitializeAsync_WithValidSavedData_ShouldRestoreAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Username = "testuser",
            Token = "test-token",
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            Email = "test@example.com",
            AvatarUrl = "https://example.com/avatar.jpg",
            UserId = userId,
            IsAdmin = true
        });

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "ircchat_unified_auth")))
            .ReturnsAsync(authData);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal("testuser", service.Username);
        Assert.Equal("test-token", service.Token);
        Assert.True(service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, service.ReservedProvider);
        Assert.Equal("test@example.com", service.Email);
        Assert.Equal("https://example.com/avatar.jpg", service.AvatarUrl);
        Assert.Equal(userId, service.UserId);
        Assert.True(service.IsAdmin);
        Assert.True(service.IsAuthenticated);
        Assert.True(service.HasUsername);
    }

    [Fact]
    public async Task InitializeAsync_WithPartialData_ShouldHandleGracefully()
    {
        // Arrange
        var authData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Username = "testuser",
            Token = (string?)null,
            IsReserved = false,
            ReservedProvider = (ExternalAuthProvider?)null,
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal("testuser", service.Username);
        Assert.Null(service.Token);
        Assert.False(service.IsReserved);
        Assert.Null(service.ReservedProvider);
        Assert.Null(service.Email);
        Assert.Null(service.AvatarUrl);
        Assert.Null(service.UserId);
        Assert.False(service.IsAdmin);
        Assert.False(service.IsAuthenticated);
        Assert.True(service.HasUsername);
    }

    [Fact]
    public async Task SetUsernameAsync_AsGuest_ShouldSaveCorrectly()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        var savedData = string.Empty;
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length == 2 && args[0]?.ToString() == "ircchat_unified_auth")
                {
                    savedData = args[1]?.ToString() ?? string.Empty;
                }
            })
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetUsernameAsync("guestuser", isReserved: false);

        // Assert
        Assert.Contains("guestuser", savedData);
        Assert.Contains("\"IsReserved\":false", savedData);
        Assert.Equal("guestuser", service.Username);
        Assert.False(service.IsReserved);
        Assert.Null(service.ReservedProvider);
    }

    [Fact]
    public async Task SetAuthStateAsync_WithAllParameters_ShouldSaveCorrectly()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        var savedData = string.Empty;
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length == 2 && args[0]?.ToString() == "ircchat_unified_auth")
                {
                    savedData = args[1]?.ToString() ?? string.Empty;
                }
            })
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        var userId = Guid.NewGuid();

        // Act
        await service.SetAuthStateAsync(
            "auth-token",
            "authuser",
            "auth@example.com",
            "https://example.com/pic.jpg",
            userId,
            ExternalAuthProvider.Microsoft,
            isAdmin: true);

        // Assert
        Assert.Contains("auth-token", savedData);
        Assert.Contains("authuser", savedData);
        Assert.Contains("auth@example.com", savedData);
        Assert.Contains("https://example.com/pic.jpg", savedData);
        Assert.Contains(userId.ToString(), savedData);
        Assert.Contains(((int)ExternalAuthProvider.Microsoft).ToString(), savedData);
        Assert.Contains("\"IsAdmin\":true", savedData);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldResetAllProperties()
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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Set some data first
        await service.SetAuthStateAsync(
            "token",
            "user",
            "email@test.com",
            "avatar.jpg",
            Guid.NewGuid(),
            ExternalAuthProvider.Facebook,
            isAdmin: true);

        var eventTriggered = false;
        service.OnAuthStateChanged += () => eventTriggered = true;

        // Act
        await service.ClearAllAsync();

        // Assert
        Assert.Null(service.Username);
        Assert.Null(service.Token);
        Assert.False(service.IsReserved);
        Assert.Null(service.ReservedProvider);
        Assert.Null(service.Email);
        Assert.Null(service.AvatarUrl);
        Assert.Null(service.UserId);
        Assert.False(service.IsAdmin);
        Assert.False(service.IsAuthenticated);
        Assert.False(service.HasUsername);
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task OnAuthStateChanged_ShouldTriggerOnAllStateChanges()
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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        var eventCount = 0;
        service.OnAuthStateChanged += () => eventCount++;

        // Act
        await service.SetUsernameAsync("user1", false);
        await service.SetAuthStateAsync("token", "user2", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);
        await service.LogoutAsync();
        await service.ClearAllAsync();

        // Assert
        Assert.Equal(4, eventCount);
    }

    [Fact]
    public async Task SetAuthStateAsync_WithDifferentProviders_ShouldSaveCorrectProvider()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Test Google
        await service.SetAuthStateAsync("token1", "user1", "email1", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);
        Assert.Equal(ExternalAuthProvider.Google, service.ReservedProvider);

        // Test Microsoft
        await service.SetAuthStateAsync("token2", "user2", "email2", null, Guid.NewGuid(), ExternalAuthProvider.Microsoft, false);
        Assert.Equal(ExternalAuthProvider.Microsoft, service.ReservedProvider);

        // Test Facebook
        await service.SetAuthStateAsync("token3", "user3", "email3", null, Guid.NewGuid(), ExternalAuthProvider.Facebook, false);
        Assert.Equal(ExternalAuthProvider.Facebook, service.ReservedProvider);
    }

    [Fact]
    public async Task LogoutAsync_MultipleSubscribers_ShouldNotifyAll()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        var subscriber1Called = 0;
        var subscriber2Called = 0;
        var subscriber3Called = 0;

        service.OnAuthStateChanged += () => subscriber1Called++;
        service.OnAuthStateChanged += () => subscriber2Called++;
        service.OnAuthStateChanged += () => subscriber3Called++;

        // Act
        await service.LogoutAsync();

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
        Assert.Equal(1, subscriber3Called);
    }

    [Fact]
    public async Task HasUsername_AfterSettingUsername_ShouldReturnTrue()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetUsernameAsync("testuser", false);

        // Assert
        Assert.True(service.HasUsername);
    }

    [Fact]
    public async Task IsAuthenticated_WithToken_ShouldReturnTrue()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        // Assert
        Assert.True(service.IsAuthenticated);
    }

    [Fact]
    public async Task IsAuthenticated_WithoutToken_ShouldReturnFalse()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act & Assert
        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public async Task CanForgetUsername_WithNoUsername_ShouldReturnFalse()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act & Assert
        Assert.False(service.CanForgetUsername);
    }

    [Fact]
    public async Task SetAuthStateAsync_WithNullAvatarUrl_ShouldHandleGracefully()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        // Assert
        Assert.Null(service.AvatarUrl);
        Assert.True(service.IsAuthenticated);
    }

    [Fact]
    public async Task LogoutAsync_ShouldPreserveUsernameAndReservedStatus()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetAuthStateAsync(
            "token",
            "reserveduser",
            "email@test.com",
            "avatar.jpg",
            Guid.NewGuid(),
            ExternalAuthProvider.Microsoft,
            isAdmin: true);

        // Act
        await service.LogoutAsync();

        // Assert
        Assert.Equal("reserveduser", service.Username);
        Assert.True(service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Microsoft, service.ReservedProvider);
        Assert.Null(service.Token);
        Assert.Null(service.Email);
        Assert.Null(service.AvatarUrl);
        Assert.Null(service.UserId);
        Assert.False(service.IsAdmin);
    }

    // tests/IrcChat.Client.Tests/Services/UnifiedAuthServiceTests.cs

    [Fact]
    public async Task RestoreFromLocalStorageAsync_WhenExceptionThrown_ShouldLogError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<UnifiedAuthService>>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Storage access denied"));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, loggerMock.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.False(service.HasUsername);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("lecture des données d'authentification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_WhenApiThrowsNonHttpException_ShouldLogWarning()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<UnifiedAuthService>>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>(
                "localStorageHelper.getItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username")
            .Throw(new InvalidOperationException("Unexpected error"));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, loggerMock.Object);
        await service.InitializeAsync();

        await service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        // Act
        await service.ForgetUsernameAndLogoutAsync();

        // Assert
        Assert.Null(service.Username);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("déconnexion côté serveur")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ==================== TESTS POUR GetClientUserIdAsync ====================

    [Fact]
    public async Task GetClientUserIdAsync_WhenCachedValue_ShouldReturnCachedValue()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var mockModule = new Mock<IJSObjectReference>();
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ReturnsAsync("cached-guid");

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act - Premier appel
        var firstResult = await service.GetClientUserIdAsync();

        // Act - Deuxième appel (devrait utiliser le cache)
        var secondResult = await service.GetClientUserIdAsync();

        // Assert
        Assert.Equal("cached-guid", firstResult);
        Assert.Equal("cached-guid", secondResult);
        // Le module ne devrait être chargé qu'une seule fois
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task GetClientUserIdAsync_WhenReservedUser_ShouldReturnUserId()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var mockModule = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        var id = Guid.NewGuid();
        await service.SetAuthStateAsync("token", "ReservedUser", null, null, id, ExternalAuthProvider.Google);

        // Act
        var userId = await service.GetClientUserIdAsync();

        // Assert
        Assert.Equal(id.ToString(), userId);
    }

    [Fact]
    public async Task GetClientUserIdAsync_WhenGuestUser_ShouldReturnGuidFromIndexedDB()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var mockModule = new Mock<IJSObjectReference>();
        var expectedGuid = "12345678-1234-1234-1234-123456789012";
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ReturnsAsync(expectedGuid);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId = await service.GetClientUserIdAsync();

        // Assert
        Assert.Equal(expectedGuid, userId);
        mockModule.Verify(
            x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task GetClientUserIdAsync_WhenModuleLoadFails_ShouldReturnFallbackGuid()
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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId = await service.GetClientUserIdAsync();

        // Assert
        Assert.NotNull(userId);
        Assert.NotEmpty(userId);
        // Devrait être un GUID valide
        Assert.True(Guid.TryParse(userId, out _));
    }

    [Fact]
    public async Task GetClientUserIdAsync_WhenGetUserIdFails_ShouldReturnFallbackGuid()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var mockModule = new Mock<IJSObjectReference>();
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("IndexedDB error"));

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId = await service.GetClientUserIdAsync();

        // Assert
        Assert.NotNull(userId);
        Assert.NotEmpty(userId);
        // Devrait être un GUID valide
        Assert.True(Guid.TryParse(userId, out _));
    }

    [Fact]
    public async Task GetClientUserIdAsync_MultipleCalls_ShouldReturnSameValue()
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
            .ReturnsAsync((IJSVoidResult)null!);

        var mockModule = new Mock<IJSObjectReference>();
        var guidValue = "fixed-guid-value";
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ReturnsAsync(guidValue);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, _jsRuntimeMock.Object, _requestAuthenticationService, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        await service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId1 = await service.GetClientUserIdAsync();
        var userId2 = await service.GetClientUserIdAsync();
        var userId3 = await service.GetClientUserIdAsync();

        // Assert - Tous les appels devraient retourner la même valeur
        Assert.Equal(guidValue, userId1);
        Assert.Equal(userId1, userId2);
        Assert.Equal(userId1, userId3);

        // Le module ne devrait être appelé qu'une seule fois (ensuite c'est caché)
        mockModule.Verify(
            x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()),
            Times.Once);
    }
}