using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TattooFlash.Web.Configuration;

namespace TattooFlash.UnitTest.Configuration;

public class PaginationConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task InvalidPositivePageSize_PreventsStartupWithAClearConfigurationError(string? pageSize)
    {
        // Arrange
        using var host = CreateHost(pageSize);

        // Act: validation runs even when no repository or options consumer is resolved.
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        // Assert
        Assert.Contains("DefaultPageSize must be a positive integer (at least 1).", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2147483648")]
    public async Task UnparseablePageSize_PreventsStartupAndIdentifiesTheConfigurationKey(string pageSize)
    {
        // Arrange
        using var host = CreateHost(pageSize);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        // Assert
        Assert.Contains("DefaultPageSize", exception.Message);
        Assert.Contains("System.Int32", exception.Message);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("12", 12)]
    [InlineData("2147483647", int.MaxValue)]
    public async Task PositivePageSize_StartsAndReusesTheValidatedOptions(string pageSize, int expected)
    {
        // Arrange
        using var host = CreateHost(pageSize);

        // Act
        await host.StartAsync();
        var options = host.Services.GetRequiredService<IOptions<PaginationConfiguration>>();
        var validated = options.Value;

        // Assert
        Assert.Equal(expected, validated.DefaultPageSize);
        Assert.Same(validated, options.Value);
        await host.StopAsync();
    }

    private static IHost CreateHost(string? pageSize)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(pageSize is null
            ? []
            : new Dictionary<string, string?> { ["DefaultPageSize"] = pageSize });
        builder.Services.AddOptions<PaginationConfiguration>()
            .Bind(builder.Configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return builder.Build();
    }
}