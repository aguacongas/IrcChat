// tests/IrcChat.Client.Tests/Services/UnifiedAuthServiceTests.cs
using Bunit;
using FluentAssertions;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class UnifiedAuthServiceTests : TestContext
{
    private readonly Mock<LocalStorageService> _localStorageMock;
    private readonly Mock<HttpClient> _httpClientMock;

    public UnifiedAuthServiceTests()
    {
        _localStorageMock = new Mock<LocalStorageService>(MockBehavior.Strict, JSRuntime);
        _httpClientMock = new Mock<HttpClient>(MockBehavior.Strict);

        Services.AddSingleton(_localStorageMock.Object);
        Services.AddSingleton(_httpClientMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRestoreFromLocalStorage()
    {
        // Arrange
        var authData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Username = "testuser",
            Token = "test-token",
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            Email = "test@example.com",
            AvatarUrl = "https://example.com/avatar.jpg",
            UserId = Guid.NewGuid(),
            IsAdmin = false
        });

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(authData);

        var service = new UnifiedAuthService(_localStorageMock.Object, _httpClientMock.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        service.HasUsername.Should().BeTrue();
        service.Username.Should().Be("testuser");
        service.IsAuthenticated.Should().BeTrue();
        service.IsReserved.Should().BeTrue();
        service.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task SetUsernameAsync_ShouldSaveToLocalStorage()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _localStorageMock
            .Setup(x => x.SetItemAsync("ircchat_unified_auth", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = new UnifiedAuthService(_localStorageMock.Object, _httpClientMock.Object);
        await service.InitializeAsync();

        var onAuthStateChangedCalled = false;
        service.OnAuthStateChanged += () => onAuthStateChangedCalled = true;

        // Act
        await service.SetUsernameAsync("newuser", isReserved: false);

        // Assert
        service.Username.Should().Be("newuser");
        service.IsReserved.Should().BeFalse();
        onAuthStateChangedCalled.Should().BeTrue();

        _localStorageMock.Verify(
            x => x.SetItemAsync("ircchat_unified_auth", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldClearStateAndStorage()
    {
        // Arrange
        var authData = System.Text.Json.JsonSerializer.Serialize(new
        {
            Username = "testuser",
            Token = "test-token"
        });

        _localStorageMock
            .Setup(x => x.GetItemAsync("ircchat_unified_auth"))
            .ReturnsAsync(authData);

        _localStorageMock
            .Setup(x => x.RemoveItemAsync("ircchat_unified_auth"))
            .Returns(Task.CompletedTask);

        var service = new UnifiedAuthService(_localStorageMock.Object, _httpClientMock.Object);
        await service.InitializeAsync();

        // Act
        await service.ClearAllAsync();

        // Assert
        service.HasUsername.Should().BeFalse();
        service.Username.Should().BeNull();
        service.IsAuthenticated.Should().BeFalse();

        _localStorageMock.Verify(
            x => x.RemoveItemAsync("ircchat_unified_auth"),
            Times.Once);
    }
}
