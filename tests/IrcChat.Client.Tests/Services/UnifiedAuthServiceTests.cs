// tests/IrcChat.Client.Tests/Services/UnifiedAuthServiceTests.cs
using System.Net;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Services;

public partial class UnifiedAuthServiceTests
{
    private readonly Mock<ILocalStorageService> _localStorageMock;
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly LocalStorageService _localStorageService;
    private readonly Mock<IRequestAuthenticationService> _requestAuthMock;
    private readonly IRequestAuthenticationService _requestAuthenticationService;
    private readonly Mock<ILogger<UnifiedAuthService>> _loggerMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly UnifiedAuthService _service;

    public UnifiedAuthServiceTests()
    {
        _localStorageMock = new Mock<ILocalStorageService>();
        _jsRuntimeMock = new Mock<IJSRuntime>(MockBehavior.Strict);
        _localStorageService = new LocalStorageService(_jsRuntimeMock.Object);
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("https://localhost:7000");

        // Mock pour le cookie endpoint (appelé dans certains scénarios)
        _mockHttp.When("/api/oauth/set-client-cookie")
            .Respond(HttpStatusCode.OK);

        _requestAuthMock = new Mock<IRequestAuthenticationService>();

        // ✅ CRITIQUE : Setup de la propriété Token pour permettre get/set
        _requestAuthMock.SetupProperty(x => x.Token);

        _requestAuthenticationService = _requestAuthMock.Object;
        _loggerMock = new Mock<ILogger<UnifiedAuthService>>();

        _service = new UnifiedAuthService(
            _localStorageMock.Object,
            _httpClient,
            _jsRuntimeMock.Object,
            _requestAuthenticationService,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SetAuthStateAsync_ShouldSetAllPropertiesAndTriggerEvent()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        var eventTriggered = false;
        _service.OnAuthStateChanged += () => eventTriggered = true;

        var userId = Guid.NewGuid();

        // Act
        await _service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            "https://example.com/avatar.jpg",
            userId,
            ExternalAuthProvider.Google,
            isAdmin: true);

        // Assert
        Assert.Equal("test-token", _service.Token);
        Assert.Equal("testuser", _service.Username);
        Assert.Equal("test@example.com", _service.Email);
        Assert.Equal("https://example.com/avatar.jpg", _service.AvatarUrl);
        Assert.Equal(userId, _service.UserId);
        Assert.True(_service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, _service.ReservedProvider);
        Assert.True(_service.IsAuthenticated);
        Assert.True(_service.IsAdmin);
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearTokenButKeepUsername()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        var userId = Guid.NewGuid();
        await _service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            "https://example.com/avatar.jpg",
            userId,
            ExternalAuthProvider.Google,
            isAdmin: true);

        var eventTriggered = false;
        _service.OnAuthStateChanged += () => eventTriggered = true;

        // Act
        await _service.LogoutAsync();

        // Assert
        Assert.Null(_service.Token);
        Assert.Null(_service.Email);
        Assert.Null(_service.AvatarUrl);
        Assert.Null(_service.UserId);
        Assert.False(_service.IsAdmin);
        Assert.False(_service.IsAuthenticated);

        // Le username et le statut réservé doivent être conservés
        Assert.Equal("testuser", _service.Username);
        Assert.True(_service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, _service.ReservedProvider);

        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_WithAuthentication_ShouldCallApiAndClear()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username");
        mockedRequest.Respond(HttpStatusCode.OK);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        var eventTriggered = false;
        _service.OnAuthStateChanged += () => eventTriggered = true;

        // Act
        await _service.ForgetUsernameAndLogoutAsync();

        // Assert
        Assert.Null(_service.Username);
        Assert.Null(_service.Token);
        Assert.False(_service.IsReserved);
        Assert.Null(_service.ReservedProvider);
        Assert.Null(_service.Email);
        Assert.Null(_service.AvatarUrl);
        Assert.Null(_service.UserId);
        Assert.False(_service.IsAdmin);
        Assert.False(_service.IsAuthenticated);
        Assert.False(_service.HasUsername);
        Assert.True(eventTriggered);

        var matchCount = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, matchCount);
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_WithoutAuthentication_ShouldClearLocally()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetUsernameAsync("guestuser", isReserved: false);

        // Act
        await _service.ForgetUsernameAndLogoutAsync();

        // Assert
        Assert.Null(_service.Username);
        Assert.False(_service.HasUsername);
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_WhenApiCallFails_ShouldStillClearLocally()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username")
            .Respond(HttpStatusCode.InternalServerError);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        // Act - Ne devrait pas lancer d'exception
        await _service.ForgetUsernameAndLogoutAsync();

        // Assert - L'état local doit être nettoyé malgré l'erreur API
        Assert.Null(_service.Username);
        Assert.Null(_service.Token);
        Assert.False(_service.HasUsername);
    }

    [Fact]
    public async Task CanForgetUsername_WhenGuest_ShouldReturnTrue()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();
        await _service.SetUsernameAsync("guestuser", isReserved: false);

        // Act & Assert
        Assert.True(_service.CanForgetUsername);
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
            IsAdmin = false,
        });

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(authData);

        await _service.InitializeAsync();

        // Act & Assert
        Assert.False(_service.CanForgetUsername);
    }

    [Fact]
    public async Task CanForgetUsername_WhenReservedAndAuthenticated_ShouldReturnTrue()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync(
            "test-token",
            "reserveduser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        // Act & Assert
        Assert.True(_service.CanForgetUsername);
    }

    [Fact]
    public async Task InitializeAsync_WithCorruptedData_ShouldHandleGracefully()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync("{ invalid json }");

        // Act
        await _service.InitializeAsync();

        // Assert - Ne devrait pas lancer d'exception
        Assert.False(_service.HasUsername);
        Assert.False(_service.IsAuthenticated);
    }

    [Fact]
    public async Task SetUsernameAsync_WithReservedProvider_ShouldSetCorrectly()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetUsernameAsync("reserveduser", isReserved: true, ExternalAuthProvider.Microsoft);

        // Assert
        Assert.Equal("reserveduser", _service.Username);
        Assert.True(_service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Microsoft, _service.ReservedProvider);
    }

    [Fact]
    public async Task LogoutAsync_ShouldSaveStateToLocalStorage()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var setItemCalls = 0;
        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => setItemCalls++)
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync(
            "test-token",
            "testuser",
            "test@example.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        setItemCalls = 0; // Reset

        // Act
        await _service.LogoutAsync();

        // Assert
        Assert.Equal(1, setItemCalls);
    }

    [Fact]
    public async Task MultipleInitializeAsync_ShouldOnlyInitializeOnce()
    {
        // Arrange
        var getItemCalls = 0;
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .Callback(() => getItemCalls++)
            .ReturnsAsync((string?)null);

        // Act
        await _service.InitializeAsync();
        await _service.InitializeAsync();
        await _service.InitializeAsync();

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
            IsAdmin = true,
        });

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(authData);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.Equal("testuser", _service.Username);
        Assert.Equal("test-token", _service.Token);
        Assert.True(_service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Google, _service.ReservedProvider);
        Assert.Equal("test@example.com", _service.Email);
        Assert.Equal("https://example.com/avatar.jpg", _service.AvatarUrl);
        Assert.Equal(userId, _service.UserId);
        Assert.True(_service.IsAdmin);
        Assert.True(_service.IsAuthenticated);
        Assert.True(_service.HasUsername);
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
            IsAdmin = false,
        });

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(authData);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.Equal("testuser", _service.Username);
        Assert.Null(_service.Token);
        Assert.False(_service.IsReserved);
        Assert.Null(_service.ReservedProvider);
        Assert.Null(_service.Email);
        Assert.Null(_service.AvatarUrl);
        Assert.Null(_service.UserId);
        Assert.False(_service.IsAdmin);
        Assert.False(_service.IsAuthenticated);
        Assert.True(_service.HasUsername);
    }

    [Fact]
    public async Task SetUsernameAsync_AsGuest_ShouldSaveCorrectly()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var savedData = string.Empty;
        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((key, value) =>
            {
                if (key == "ircchat_unified_auth")
                {
                    savedData = value;
                }
            })
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetUsernameAsync("guestuser", isReserved: false);

        // Assert
        Assert.Contains("guestuser", savedData);
        Assert.Contains("\"IsReserved\":false", savedData);
        Assert.Equal("guestuser", _service.Username);
        Assert.False(_service.IsReserved);
        Assert.Null(_service.ReservedProvider);
    }

    [Fact]
    public async Task SetAuthStateAsync_WithAllParameters_ShouldSaveCorrectly()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var savedData = string.Empty;
        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((key, value) =>
            {
                if (key == "ircchat_unified_auth")
                {
                    savedData = value;
                }
            })
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        var userId = Guid.NewGuid();

        // Act
        await _service.SetAuthStateAsync(
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
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Set some data first
        await _service.SetAuthStateAsync(
            "token",
            "user",
            "email@test.com",
            "avatar.jpg",
            Guid.NewGuid(),
            ExternalAuthProvider.Facebook,
            isAdmin: true);

        var eventTriggered = false;
        _service.OnAuthStateChanged += () => eventTriggered = true;

        // Act
        await _service.ClearAllAsync();

        // Assert
        Assert.Null(_service.Username);
        Assert.Null(_service.Token);
        Assert.False(_service.IsReserved);
        Assert.Null(_service.ReservedProvider);
        Assert.Null(_service.Email);
        Assert.Null(_service.AvatarUrl);
        Assert.Null(_service.UserId);
        Assert.False(_service.IsAdmin);
        Assert.False(_service.IsAuthenticated);
        Assert.False(_service.HasUsername);
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task OnAuthStateChanged_ShouldTriggerOnAllStateChanges()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        var eventCount = 0;
        _service.OnAuthStateChanged += () => eventCount++;

        // Act
        await _service.SetUsernameAsync("user1", false);
        await _service.SetAuthStateAsync("token", "user2", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);
        await _service.LogoutAsync();
        await _service.ClearAllAsync();

        // Assert
        Assert.Equal(4, eventCount);
    }

    [Fact]
    public async Task SetAuthStateAsync_WithDifferentProviders_ShouldSaveCorrectProvider()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Test Google
        await _service.SetAuthStateAsync("token1", "user1", "email1", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);
        Assert.Equal(ExternalAuthProvider.Google, _service.ReservedProvider);

        // Test Microsoft
        await _service.SetAuthStateAsync("token2", "user2", "email2", null, Guid.NewGuid(), ExternalAuthProvider.Microsoft, false);
        Assert.Equal(ExternalAuthProvider.Microsoft, _service.ReservedProvider);

        // Test Facebook
        await _service.SetAuthStateAsync("token3", "user3", "email3", null, Guid.NewGuid(), ExternalAuthProvider.Facebook, false);
        Assert.Equal(ExternalAuthProvider.Facebook, _service.ReservedProvider);
    }

    [Fact]
    public async Task LogoutAsync_MultipleSubscribers_ShouldNotifyAll()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        var subscriber1Called = 0;
        var subscriber2Called = 0;
        var subscriber3Called = 0;

        _service.OnAuthStateChanged += () => subscriber1Called++;
        _service.OnAuthStateChanged += () => subscriber2Called++;
        _service.OnAuthStateChanged += () => subscriber3Called++;

        // Act
        await _service.LogoutAsync();

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
        Assert.Equal(1, subscriber3Called);
    }

    [Fact]
    public async Task HasUsername_AfterSettingUsername_ShouldReturnTrue()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetUsernameAsync("testuser", false);

        // Assert
        Assert.True(_service.HasUsername);
    }

    [Fact]
    public async Task IsAuthenticated_WithToken_ShouldReturnTrue()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        // Assert
        Assert.True(_service.IsAuthenticated);
    }

    [Fact]
    public async Task IsAuthenticated_WithoutToken_ShouldReturnFalse()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        await _service.InitializeAsync();

        // Act & Assert
        Assert.False(_service.IsAuthenticated);
    }

    [Fact]
    public async Task CanForgetUsername_WithNoUsername_ShouldReturnFalse()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        await _service.InitializeAsync();

        // Act & Assert
        Assert.False(_service.CanForgetUsername);
    }

    [Fact]
    public async Task SetAuthStateAsync_WithNullAvatarUrl_ShouldHandleGracefully()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        // Assert
        Assert.Null(_service.AvatarUrl);
        Assert.True(_service.IsAuthenticated);
    }

    [Fact]
    public async Task LogoutAsync_ShouldPreserveUsernameAndReservedStatus()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync(
            "token",
            "reserveduser",
            "email@test.com",
            "avatar.jpg",
            Guid.NewGuid(),
            ExternalAuthProvider.Microsoft,
            isAdmin: true);

        // Act
        await _service.LogoutAsync();

        // Assert
        Assert.Equal("reserveduser", _service.Username);
        Assert.True(_service.IsReserved);
        Assert.Equal(ExternalAuthProvider.Microsoft, _service.ReservedProvider);
        Assert.Null(_service.Token);
        Assert.Null(_service.Email);
        Assert.Null(_service.AvatarUrl);
        Assert.Null(_service.UserId);
        Assert.False(_service.IsAdmin);
    }

    [Fact]
    public async Task RestoreFromLocalStorageAsync_WhenExceptionThrown_ShouldLogError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<UnifiedAuthService>>();

        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ThrowsAsync(new JSException("Storage access denied"));

        var requestAuthMock = new Mock<IRequestAuthenticationService>();
        requestAuthMock.SetupProperty(x => x.Token);

        var service = new UnifiedAuthService(
            _localStorageMock.Object,
            _httpClient,
            _jsRuntimeMock.Object,
            requestAuthMock.Object,
            loggerMock.Object);

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

        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username")
            .Throw(new InvalidOperationException("Unexpected error"));

        var requestAuthMock = new Mock<IRequestAuthenticationService>();
        requestAuthMock.SetupProperty(x => x.Token);

        var service = new UnifiedAuthService(
            _localStorageMock.Object,
            _httpClient,
            _jsRuntimeMock.Object,
            requestAuthMock.Object,
            loggerMock.Object);

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
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var mockModule = new Mock<IJSObjectReference>();
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ReturnsAsync("cached-guid");

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        await _service.InitializeAsync();

        // Act - Premier appel
        var firstResult = await _service.GetClientUserIdAsync();

        // Act - Deuxième appel (devrait utiliser le cache)
        var secondResult = await _service.GetClientUserIdAsync();

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
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var mockModule = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        await _service.InitializeAsync();

        var id = Guid.NewGuid();
        await _service.SetAuthStateAsync("token", "ReservedUser", null, null, id, ExternalAuthProvider.Google);

        // Act
        var userId = await _service.GetClientUserIdAsync();

        // Assert
        Assert.Equal(id.ToString(), userId);
    }

    [Fact]
    public async Task GetClientUserIdAsync_WhenGuestUser_ShouldReturnGuidFromIndexedDB()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var mockModule = new Mock<IJSObjectReference>();
        var expectedGuid = "12345678-1234-1234-1234-123456789012";
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ReturnsAsync(expectedGuid);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        await _service.InitializeAsync();

        await _service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId = await _service.GetClientUserIdAsync();

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
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        await _service.InitializeAsync();

        await _service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId = await _service.GetClientUserIdAsync();

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
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var mockModule = new Mock<IJSObjectReference>();
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("IndexedDB error"));

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        await _service.InitializeAsync();

        await _service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId = await _service.GetClientUserIdAsync();

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
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var mockModule = new Mock<IJSObjectReference>();
        var guidValue = "fixed-guid-value";
        mockModule
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ReturnsAsync(guidValue);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        await _service.InitializeAsync();

        await _service.SetUsernameAsync("GuestUser", isReserved: false);

        // Act
        var userId1 = await _service.GetClientUserIdAsync();
        var userId2 = await _service.GetClientUserIdAsync();
        var userId3 = await _service.GetClientUserIdAsync();

        // Assert - Tous les appels devraient retourner la même valeur
        Assert.Equal(guidValue, userId1);
        Assert.Equal(userId1, userId2);
        Assert.Equal(userId1, userId3);

        // Le module ne devrait être appelé qu'une seule fois (ensuite c'est caché)
        mockModule.Verify(
            x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()),
            Times.Once);
    }

    // ==================== TESTS SETNOPVMODEASYNC ====================

    [Fact]
    public async Task SetNoPvModeAsync_WithTrue_ShouldSetPropertyAndSave()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var setItemCalls = 0;
        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => setItemCalls++)
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetNoPvModeAsync(true);

        // Assert
        Assert.True(_service.IsNoPvMode);
        Assert.Equal(1, setItemCalls); // Doit sauvegarder
    }

    [Fact]
    public async Task SetNoPvModeAsync_WithFalse_ShouldSetPropertyAndSave()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();
        await _service.SetNoPvModeAsync(true); // Activer d'abord

        var setItemCalls = 0;
        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => setItemCalls++)
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetNoPvModeAsync(false);

        // Assert
        Assert.False(_service.IsNoPvMode);
        Assert.Equal(1, setItemCalls);
    }

    [Fact]
    public async Task SetNoPvModeAsync_ShouldTriggerOnAuthStateChanged()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        var eventTriggered = false;
        _service.OnAuthStateChanged += () => eventTriggered = true;

        // Act
        await _service.SetNoPvModeAsync(true);

        // Assert
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task SetNoPvModeAsync_MultipleTimes_ShouldUpdateCorrectly()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act & Assert
        await _service.SetNoPvModeAsync(true);
        Assert.True(_service.IsNoPvMode);

        await _service.SetNoPvModeAsync(false);
        Assert.False(_service.IsNoPvMode);

        await _service.SetNoPvModeAsync(true);
        Assert.True(_service.IsNoPvMode);

        await _service.SetNoPvModeAsync(true); // Même valeur
        Assert.True(_service.IsNoPvMode);
    }

    [Fact]
    public async Task SetNoPvModeAsync_ShouldPersistToLocalStorage()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var savedData = string.Empty;
        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((key, value) =>
            {
                if (key == "ircchat_unified_auth")
                {
                    savedData = value;
                }
            })
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        // Act
        await _service.SetNoPvModeAsync(true);

        // Assert
        Assert.Contains("\"IsNoPvMode\":true", savedData);
    }

    [Fact]
    public async Task InitializeAsync_WithIsNoPvModeInStorage_ShouldRestore()
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
            IsAdmin = false,
            IsNoPvMode = true, // Mode no PV activé dans le storage
        });

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(authData);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.True(_service.IsNoPvMode);
    }

    [Fact]
    public async Task InitializeAsync_WithoutIsNoPvModeInStorage_ShouldDefaultToFalse()
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
            IsAdmin = false,
            // IsNoPvMode absent
        });

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(authData);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.False(_service.IsNoPvMode);
    }

    [Fact]
    public async Task LogoutAsync_ShouldPreserveIsNoPvMode()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync(
            "token",
            "user",
            "email@test.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        await _service.SetNoPvModeAsync(true);

        // Act
        await _service.LogoutAsync();

        // Assert - IsNoPvMode doit être préservé après logout
        Assert.True(_service.IsNoPvMode);
    }

    [Fact]
    public async Task ForgetUsernameAndLogoutAsync_ShouldResetIsNoPvMode()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username")
            .Respond(HttpStatusCode.OK);

        await _service.InitializeAsync();

        await _service.SetAuthStateAsync(
            "token",
            "user",
            "email@test.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        await _service.SetNoPvModeAsync(true);

        // Act
        await _service.ForgetUsernameAndLogoutAsync();

        // Assert - IsNoPvMode doit être réinitialisé
        Assert.False(_service.IsNoPvMode);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldResetIsNoPvMode()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetNoPvModeAsync(true);

        // Act
        await _service.ClearAllAsync();

        // Assert
        Assert.False(_service.IsNoPvMode);
    }

    [Fact]
    public async Task SetNoPvModeAsync_WithMultipleSubscribers_ShouldNotifyAll()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        var subscriber1Called = 0;
        var subscriber2Called = 0;
        var subscriber3Called = 0;

        _service.OnAuthStateChanged += () => subscriber1Called++;
        _service.OnAuthStateChanged += () => subscriber2Called++;
        _service.OnAuthStateChanged += () => subscriber3Called++;

        // Act
        await _service.SetNoPvModeAsync(true);

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
        Assert.Equal(1, subscriber3Called);
    }

    [Fact]
    public async Task SetAuthStateAsync_ShouldNotAffectIsNoPvMode()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _service.InitializeAsync();

        await _service.SetNoPvModeAsync(true);

        // Act - SetAuthState ne devrait pas réinitialiser IsNoPvMode
        await _service.SetAuthStateAsync(
            "token",
            "user",
            "email@test.com",
            null,
            Guid.NewGuid(),
            ExternalAuthProvider.Google,
            isAdmin: false);

        // Assert
        Assert.True(_service.IsNoPvMode); // Doit être préservé
    }

    [Fact]
    public async Task IsNoPvMode_DefaultValue_ShouldBeFalse()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.False(_service.IsNoPvMode); // Valeur par défaut
    }
}