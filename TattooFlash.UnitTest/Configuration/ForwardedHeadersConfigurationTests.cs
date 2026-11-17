using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TattooFlash.Web.Configuration;

namespace TattooFlash.UnitTest.Configuration;

public class ForwardedHeadersConfigurationTests
{
    [Fact]
    public void Configuration_EnablesOnlyClientIpAndSchemeForwarding()
    {
        // Arrange
        using var services = CreateServices();

        // Act
        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // Assert
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
    }

    [Theory]
    [InlineData("172.30.10.2", true)]
    [InlineData("172.30.10.254", true)]
    [InlineData("172.30.12.2", true)]
    [InlineData("172.30.12.254", true)]
    [InlineData("172.30.9.254", false)]
    [InlineData("172.30.11.2", false)]
    [InlineData("172.30.13.2", false)]
    [InlineData("203.0.113.10", false)]
    public async Task Middleware_AcceptsForwardedHeadersOnlyFromTrustedProxyNetworks(
        string proxyAddress, bool trusted)
    {
        // Arrange
        using var services = CreateServices();
        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(proxyAddress);
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("www.emmamccormicktattoo.com");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.20";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-Host"] = "untrusted.example";
        var nextCalled = false;
        var middleware = new ForwardedHeadersMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLoggerFactory.Instance, options);

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse(trusted ? "198.51.100.20" : proxyAddress),
            context.Connection.RemoteIpAddress);
        Assert.Equal("www.emmamccormicktattoo.com", context.Request.Host.Value);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.ConfigureOptions<ForwardedHeadersConfiguration>();
        return services.BuildServiceProvider();
    }
}