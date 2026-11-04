using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
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
        var result = await sut.GetAllDesignsAsync();

        // Assert
        Assert.Equal(new[] { anchor.Id, zebra.Id }, result.Select(x => x.Id));
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task GetAllDesignsAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        var result = await sut.GetAllDesignsAsync();

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
        var result = await sut.CreateDesignAsync(design);

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
    public async Task UpdateDesignAsync_UpdatesEditableFieldsAndPreservesStoredIdentityAndCreationTime()
    {
        // Arrange
        var original = Design("Anchor");
        var untouched = Design("Rose");
        await SeedAsync(original, untouched);
        var submitted = Design("New anchor");
        submitted.Price = 250;
        submitted.ImageName = "replacement.webp";
        submitted.IsAvailable = false;
        submitted.Created = DateTime.UtcNow;
        var sut = CreateRepository();
        var before = DateTime.UtcNow;

        // Act
        var result = await sut.UpdateDesignAsync(original.Id, submitted);

        // Assert
        Assert.Equal(original.Id, result.Id);
        Assert.Equal(original.Created, result.Created);
        Assert.Equal(submitted.Name, result.Name);
        Assert.Equal(submitted.Price, result.Price);
        Assert.Equal(submitted.ImageName, result.ImageName);
        Assert.Equal(submitted.IsAvailable, result.IsAvailable);
        Assert.InRange(result.Updated, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, result.Updated.Kind);
        await using var verification = new AppDbContext(_options);
        AssertDesignEqual(result, await verification.Designs.SingleAsync(x => x.Id == original.Id));
        AssertDesignEqual(untouched, await verification.Designs.SingleAsync(x => x.Id == untouched.Id));
        AssertOperationContextWasDisposed();
    }

    [Fact]
    public async Task UpdateDesignAsync_WhenMissing_ThrowsAndDoesNotInsertDesign()
    {
        // Arrange
        var sut = CreateRepository();
        var submitted = Design("Anchor");

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateDesignAsync(Guid.NewGuid(), submitted));

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
        var sut = CreateRepository("2");

        // Act
        var result = await sut.GetDesignsAsync(page);

        // Assert
        Assert.Equal(expectedCount, result.Designs.Count);
        Assert.Equal(expected, result.Designs.Select(x => x.Id));
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(page, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        AssertReadContextWasUntrackedAndDisposed();
    }

    [Fact]
    public async Task GetDesignsAsync_WhenEmpty_ReturnsEmptyPageWithMetadata()
    {
        // Arrange
        var sut = CreateRepository("3");

        // Act
        var result = await sut.GetDesignsAsync(1);

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
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => sut.GetDesignsAsync(page));

        // Assert
        Assert.Equal("Invalid page number", exception.Message);
        _factory.Verify(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null, typeof(InvalidOperationException))]
    [InlineData("-1", typeof(InvalidOperationException))]
    [InlineData("invalid", typeof(FormatException))]
    [InlineData("2147483648", typeof(OverflowException))]
    public async Task GetDesignsAsync_InvalidPageSizeConfiguration_Throws(string? pageSize, Type exceptionType)
    {
        // Arrange
        var sut = CreateRepository(pageSize);

        // Act
        var exception = await Record.ExceptionAsync(() => sut.GetDesignsAsync(1));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType(exceptionType, exception);
        AssertOperationContextWasDisposed();
    }

    [Fact]
    public async Task RepeatedOperations_CreateSeparateContexts()
    {
        // Arrange
        var sut = CreateRepository();

        // Act
        await sut.GetAllDesignsAsync();
        await sut.GetAllDesignsAsync();

        // Assert
        Assert.Equal(2, _operationContexts.Count);
        Assert.NotSame(_operationContexts[0], _operationContexts[1]);
        _factory.Verify(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        foreach (var context in _operationContexts)
            Assert.Throws<ObjectDisposedException>(() => context.Designs.ToList());
    }

    private DesignRepository CreateRepository(string? pageSize = "2")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DefaultPageSize"] = pageSize
        }).Build();
        return new DesignRepository(_factory.Object, config);
    }

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