using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TattooFlash.Web.Endpoints;

namespace TattooFlash.UnitTest.Endpoints;

public class LoginEndpointTests
{
    [Theory]
    [InlineData("/", "")]
    [InlineData("/management", "")]
    [InlineData("/management?page=2#designs", "")]
    [InlineData("/management?next=https://example.com", "")]
    [InlineData("~/management", "")]
    [InlineData("~/management", "/tattooflash")]
    public async Task HandleAsync_LocalDestination_PreservesReturnPath(string destination, string pathBase)
    {
        // Arrange
        var expected = destination.StartsWith("~/", StringComparison.Ordinal)
            ? pathBase + destination[1..]
            : destination;

        // Act and Assert
        await AssertChallengeAsync(destination, pathBase, expected);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/management")]
    [InlineData("//example.com")]
    [InlineData("///example.com")]
    [InlineData("/\\example.com")]
    [InlineData("\\\\example.com")]
    [InlineData("~//example.com")]
    [InlineData("~/\\example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("management")]
    [InlineData("?next=/management")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/management\r\nLocation: https://example.com")]
    [InlineData("/management\0")]
    public async Task HandleAsync_UnsafeDestination_FallsBackToHome(string destination)
    {
        // Act and Assert
        await AssertChallengeAsync(destination, "", "/");
    }

    [Fact]
    public async Task HandleAsync_OmittedDestination_ReturnsToHome()
    {
        // Act and Assert
        await AssertChallengeAsync(null, "", "/");
    }

    private static async Task AssertChallengeAsync(string? destination, string pathBase, string expected)
    {
        // Arrange
        var authentication = new Mock<IAuthenticationService>(MockBehavior.Strict);
        using var services = new ServiceCollection()
            .AddSingleton(authentication.Object)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.PathBase = pathBase;
        authentication.Setup(x => x.ChallengeAsync(
                context,
                Auth0Constants.AuthenticationScheme,
                It.Is<AuthenticationProperties>(properties => properties.RedirectUri == expected)))
            .Returns(Task.CompletedTask);

        // Act
        if (destination is null)
            await LoginEndpoint.HandleAsync(context);
        else
            await LoginEndpoint.HandleAsync(context, destination);

        // Assert
        authentication.Verify(x => x.ChallengeAsync(
            context,
            Auth0Constants.AuthenticationScheme,
            It.Is<AuthenticationProperties>(properties => properties.RedirectUri == expected)), Times.Once);
        authentication.VerifyNoOtherCalls();
    }
}