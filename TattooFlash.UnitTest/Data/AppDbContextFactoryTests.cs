using System.Data;
using Microsoft.EntityFrameworkCore;
using TattooFlash.Web.Data;

namespace TattooFlash.UnitTest.Data;

// Environment variables belong to the process, so these tests must not overlap other collections.
[CollectionDefinition(nameof(AppDbContextFactoryEnvironmentCollection), DisableParallelization = true)]
public sealed class AppDbContextFactoryEnvironmentCollection;

[Collection(nameof(AppDbContextFactoryEnvironmentCollection))]
public class AppDbContextFactoryTests
{
    [Theory]
    [InlineData(
        "Host=factory-test.invalid;Port=5433;Database=factory_test;Username=test_user;Password=test-only-password",
        "Host=factory-test.invalid;Port=5433;Database=factory_test;Username=test_user;Password=test-only-password")]
    [InlineData(
        null,
        "Host=localhost;Port=5432;Database=tattooflash_design_time;Username=design_time;Password=not-a-real-password")]
    public async Task CreateDbContext_UsesConfiguredConnectionOrDesignTimeFallbackWithoutOpeningConnection(
        string? configuredConnection, string expectedConnection)
    {
        // Arrange
        const string variable = "ConnectionStrings__DefaultConnection";
        var originalValue = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, configuredConnection);
            var factory = new AppDbContextFactory();

            // Act
            await using var context = factory.CreateDbContext([]);

            // Assert: inspect configuration only; no database operation is performed.
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Equal(expectedConnection, context.Database.GetConnectionString());
            Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, originalValue);
        }
    }
}
