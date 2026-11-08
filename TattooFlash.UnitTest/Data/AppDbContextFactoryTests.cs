using System.Data;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using TattooFlash.Web.Data;

namespace TattooFlash.UnitTest.Data;

// Environment variables belong to the process, so these tests must not overlap other collections.
[CollectionDefinition(nameof(AppDbContextFactoryEnvironmentCollection), DisableParallelization = true)]
public sealed class AppDbContextFactoryEnvironmentCollection;

[Collection(nameof(AppDbContextFactoryEnvironmentCollection))]
public class AppDbContextFactoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CreateDbContext_UsesEnvironmentBeforeUserSecretsThenFallbackWithoutOpeningConnection(
        bool hasUserSecret, bool hasEnvironmentConnection)
    {
        // Arrange
        const string secretConnection = "Host=secrets-test.invalid;Database=secrets_test;Username=test_user;Password=test-only-secret";
        const string environmentConnection = "Host=environment-test.invalid;Database=environment_test;Username=test_user;Password=test-only-environment";
        const string fallbackConnection = "Host=localhost;Port=5432;Database=tattooflash_design_time;Username=design_time;Password=not-a-real-password";

        // APPDATA is checked first by the secrets provider, including on Linux.
        // Redirect it rather than reading or modifying the developer's secrets.
        // Clear alternate environment spellings/providers that map to the same key.
        string[] variables =
        [
            "APPDATA",
            "ConnectionStrings__DefaultConnection",
            "ConnectionStrings:DefaultConnection",
            "CUSTOMCONNSTR_DefaultConnection",
            "SQLCONNSTR_DefaultConnection",
            "SQLAZURECONNSTR_DefaultConnection",
            "MYSQLCONNSTR_DefaultConnection",
            "POSTGRESQLCONNSTR_DefaultConnection"
        ];
        var originalValues = variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        var temporaryDirectory = Directory.CreateTempSubdirectory("TattooFlash-factory-tests-");
        try
        {
            foreach (var variable in variables)
                Environment.SetEnvironmentVariable(variable, null);

            Environment.SetEnvironmentVariable("APPDATA", temporaryDirectory.FullName);
            if (hasUserSecret)
            {
                var secretsId = typeof(AppDbContextFactory).Assembly
                    .GetCustomAttribute<UserSecretsIdAttribute>()!.UserSecretsId;
                var secretsPath = PathHelper.GetSecretsPathFromSecretsId(secretsId);
                Directory.CreateDirectory(Path.GetDirectoryName(secretsPath)!);
                var json = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = secretConnection
                });
                await File.WriteAllTextAsync(secretsPath, json);
            }

            if (hasEnvironmentConnection)
                Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", environmentConnection);

            var factory = new AppDbContextFactory();
            var expectedConnection = hasEnvironmentConnection ? environmentConnection
                : hasUserSecret ? secretConnection : fallbackConnection;

            // Act
            await using var context = factory.CreateDbContext([]);

            // Assert: inspect configuration only; no database operation is performed.
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Equal(expectedConnection, context.Database.GetConnectionString());
            Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
        }
        finally
        {
            foreach (var (variable, value) in originalValues)
                Environment.SetEnvironmentVariable(variable, value);

            temporaryDirectory.Delete(recursive: true);
        }
    }
}
