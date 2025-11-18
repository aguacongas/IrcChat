using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class HealthEndpointTest(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyStatus()
    {
        // Arrange
        var client = factory.CreateClient();
        // Act
        var response = await client.GetAsync("/healthz");
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content, StringComparison.OrdinalIgnoreCase);
    }
}