using Bunit;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class ReserveUsernameTests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;
    private readonly Mock<IJSRuntime> _jsRuntimeMock;

    public ReserveUsernameTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _navigationManagerMock = new Mock<NavigationManager>();
        _jsRuntimeMock = new Mock<IJSRuntime>();

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_navigationManagerMock.Object);
        Services.AddSingleton(_jsRuntimeMock.Object);
    }

    [Fact]
    public void ReserveUsername_WithoutUsernameParam_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/login", false))
            .Callback(() => navigateCalled = true);

        // Act
        var cut = RenderComponent<ReserveUsername>(parameters => parameters
            .Add(p => p.UsernameParam, null));

        // Assert
        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public void ReserveUsername_WithUsernameParam_ShouldDisplayUsername()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        var cut = RenderComponent<ReserveUsername>(parameters => parameters
            .Add(p => p.UsernameParam, "TestUser"));

        // Assert
        cut.Markup.Should().Contain("TestUser");
        cut.Markup.Should().Contain("Réserver mon pseudo");
    }

    [Fact]
    public void ReserveUsername_ShouldShowOAuthProviders()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        var cut = RenderComponent<ReserveUsername>(parameters => parameters
            .Add(p => p.UsernameParam, "TestUser"));

        // Assert
        cut.Markup.Should().Contain("Google");
        cut.Markup.Should().Contain("Microsoft");
        cut.Markup.Should().Contain("Facebook");
    }

    [Fact]
    public async Task ReserveUsername_ClickProvider_ShouldSaveAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<object>(
                "sessionStorage.setItem",
                It.Is<object[]>(o =>
                    o.Length == 2 &&
                    (string)o[0] == "temp_username_to_reserve" &&
                    (string)o[1] == "TestUser")))
            .ReturnsAsync(new object());

        var navigateCalled = false;
        string? navigationUrl = null;
        _navigationManagerMock
            .Setup(x => x.NavigateTo(It.IsAny<string>(), false))
            .Callback<string, bool>((url, _) =>
            {
                navigateCalled = true;
                navigationUrl = url;
            });

        var cut = RenderComponent<ReserveUsername>(parameters => parameters
            .Add(p => p.UsernameParam, "TestUser"));

        // Act
        var googleButton = cut.Find("button.oauth-btn.google");
        await cut.InvokeAsync(() => googleButton.Click());

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<object>(
                "sessionStorage.setItem",
                It.Is<object[]>(o =>
                    o.Length == 2 &&
                    (string)o[0] == "temp_username_to_reserve")),
            Times.Once);
        navigateCalled.Should().BeTrue();
        navigationUrl.Should().Contain("oauth-login");
        navigationUrl.Should().Contain("provider=Google");
        navigationUrl.Should().Contain("mode=reserve");
    }

    [Fact]
    public void ReserveUsername_BackLink_ShouldNavigateToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var cut = RenderComponent<ReserveUsername>(parameters => parameters
            .Add(p => p.UsernameParam, "TestUser"));

        // Act
        var backLink = cut.Find("a[href='/login']");

        // Assert
        backLink.Should().NotBeNull();
        backLink.GetAttribute("href").Should().Be("/login");
    }
}
