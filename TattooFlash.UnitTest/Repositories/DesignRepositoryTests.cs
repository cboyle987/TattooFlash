using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using TattooFlash.Web.Configuration;
using TattooFlash.Web.Data;
using TattooFlash.Web.Models;
using TattooFlash.Web.Repositories;

namespace TattooFlash.UnitTest.Repositories;

// Each test owns a relational database. The open connection keeps it alive while
// the repository creates and disposes a fresh context for each operation.
public class DesignRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Mock<IDbContextFactory<AppDbContext>> _factory = new(MockBehavior.Strict);
    private readonly List<AppDbContext> _operationContexts = [];
    private bool _trackedEntities;
    private DbContextOptions<AppDbContext> _options = null!;
    private static readonly DateTime OriginalTimestamp = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        _factory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var operationContext = new AppDbContext(_options);
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
    public async Task GetAllDesignsAsync_ReturnsDesignsOrderedByNameWithoutTracking()
    {
        // Arrange
        var zebra = Design("Zebra");
        var anchor = Design("Anchor");
        await SeedAsync(zebra, anchor);
        var sut = CreateRepository();

        // Act
        var result = await sut.GetAllDesignsAsync(Guid.Empty);

        // Assert
        Assert.Equal(new[] { anchor.Id, zebra.Id }, result.Select(x => x.Id));
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task GetAllDesignsAsync_WithSelectedTag_ReturnsOnlyMatchingDesigns()
    {
        // Arrange
        var selected = new TagModel { DisplayName = "Flowers", NormalizedName = "FLOWERS" };
        var matching = Design("Rose");
        matching.Tags = [selected];
        await SeedAsync(Design("Anchor"), matching);
        var sut = CreateRepository();

        // Act
        var result = await sut.GetAllDesignsAsync(selected.Id);

        // Assert
        Assert.Equal(matching.Id, Assert.Single(result).Id);
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task GetAllDesignsAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        var result = await sut.GetAllDesignsAsync(Guid.Empty);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDesignByIdAsync_WhenFound_ReturnsMatchingUntrackedDesign()
    {
        // Arrange
        var expected = Design("Anchor");
        await SeedAsync(expected, Design("Rose"));
        var sut = CreateRepository();

        // Act
        var result = await sut.GetDesignByIdAsync(expected.Id);

        // Assert
        Assert.NotNull(result);
        AssertDesignEqual(expected, result);
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task GetDesignByIdAsync_WhenMissing_ReturnsNull()
    {
        // Arrange
        await SeedAsync(Design("Rose"));
        var sut = CreateRepository();

        // Act
        var result = await sut.GetDesignByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateDesignAsync_AssignsIdentityAndUtcTimestampsAndPersistsFields()
    {
        // Arrange
        var design = Design("Anchor");
        var submittedId = design.Id;
        var sut = CreateRepository();
        var before = DateTime.UtcNow;

        // Act
        var result = await sut.CreateDesignAsync(design, Array.Empty<Guid>());

        // Assert
        var after = DateTime.UtcNow;
        Assert.Same(design, result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.NotEqual(submittedId, result.Id);
        Assert.InRange(result.Created, before, after);
        Assert.InRange(result.Updated, result.Created, after);
        Assert.Equal(DateTimeKind.Utc, result.Created.Kind);
        Assert.Equal(DateTimeKind.Utc, result.Updated.Kind);
        await using var verification = new AppDbContext(_options);
        AssertDesignEqual(result, await verification.Designs.SingleAsync());
        AssertOperationContextWasDisposed();
    }

    [Fact]
    public async Task CreateDesignAsync_WithNoSelectedTags_PersistsDesignWithoutAssignments()
    {
        // Arrange: having a tag available must not assign it automatically.
        var available = new TagModel { DisplayName = "Flowers", NormalizedName = "FLOWERS" };
        await using (var seed = new AppDbContext(_options))
        {
            seed.Tags.Add(available);
            await seed.SaveChangesAsync();
        }
        var design = Design("Rose");
        var sut = CreateRepository();

        // Act
        var result = await sut.CreateDesignAsync(design, Array.Empty<Guid>());

        // Assert: use a fresh context to verify stored relationships.
        await using var verification = new AppDbContext(_options);
        var stored = await verification.Designs.AsNoTracking().Include(d => d.Tags).SingleAsync();
        AssertDesignEqual(result, stored);
        Assert.Empty(result.Tags);
        Assert.Empty(stored.Tags);
        Assert.Equal(available.Id, (await verification.Tags.SingleAsync()).Id);
        Assert.Equal(0L, await verification.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS Value FROM DesignTags").SingleAsync());
        AssertOperationContextWasDisposed();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateDesignAsync_WithExistingTags_PersistsOnlySelectedAssignmentsWithoutDuplicatingTags(bool repeatId)
    {
        // Arrange: one selected tag already belongs to another design.
        var flowers = new TagModel { DisplayName = "Flowers", NormalizedName = "FLOWERS" };
        var traditional = new TagModel { DisplayName = "Traditional", NormalizedName = "TRADITIONAL" };
        var unselected = new TagModel { DisplayName = "Japanese", NormalizedName = "JAPANESE" };
        var existing = Design("Anchor");
        existing.Tags = [traditional];
        await using (var seed = new AppDbContext(_options))
        {
            seed.Tags.AddRange(flowers, traditional, unselected);
            seed.Designs.Add(existing);
            await seed.SaveChangesAsync();
        }
        var selectedIds = new List<Guid> { flowers.Id, traditional.Id };
        if (repeatId) selectedIds.Add(flowers.Id);
        var design = Design("Rose");
        var sut = CreateRepository();

        // Act
        var result = await sut.CreateDesignAsync(design, selectedIds);

        // Assert: reloading prevents EF's in-memory relationship fixup hiding a missing save.
        await using var verification = new AppDbContext(_options);
        var stored = await verification.Designs.AsNoTracking().Include(d => d.Tags)
            .SingleAsync(d => d.Id == result.Id);
        AssertDesignEqual(result, stored);
        Assert.Equal(new[] { flowers.Id, traditional.Id }.OrderBy(id => id),
            stored.Tags.Select(t => t.Id).OrderBy(id => id));
        var storedTags = await verification.Tags.AsNoTracking().OrderBy(t => t.NormalizedName).ToListAsync();
        Assert.Equal(new[] { flowers.Id, unselected.Id, traditional.Id }, storedTags.Select(t => t.Id));
        Assert.Equal(new[] { "Flowers", "Japanese", "Traditional" }, storedTags.Select(t => t.DisplayName));
        Assert.Equal(new[] { "FLOWERS", "JAPANESE", "TRADITIONAL" }, storedTags.Select(t => t.NormalizedName));
        var storedExisting = await verification.Designs.AsNoTracking().Include(d => d.Tags)
            .SingleAsync(d => d.Id == existing.Id);
        AssertDesignEqual(existing, storedExisting);
        Assert.Equal(traditional.Id, Assert.Single(storedExisting.Tags).Id);
        Assert.Equal(3L, await verification.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS Value FROM DesignTags").SingleAsync());
        AssertOperationContextWasDisposed();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateDesignAsync_UpdatesEditableFieldsAndTagsAndPreservesStoredIdentityAndCreationTime(bool clearTags)
    {
        // Arrange
        var original = Design("Anchor");
        var untouched = Design("Rose");
        var previousTag = new TagModel { DisplayName = "Flowers", NormalizedName = "FLOWERS" };
        var replacementTag = new TagModel { DisplayName = "Traditional", NormalizedName = "TRADITIONAL" };
        original.Tags = [previousTag];
        untouched.Tags = [previousTag, replacementTag];
        await SeedAsync(original, untouched);
        Guid[] selectedTagIds = clearTags ? [] : [replacementTag.Id];
        var submitted = Design("New anchor");
        submitted.Price = 250;
        submitted.ImageName = "replacement.webp";
        submitted.IsAvailable = false;
        submitted.Created = DateTime.UtcNow;
        var sut = CreateRepository();
        var before = DateTime.UtcNow;

        // Act
        var result = await sut.UpdateDesignAsync(original.Id, submitted, selectedTagIds);

        // Assert
        Assert.Equal(original.Id, result.Id);
        Assert.Equal(original.Created, result.Created);
        Assert.Equal(submitted.Name, result.Name);
        Assert.Equal(submitted.Price, result.Price);
        Assert.Equal(submitted.ImageName, result.ImageName);
        Assert.Equal(submitted.IsAvailable, result.IsAvailable);
        Assert.InRange(result.Updated, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, result.Updated.Kind);
        Assert.Equal(selectedTagIds, result.Tags.Select(t => t.Id));
        await using var verification = new AppDbContext(_options);
        var stored = await verification.Designs.AsNoTracking().Include(x => x.Tags).SingleAsync(x => x.Id == original.Id);
        var storedUntouched = await verification.Designs.AsNoTracking().Include(x => x.Tags).SingleAsync(x => x.Id == untouched.Id);
        AssertDesignEqual(result, stored);
        Assert.Equal(selectedTagIds, stored.Tags.Select(t => t.Id));
        AssertDesignEqual(untouched, storedUntouched);
        Assert.Equal(untouched.Tags.Select(t => t.Id).OrderBy(id => id),
            storedUntouched.Tags.Select(t => t.Id).OrderBy(id => id));
        AssertOperationContextWasDisposed();
    }

    [Fact]
    public async Task UpdateDesignAsync_WhenMissing_ThrowsAndDoesNotInsertDesign()
    {
        // Arrange
        var sut = CreateRepository();
        var submitted = Design("Anchor");

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateDesignAsync(Guid.NewGuid(), submitted, []));

        // Assert
        Assert.Equal("Design not found", exception.Message);
        await using var verification = new AppDbContext(_options);
        Assert.Empty(await verification.Designs.ToListAsync());
        AssertOperationContextWasDisposed();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteDesignAsync_DeletesOnlyMatchingDesignAndReturnsWhetherFound(bool exists)
    {
        // Arrange
        var target = Design("Anchor");
        var untouched = Design("Rose");
        await SeedAsync(target, untouched);
        var sut = CreateRepository();

        // Act
        var result = await sut.DeleteDesignAsync(exists ? target.Id : Guid.NewGuid());

        // Assert
        Assert.Equal(exists, result);
        await using var verification = new AppDbContext(_options);
        Assert.Equal(!exists, await verification.Designs.AnyAsync(x => x.Id == target.Id));
        AssertDesignEqual(untouched, await verification.Designs.SingleAsync(x => x.Id == untouched.Id));
        AssertOperationContextWasDisposed();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ToggleAvailabilityAsync_ReversesOnlyMatchingDesignAvailability(bool available)
    {
        // Arrange
        var target = Design("Anchor");
        target.IsAvailable = available;
        var untouched = Design("Rose");
        await SeedAsync(target, untouched);
        var sut = CreateRepository();

        // Act
        await sut.ToggleAvailabilityAsync(target.Id);

        // Assert
        await using var verification = new AppDbContext(_options);
        var stored = await verification.Designs.SingleAsync(x => x.Id == target.Id);
        target.IsAvailable = !available;
        AssertDesignEqual(target, stored);
        AssertDesignEqual(untouched, await verification.Designs.SingleAsync(x => x.Id == untouched.Id));
        AssertOperationContextWasDisposed();
    }

    [Fact]
    public async Task ToggleAvailabilityAsync_WhenMissing_Throws()
    {
        // Arrange
        var untouched = Design("Rose");
        await SeedAsync(untouched);
        var sut = CreateRepository();

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.ToggleAvailabilityAsync(Guid.NewGuid()));

        // Assert
        Assert.Equal("Design not found", exception.Message);
        await using var verification = new AppDbContext(_options);
        AssertDesignEqual(untouched, await verification.Designs.SingleAsync());
        AssertOperationContextWasDisposed();
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    public async Task GetDesignsAsync_ReturnsRequestedPageInStableNewestFirstOrder(int page, int expectedCount)
    {
        // Arrange
        var designs = Enumerable.Range(1, 5).Select(i =>
        {
            var design = Design($"Design {6 - i}");
            design.Id = Guid.Parse($"00000000-0000-0000-0000-{i:D12}");
            // Tie spans a page boundary, so missing the ID tie-breaker affects paging.
            design.Created = OriginalTimestamp.AddDays(i <= 3 ? 1 : 0);
            return design;
        }).ToArray();
        await SeedAsync(designs.Reverse().ToArray());
        var expected = designs.OrderByDescending(x => x.Created).ThenBy(x => x.Id)
            .Skip((page - 1) * 2).Take(2).Select(x => x.Id).ToArray();
        var sut = CreateRepository(2);

        // Act
        var result = await sut.GetDesignsAsync(page, Guid.Empty);

        // Assert
        Assert.Equal(expectedCount, result.Designs.Count);
        Assert.Equal(expected, result.Designs.Select(x => x.Id));
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(page, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task GetDesignsAsync_WithSelectedTag_FiltersBeforePagingAndLoadsAllTags()
    {
        // Arrange
        var selected = new TagModel { DisplayName = "Flowers", NormalizedName = "FLOWERS" };
        var other = new TagModel { DisplayName = "Traditional", NormalizedName = "TRADITIONAL" };
        var newest = Design("Anchor");
        newest.Created = OriginalTimestamp.AddDays(2);
        newest.Tags = [other];
        var firstMatch = Design("Rose");
        firstMatch.Created = OriginalTimestamp.AddDays(1);
        firstMatch.Tags = [selected];
        var secondMatch = Design("Peony");
        secondMatch.Tags = [selected, other];
        await SeedAsync(newest, firstMatch, secondMatch);
        var sut = CreateRepository(pageSize: 1);

        // Act
        var result = await sut.GetDesignsAsync(2, selected.Id);

        // Assert
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.PageNumber);
        var design = Assert.Single(result.Designs);
        Assert.Equal(secondMatch.Id, design.Id);
        Assert.Equal(new[] { selected.Id, other.Id }.OrderBy(id => id),
            design.Tags.Select(tag => tag.Id).OrderBy(id => id));
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task GetDesignsAsync_WhenSelectedTagHasNoMatches_ReturnsEmptyPage()
    {
        // Arrange
        await SeedAsync(Design("Anchor"));
        var sut = CreateRepository();

        // Act
        var result = await sut.GetDesignsAsync(1, Guid.NewGuid());

        // Assert
        Assert.Empty(result.Designs);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetDesignsAsync_WhenEmpty_ReturnsEmptyPageWithMetadata()
    {
        // Arrange
        var sut = CreateRepository(3);

        // Act
        var result = await sut.GetDesignsAsync(1, Guid.Empty);

        // Assert
        Assert.Empty(result.Designs);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(3, result.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetDesignsAsync_InvalidPageNumber_ThrowsBeforeCreatingContext(int page)
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => sut.GetDesignsAsync(page, Guid.Empty));

        // Assert
        Assert.Equal("Invalid page number", exception.Message);
        _factory.Verify(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDesignsAsync_WithPageSizeOne_ReturnsOneDesignPerPage()
    {
        // Arrange
        var first = Design("Anchor");
        first.Created = OriginalTimestamp.AddDays(1);
        var second = Design("Rose");
        await SeedAsync(first, second);
        var sut = CreateRepository(1);

        // Act
        var result = await sut.GetDesignsAsync(2, Guid.Empty);

        // Assert
        Assert.Equal(second.Id, Assert.Single(result.Designs).Id);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.TotalPages);
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task RepeatedOperations_CreateSeparateContexts()
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        await sut.GetAllDesignsAsync(Guid.Empty);
        await sut.GetAllDesignsAsync(Guid.Empty);

        // Assert
        Assert.Equal(2, _operationContexts.Count);
        Assert.NotSame(_operationContexts[0], _operationContexts[1]);
        _factory.Verify(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        foreach (var context in _operationContexts)
            Assert.Throws<ObjectDisposedException>(() => context.Designs.ToList());
    }

    private DesignRepository CreateRepository(int pageSize = 2) =>
        new(_factory.Object, Options.Create(new PaginationConfiguration { DefaultPageSize = pageSize }));

    private async Task SeedAsync(params DesignModel[] designs)
    {
        await using var context = new AppDbContext(_options);
        context.Designs.AddRange(designs);
        await context.SaveChangesAsync();
    }

    private void AssertReadContextWasUntrackedAndDisposed()
    {
        var context = Assert.Single(_operationContexts);
        // Disposing a context clears its tracked entries. The Tracked event is
        // captured when creating each context, so a tracked query cannot pass accidentally.
        Assert.False(_trackedEntities);
        Assert.Throws<ObjectDisposedException>(() => context.Designs.ToList());
    }

    private void AssertOperationContextWasDisposed()
    {
        var context = Assert.Single(_operationContexts);
        Assert.Throws<ObjectDisposedException>(() => context.Designs.ToList());
    }

    private static DesignModel Design(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Price = 100,
        ImageName = "original.webp",
        IsAvailable = true,
        Created = OriginalTimestamp,
        Updated = OriginalTimestamp
    };

    private static void AssertDesignEqual(DesignModel expected, DesignModel actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Price, actual.Price);
        Assert.Equal(expected.ImageName, actual.ImageName);
        Assert.Equal(expected.IsAvailable, actual.IsAvailable);
        Assert.Equal(expected.Created, actual.Created);
        Assert.Equal(expected.Updated, actual.Updated);
    }
}