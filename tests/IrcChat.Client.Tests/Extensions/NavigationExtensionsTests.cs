using IrcChat.Client.Extensions;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Extensions;

public class NavigationExtensionsTests
{
    [Theory]
    [InlineData("https://localhost/", "chat", "https://localhost/chat")]
    [InlineData("https://localhost/IrcChat/", "chat", "https://localhost/IrcChat/chat")]
    [InlineData("https://aguacongas.github.io/IrcChat/", "chat", "https://aguacongas.github.io/IrcChat/chat")]
    [InlineData("https://localhost/", "/chat", "https://localhost/chat")]
    [InlineData("https://localhost/IrcChat/", "/chat", "https://localhost/IrcChat/chat")]
    [InlineData("https://aguacongas.github.io/IrcChat/", "/settings", "https://aguacongas.github.io/IrcChat/settings")]
    public void NavigateToRelative_WithDifferentBaseUris_ShouldNavigateCorrectly(
        string baseUri, string relativeUri, string expectedUri)
    {
        // Arrange
        var navigationManager = new Mock<NavigationManager>();
        navigationManager.SetupGet(x => x.BaseUri).Returns(baseUri);

        string? capturedUri = null;
        bool? capturedForceLoad = null;

        navigationManager
            .Setup(x => x.NavigateTo(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((uri, forceLoad) =>
            {
                capturedUri = uri;
                capturedForceLoad = forceLoad;
            });

        // Simuler ToAbsoluteUri
        navigationManager
            .Setup(x => x.ToAbsoluteUri(It.IsAny<string>()))
            .Returns<string>(uri =>
            {
                var cleanUri = uri.TrimStart('/');
                return new Uri(new Uri(baseUri), cleanUri);
            });

        // Act
        navigationManager.Object.NavigateToRelative(relativeUri);

        // Assert
        Assert.Equal(expectedUri, capturedUri);
        Assert.False(capturedForceLoad);
        navigationManager.Verify(x => x.NavigateTo(expectedUri, false), Times.Once);
    }

    [Theory]
    [InlineData("https://localhost/", "chat", true)]
    [InlineData("https://localhost/IrcChat/", "settings", true)]
    public void NavigateToRelative_WithForceLoad_ShouldPassForceLoadParameter(
        string baseUri, string relativeUri, bool forceLoad)
    {
        // Arrange
        var navigationManager = new Mock<NavigationManager>();
        navigationManager.SetupGet(x => x.BaseUri).Returns(baseUri);

        bool? capturedForceLoad = null;

        navigationManager
            .Setup(x => x.NavigateTo(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((_, fl) => capturedForceLoad = fl);

        navigationManager
            .Setup(x => x.ToAbsoluteUri(It.IsAny<string>()))
            .Returns<string>(uri =>
            {
                var cleanUri = uri.TrimStart('/');
                return new Uri(new Uri(baseUri), cleanUri);
            });

        // Act
        navigationManager.Object.NavigateToRelative(relativeUri, forceLoad);

        // Assert
        Assert.Equal(forceLoad, capturedForceLoad);
    }

    [Theory]
    [InlineData("https://localhost/chat", "https://localhost/", "chat")]
    [InlineData("https://localhost/IrcChat/chat", "https://localhost/IrcChat/", "chat")]
    [InlineData("https://aguacongas.github.io/IrcChat/settings", "https://aguacongas.github.io/IrcChat/", "settings")]
    [InlineData("https://localhost/", "https://localhost/", "")]
    [InlineData("https://localhost/IrcChat/", "https://localhost/IrcChat/", "")]
    public void GetRelativeUri_WithDifferentBaseUris_ShouldReturnCorrectRelativeUri(
        string currentUri, string baseUri, string expectedRelativeUri)
    {
        // Arrange
        var navigationManager = new Mock<NavigationManager>();
        navigationManager.SetupGet(x => x.BaseUri).Returns(baseUri);
        navigationManager.SetupGet(x => x.Uri).Returns(currentUri);

        // Act
        var relativeUri = navigationManager.Object.GetRelativeUri();

        // Assert
        Assert.Equal(expectedRelativeUri, relativeUri);
    }

    [Fact]
    public void GetRelativeUri_WhenCurrentUriDoesNotStartWithBaseUri_ShouldReturnEmpty()
    {
        // Arrange
        var navigationManager = new Mock<NavigationManager>();
        navigationManager.SetupGet(x => x.BaseUri).Returns("https://localhost/IrcChat/");
        navigationManager.SetupGet(x => x.Uri).Returns("https://otherdomain.com/page");

        // Act
        var relativeUri = navigationManager.Object.GetRelativeUri();

        // Assert
        Assert.Equal(string.Empty, relativeUri);
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("/chat")]
    [InlineData("//chat")]
    [InlineData("///chat")]
    public void NavigateToRelative_WithLeadingSlashes_ShouldNormalizeUri(string relativeUri)
    {
        // Arrange
        var baseUri = "https://localhost/IrcChat/";
        var expectedUri = "https://localhost/IrcChat/chat";

        var navigationManager = new Mock<NavigationManager>();
        navigationManager.SetupGet(x => x.BaseUri).Returns(baseUri);

        string? capturedUri = null;

        navigationManager
            .Setup(x => x.NavigateTo(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((uri, _) => capturedUri = uri);

        navigationManager
            .Setup(x => x.ToAbsoluteUri(It.IsAny<string>()))
            .Returns<string>(uri =>
            {
                var cleanUri = uri.TrimStart('/');
                return new Uri(new Uri(baseUri), cleanUri);
            });

        // Act
        navigationManager.Object.NavigateToRelative(relativeUri);

        // Assert
        Assert.Equal(expectedUri, capturedUri);
    }

    [Theory]
    [InlineData("oauth-login?provider=Google&mode=login")]
    [InlineData("reserve?username=testuser")]
    public void NavigateToRelative_WithQueryString_ShouldPreserveQueryString(string relativeUri)
    {
        // Arrange
        var baseUri = "https://localhost/IrcChat/";

        var navigationManager = new Mock<NavigationManager>();
        navigationManager.SetupGet(x => x.BaseUri).Returns(baseUri);

        string? capturedUri = null;

        navigationManager
            .Setup(x => x.NavigateTo(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((uri, _) => capturedUri = uri);

        navigationManager
            .Setup(x => x.ToAbsoluteUri(It.IsAny<string>()))
            .Returns<string>(uri =>
            {
                var cleanUri = uri.TrimStart('/');
                return new Uri(new Uri(baseUri), cleanUri);
            });

        // Act
        navigationManager.Object.NavigateToRelative(relativeUri);

        // Assert
        Assert.NotNull(capturedUri);
        Assert.Contains(relativeUri.Split('?')[0], capturedUri);
        Assert.Contains(relativeUri.Split('?')[1], capturedUri);
    }
}