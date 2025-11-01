using Xunit;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using IrcChat.Client.Services;
using System.Threading.Tasks;

namespace IrcChat.Client.Tests.Services;

public class LocalStorageServiceTests
{
    [Fact]
    public async Task GetItemAsync_ShouldReturnValueFromJsRuntime()
    {
        var js = new Mock<IJSRuntime>(MockBehavior.Strict);
        js.Setup(x => x.InvokeAsync<string?>(
            "localStorageHelper.getItem",
            It.IsAny<object[]>()))
          .ReturnsAsync("test-value");

        var service = new LocalStorageService(js.Object);

        var result = await service.GetItemAsync("key");
        result.Should().Be("test-value");
    }
}