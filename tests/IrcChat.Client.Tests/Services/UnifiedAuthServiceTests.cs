// tests/IrcChat.Client.Tests/Services/UnifiedAuthServiceCompleteTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
            .ReturnsAsync((IJSVoidResult)null!);

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
            .ReturnsAsync((IJSVoidResult)null!);

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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/oauth/forget-username");
        mockedRequest.Respond(HttpStatusCode.OK);

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

        _mockHttp.GetMatchCount(mockedRequest)
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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

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
            .ReturnsAsync((IJSVoidResult)null!);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

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
            .ReturnsAsync((IJSVoidResult)null!);

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
            .ReturnsAsync((IJSVoidResult)null!);

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
            .ReturnsAsync((IJSVoidResult)null!);

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
            .ReturnsAsync((IJSVoidResult)null!);

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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();

        // Assert
        service.Username.Should().Be("testuser");
        service.Token.Should().Be("test-token");
        service.IsReserved.Should().BeTrue();
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Google);
        service.Email.Should().Be("test@example.com");
        service.AvatarUrl.Should().Be("https://example.com/avatar.jpg");
        service.UserId.Should().Be(userId);
        service.IsAdmin.Should().BeTrue();
        service.IsAuthenticated.Should().BeTrue();
        service.HasUsername.Should().BeTrue();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);

        // Act
        await service.InitializeAsync();

        // Assert
        service.Username.Should().Be("testuser");
        service.Token.Should().BeNull();
        service.IsReserved.Should().BeFalse();
        service.ReservedProvider.Should().BeNull();
        service.Email.Should().BeNull();
        service.AvatarUrl.Should().BeNull();
        service.UserId.Should().BeNull();
        service.IsAdmin.Should().BeFalse();
        service.IsAuthenticated.Should().BeFalse();
        service.HasUsername.Should().BeTrue();
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

        var savedData = "";
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length == 2 && args[0]?.ToString() == "ircchat_unified_auth")
                {
                    savedData = args[1]?.ToString() ?? "";
                }
            })
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetUsernameAsync("guestuser", isReserved: false);

        // Assert
        savedData.Should().Contain("guestuser");
        savedData.Should().Contain("\"IsReserved\":false");
        service.Username.Should().Be("guestuser");
        service.IsReserved.Should().BeFalse();
        service.ReservedProvider.Should().BeNull();
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

        var savedData = "";
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "localStorageHelper.setItem",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length == 2 && args[0]?.ToString() == "ircchat_unified_auth")
                {
                    savedData = args[1]?.ToString() ?? "";
                }
            })
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
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
        savedData.Should().Contain("auth-token");
        savedData.Should().Contain("authuser");
        savedData.Should().Contain("auth@example.com");
        savedData.Should().Contain("https://example.com/pic.jpg");
        savedData.Should().Contain(userId.ToString());
        savedData.Should().Contain(((int)ExternalAuthProvider.Microsoft).ToString());
        savedData.Should().Contain("\"IsAdmin\":true");
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        var eventCount = 0;
        service.OnAuthStateChanged += () => eventCount++;

        // Act
        await service.SetUsernameAsync("user1", false);
        await service.SetAuthStateAsync("token", "user2", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);
        await service.LogoutAsync();
        await service.ClearAllAsync();

        // Assert
        eventCount.Should().Be(4);
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Test Google
        await service.SetAuthStateAsync("token1", "user1", "email1", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Google);

        // Test Microsoft
        await service.SetAuthStateAsync("token2", "user2", "email2", null, Guid.NewGuid(), ExternalAuthProvider.Microsoft, false);
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Microsoft);

        // Test Facebook
        await service.SetAuthStateAsync("token3", "user3", "email3", null, Guid.NewGuid(), ExternalAuthProvider.Facebook, false);
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Facebook);
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
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
        subscriber1Called.Should().Be(1);
        subscriber2Called.Should().Be(1);
        subscriber3Called.Should().Be(1);
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetUsernameAsync("testuser", false);

        // Assert
        service.HasUsername.Should().BeTrue();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        // Assert
        service.IsAuthenticated.Should().BeTrue();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act & Assert
        service.IsAuthenticated.Should().BeFalse();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act & Assert
        service.CanForgetUsername.Should().BeFalse();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
        await service.InitializeAsync();

        // Act
        await service.SetAuthStateAsync("token", "user", "email", null, Guid.NewGuid(), ExternalAuthProvider.Google, false);

        // Assert
        service.AvatarUrl.Should().BeNull();
        service.IsAuthenticated.Should().BeTrue();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, NullLogger<UnifiedAuthService>.Instance);
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
        service.Username.Should().Be("reserveduser");
        service.IsReserved.Should().BeTrue();
        service.ReservedProvider.Should().Be(ExternalAuthProvider.Microsoft);
        service.Token.Should().BeNull();
        service.Email.Should().BeNull();
        service.AvatarUrl.Should().BeNull();
        service.UserId.Should().BeNull();
        service.IsAdmin.Should().BeFalse();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, loggerMock.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        service.HasUsername.Should().BeFalse();
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

        var service = new UnifiedAuthService(_localStorageService, _httpClient, loggerMock.Object);
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
        service.Username.Should().BeNull();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("déconnexion côté serveur")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}