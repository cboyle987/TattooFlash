using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Npgsql;
using TattooFlash.Web.Data;
using TattooFlash.Web.Models;
using TattooFlash.Web.Repositories;

namespace TattooFlash.UnitTest.Repositories;

public class TagRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private readonly Mock<IDbContextFactory<AppDbContext>> _factory = new(MockBehavior.Strict);
    private readonly List<AppDbContext> _operationContexts = [];
    private DbContextOptions<AppDbContext> _options = null!;
    private Exception? _saveException;
    private bool _trackedEntities;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        _factory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var options = _saveException is null ? _options
                    : new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection)
                        .AddInterceptors(new FailedSaveInterceptor(_saveException)).Options;
                var operationContext = new AppDbContext(options);
                operationContext.ChangeTracker.Tracked += (_, _) => _trackedEntities = true;
                _operationContexts.Add(operationContext);
                return Task.FromResult(operationContext);
            });
    }

    public async Task DisposeAsync()
    {
        foreach (var context in _operationContexts)
            await context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetAllTags_ReturnsTagsOrderedByNormalizedNameWithoutTracking()
    {
        // Arrange: display-name ordering intentionally differs from normalized ordering.
        var second = new TagModel { DispalyName = "A display", NormalizedName = "ZEBRA" };
        var first = new TagModel { DispalyName = "Z display", NormalizedName = "ANCHOR" };
        await SeedAsync(second, first);
        var sut = new TagRepository(_factory.Object);

        // Act
        var result = await sut.GetAllTags();

        // Assert
        Assert.Equal(new[] { first.Id, second.Id }, result.Select(t => t.Id));
        Assert.Equal("Z display", result[0].DispalyName);
        Assert.False(_trackedEntities);
        AssertOperationContextDisposed();
    }

    [Fact]
    public async Task GetAllTags_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        var sut = new TagRepository(_factory.Object);

        // Act
        var result = await sut.GetAllTags();

        // Assert
        Assert.Empty(result);
        Assert.False(_trackedEntities);
        AssertOperationContextDisposed();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetTagById_ReturnsMatchingTagOrNullWithoutTracking(bool exists)
    {
        // Arrange
        var tag = Tag("Old School", "OLD SCHOOL");
        await SeedAsync(tag, Tag("Flowers", "FLOWERS"));
        var sut = new TagRepository(_factory.Object);

        // Act
        var result = await sut.GetTagById(exists ? tag.Id : Guid.NewGuid());

        // Assert
        if (exists)
        {
            Assert.NotNull(result);
            Assert.Equal(tag.Id, result.Id);
            Assert.Equal("Old School", result.DispalyName);
            Assert.Equal("OLD SCHOOL", result.NormalizedName);
        }
        else
            Assert.Null(result);
        Assert.False(_trackedEntities);
        AssertOperationContextDisposed();
    }

    [Fact]
    public async Task CreateTag_PersistsNamesAndAssignsAnIdentifier()
    {
        // Arrange
        var tag = Tag("Old School", "OLD SCHOOL");
        var sut = new TagRepository(_factory.Object);

        // Act
        var result = await sut.CreateTag(tag);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(tag, result.Tag);
        Assert.Equal(string.Empty, result.TagSaveError);
        Assert.NotEqual(Guid.Empty, tag.Id);
        AssertOperationContextDisposed();
        await using var verification = new AppDbContext(_options);
        var stored = await verification.Tags.SingleAsync();
        Assert.Equal(tag.Id, stored.Id);
        Assert.Equal("Old School", stored.DispalyName);
        Assert.Equal("OLD SCHOOL", stored.NormalizedName);
    }

    [Fact]
    public async Task UpdateTag_PreservesStoredIdentityAndAssignmentsWhileChangingNames()
    {
        // Arrange
        var existing = Tag("Old School", "OLD SCHOOL");
        var design = new DesignModel { Id = Guid.NewGuid(), Name = "Rose", Tags = [existing] };
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.Add(design);
            await context.SaveChangesAsync();
        }
        var submitted = Tag("Traditional", "TRADITIONAL");
        submitted.Id = Guid.NewGuid();
        var sut = new TagRepository(_factory.Object);

        // Act
        var result = await sut.UpdateTag(existing.Id, submitted);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Tag);
        Assert.Equal(existing.Id, result.Tag.Id);
        Assert.Equal("Traditional", result.Tag.DispalyName);
        Assert.Equal("TRADITIONAL", result.Tag.NormalizedName);
        Assert.Equal(string.Empty, result.TagSaveError);
        AssertOperationContextDisposed();
        await using var verification = new AppDbContext(_options);
        var stored = await verification.Tags.Include(t => t.Designs).SingleAsync();
        Assert.Equal(existing.Id, stored.Id);
        Assert.Equal("Traditional", stored.DispalyName);
        Assert.Equal("TRADITIONAL", stored.NormalizedName);
        Assert.Equal(design.Id, Assert.Single(stored.Designs).Id);
    }

    [Fact]
    public async Task UpdateTag_WhenMissing_ThrowsKeyNotFoundException()
    {
        // Arrange
        var sut = new TagRepository(_factory.Object);

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.UpdateTag(Guid.NewGuid(), Tag("Traditional", "TRADITIONAL")));

        // Assert
        Assert.Equal("Tag not found", exception.Message);
        AssertOperationContextDisposed();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteTag_ReturnsWhetherTheTagExistedAndPreservesOtherTags(bool exists)
    {
        // Arrange
        var target = Tag("Old School", "OLD SCHOOL");
        var other = Tag("Flowers", "FLOWERS");
        await SeedAsync(target, other);
        var sut = new TagRepository(_factory.Object);

        // Act
        var result = await sut.DeleteTag(exists ? target.Id : Guid.NewGuid());

        // Assert
        Assert.Equal(exists, result);
        AssertOperationContextDisposed();
        await using var verification = new AppDbContext(_options);
        Assert.True(await verification.Tags.AnyAsync(t => t.Id == other.Id));
        Assert.Equal(!exists, await verification.Tags.AnyAsync(t => t.Id == target.Id));
    }

    [Fact]
    public async Task DeleteTag_WhenAssigned_RemovesLinksAndPreservesDesignsAndOtherAssignments()
    {
        // Arrange
        var target = Tag("Old School", "OLD SCHOOL");
        var other = Tag("Flowers", "FLOWERS");
        var rose = new DesignModel { Name = "Rose", Tags = [target, other] };
        var anchor = new DesignModel { Name = "Anchor", Tags = [target] };
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.AddRange(rose, anchor);
            await context.SaveChangesAsync();
        }
        var sut = new TagRepository(_factory.Object);

        // Act
        var result = await sut.DeleteTag(target.Id);

        // Assert
        Assert.True(result);
        AssertOperationContextDisposed();
        await using var verification = new AppDbContext(_options);
        Assert.Equal(other.Id, (await verification.Tags.SingleAsync()).Id);
        Assert.Equal(2, await verification.Designs.CountAsync());
        var storedRose = await verification.Designs.Include(d => d.Tags).SingleAsync(d => d.Id == rose.Id);
        var storedAnchor = await verification.Designs.Include(d => d.Tags).SingleAsync(d => d.Id == anchor.Id);
        Assert.Equal(other.Id, Assert.Single(storedRose.Tags).Id);
        Assert.Empty(storedAnchor.Tags);
    }

    [Theory]
    [InlineData(false, "IX_Tags_NormalizedName")]
    [InlineData(true, "IX_Tags_NormalizedName")]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task SaveTag_WhenPostgresReportsDuplicateNormalizedName_ReturnsFailure(
        bool update, string? migrationIndexName)
    {
        // Arrange
        var existing = Tag("Flowers", "FLOWERS");
        await SeedAsync(existing);
        // Exercise both the existing migration's name and the current model's
        // actual name. Testing only the catch's literal would conceal a mismatch.
        var constraintName = migrationIndexName ?? GetNormalizedNameIndexName();
        _saveException = PostgresSaveException(PostgresErrorCodes.UniqueViolation, constraintName);
        var sut = new TagRepository(_factory.Object);
        var submitted = Tag("Old School", "OLD SCHOOL");

        // Act
        var result = update ? await sut.UpdateTag(existing.Id, submitted) : await sut.CreateTag(submitted);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Tag);
        Assert.Equal("Duplicate tag name", result.TagSaveError);
        AssertOperationContextDisposed();
        await using var verification = new AppDbContext(_options);
        Assert.Equal("FLOWERS", (await verification.Tags.SingleAsync()).NormalizedName);
    }

    [Theory]
    [InlineData(false, PostgresErrorCodes.UniqueViolation, "PK_Tags")]
    [InlineData(true, PostgresErrorCodes.UniqueViolation, "PK_Tags")]
    [InlineData(false, PostgresErrorCodes.NotNullViolation, "IX_Tags_NormalizedName")]
    [InlineData(true, PostgresErrorCodes.NotNullViolation, "IX_Tags_NormalizedName")]
    [InlineData(false, null, null)]
    [InlineData(true, null, null)]
    public async Task SaveTag_WhenFailureIsNotDuplicateName_PropagatesOriginalException(
        bool update, string? sqlState, string? constraintName)
    {
        // Arrange
        var existing = Tag("Flowers", "FLOWERS");
        await SeedAsync(existing);
        var expected = sqlState is null ? new DbUpdateException("Unrelated save failure")
            : PostgresSaveException(sqlState, constraintName!);
        _saveException = expected;
        var sut = new TagRepository(_factory.Object);
        var submitted = Tag("Old School", "OLD SCHOOL");

        // Act
        var actual = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            if (update)
                await sut.UpdateTag(existing.Id, submitted);
            else
                await sut.CreateTag(submitted);
        });

        // Assert
        Assert.Same(expected, actual);
        AssertOperationContextDisposed();
        await using var verification = new AppDbContext(_options);
        Assert.Equal("FLOWERS", (await verification.Tags.SingleAsync()).NormalizedName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveTag_WithRealSqliteDuplicate_EnforcesUniquenessWithoutPostgresTranslation(bool update)
    {
        // Arrange
        var first = Tag("Old School", "OLD SCHOOL");
        var second = Tag("Flowers", "FLOWERS");
        await SeedAsync(first, second);
        var sut = new TagRepository(_factory.Object);
        var submitted = Tag("old school", "OLD SCHOOL");

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            if (update)
                await sut.UpdateTag(second.Id, submitted);
            else
                await sut.CreateTag(submitted);
        });

        // Assert: real SQLite errors do not match the PostgreSQL catch filter.
        Assert.Equal(2067, Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode);
        AssertOperationContextDisposed();
        await using var verification = new AppDbContext(_options);
        Assert.Equal(2, await verification.Tags.CountAsync());
        Assert.Equal("FLOWERS", (await verification.Tags.SingleAsync(t => t.Id == second.Id)).NormalizedName);
    }

    [Fact]
    public async Task RepeatedOperations_CreateAndDisposeSeparateContexts()
    {
        // Arrange
        var sut = new TagRepository(_factory.Object);

        // Act
        await sut.GetAllTags();
        await sut.GetAllTags();

        // Assert
        Assert.Equal(2, _operationContexts.Count);
        Assert.NotSame(_operationContexts[0], _operationContexts[1]);
        _factory.Verify(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        foreach (var context in _operationContexts)
            Assert.Throws<ObjectDisposedException>(() => context.Tags.ToList());
    }

    private async Task SeedAsync(params TagModel[] tags)
    {
        await using var context = new AppDbContext(_options);
        context.Tags.AddRange(tags);
        await context.SaveChangesAsync();
    }

    private void AssertOperationContextDisposed()
    {
        var context = Assert.Single(_operationContexts);
        _factory.Verify(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Throws<ObjectDisposedException>(() => context.Tags.ToList());
    }

    private static TagModel Tag(string displayName, string normalizedName) =>
        new() { DispalyName = displayName, NormalizedName = normalizedName };

    private static string GetNormalizedNameIndexName()
    {
        // Constructing the model does not open a connection.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=repository-test.invalid;Database=unused").Options;
        using var context = new AppDbContext(options);
        return context.Model.FindEntityType(typeof(TagModel))!.GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(TattooFlash.Web.Models.TagModel.NormalizedName) })).GetDatabaseName()!;
    }

    private static DbUpdateException PostgresSaveException(string sqlState, string constraintName) =>
        new("Save failed", new PostgresException("Test database error", "ERROR", "ERROR", sqlState,
            constraintName: constraintName));

    // Real relational reads/adds still execute; only saving is replaced to
    // exercise PostgreSQL-specific error translation without a live server.
    private sealed class FailedSaveInterceptor(Exception exception) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(exception);
    }
}