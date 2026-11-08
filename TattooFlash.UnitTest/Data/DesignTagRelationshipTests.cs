using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TattooFlash.Web.Data;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Data;

// Each test owns one relational database. Separate contexts ensure assertions
// verify persisted data rather than EF's in-memory relationship fixup.
public class DesignTagRelationshipTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private DbContextOptions<AppDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task SaveDesign_WithoutTags_PreservesItsIdentityAndFields()
    {
        // Arrange
        var design = new DesignModel
        {
            Id = Guid.NewGuid(),
            Name = "Rose",
            Price = 120,
            ImageName = "rose.webp",
            IsAvailable = true,
            Created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Updated = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.Add(design);
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verification = new AppDbContext(_options);
        var stored = await verification.Designs.AsNoTracking().Include(d => d.Tags).SingleAsync();
        Assert.Equal(design.Id, stored.Id);
        Assert.Equal("Rose", stored.Name);
        Assert.Equal(120, stored.Price);
        Assert.Equal("rose.webp", stored.ImageName);
        Assert.True(stored.IsAvailable);
        Assert.Equal(design.Created, stored.Created);
        Assert.Equal(design.Updated, stored.Updated);
        Assert.Empty(stored.Tags);
    }

    [Fact]
    public async Task SaveDesigns_WithSharedAndMultipleTags_RoundTripsBothSidesAndDisplaySpelling()
    {
        // Arrange
        var shared = new TagModel { DispalyName = "Old School", NormalizedName = "OLD SCHOOL" };
        var other = new TagModel { DispalyName = "Japanese", NormalizedName = "JAPANESE" };
        var rose = new DesignModel { Name = "Rose", Tags = [shared, other] };
        var anchor = new DesignModel { Name = "Anchor", Tags = [shared] };

        // Act
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.AddRange(rose, anchor);
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verification = new AppDbContext(_options);
        var storedRose = await verification.Designs.AsNoTracking().Include(d => d.Tags)
            .SingleAsync(d => d.Id == rose.Id);
        Assert.Equal(2, storedRose.Tags.Count);
        Assert.Contains(storedRose.Tags, t => t.Id == shared.Id);
        Assert.Contains(storedRose.Tags, t => t.Id == other.Id);

        var storedShared = await verification.Set<TagModel>().AsNoTracking().Include(t => t.Designs)
            .SingleAsync(t => t.Id == shared.Id);
        Assert.NotEqual(Guid.Empty, storedShared.Id);
        Assert.Equal("Old School", storedShared.DispalyName);
        Assert.Equal("OLD SCHOOL", storedShared.NormalizedName);
        Assert.Equal(2, storedShared.Designs.Count);
        Assert.Contains(storedShared.Designs, d => d.Id == rose.Id);
        Assert.Contains(storedShared.Designs, d => d.Id == anchor.Id);
        Assert.Equal(2, await verification.Set<TagModel>().CountAsync());
    }

    [Fact]
    public async Task DeleteTag_RemovesOnlyItsAssignmentsAndPreservesDesignsAndOtherTags()
    {
        // Arrange
        var removed = new TagModel { DispalyName = "Old School", NormalizedName = "OLD SCHOOL" };
        var retained = new TagModel { DispalyName = "Flowers", NormalizedName = "FLOWERS" };
        var rose = new DesignModel { Name = "Rose", Tags = [removed, retained] };
        var anchor = new DesignModel { Name = "Anchor", Tags = [removed] };
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.AddRange(rose, anchor);
            await context.SaveChangesAsync();
        }

        // Act: no relationships are loaded, so the database must perform the cascade.
        await using (var context = new AppDbContext(_options))
            await context.Set<TagModel>().Where(t => t.Id == removed.Id).ExecuteDeleteAsync();

        // Assert
        await using var verification = new AppDbContext(_options);
        Assert.Equal(2, await verification.Designs.CountAsync());
        Assert.Equal(retained.Id, (await verification.Set<TagModel>().SingleAsync()).Id);
        var storedRose = await verification.Designs.Include(d => d.Tags).SingleAsync(d => d.Id == rose.Id);
        var storedAnchor = await verification.Designs.Include(d => d.Tags).SingleAsync(d => d.Id == anchor.Id);
        Assert.Equal(retained.Id, Assert.Single(storedRose.Tags).Id);
        Assert.Empty(storedAnchor.Tags);
        Assert.Equal(1L, await CountAssignmentsAsync(verification));
    }

    [Fact]
    public async Task DeleteDesign_RemovesOnlyItsAssignmentsAndPreservesSharedAndUnusedTags()
    {
        // Arrange
        var shared = new TagModel { DispalyName = "Old School", NormalizedName = "OLD SCHOOL" };
        var exclusive = new TagModel { DispalyName = "Flowers", NormalizedName = "FLOWERS" };
        var rose = new DesignModel { Name = "Rose", Tags = [shared, exclusive] };
        var anchor = new DesignModel { Name = "Anchor", Tags = [shared] };
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.AddRange(rose, anchor);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new AppDbContext(_options))
            await context.Designs.Where(d => d.Id == rose.Id).ExecuteDeleteAsync();

        // Assert
        await using var verification = new AppDbContext(_options);
        Assert.Equal(2, await verification.Set<TagModel>().CountAsync());
        var remaining = await verification.Designs.Include(d => d.Tags).SingleAsync();
        Assert.Equal(anchor.Id, remaining.Id);
        Assert.Equal(shared.Id, Assert.Single(remaining.Tags).Id);
        var unused = await verification.Set<TagModel>().Include(t => t.Designs).SingleAsync(t => t.Id == exclusive.Id);
        Assert.Empty(unused.Designs);
        Assert.Equal(1L, await CountAssignmentsAsync(verification));
    }

    [Fact]
    public async Task RemoveAssignment_PreservesBothParentsAndOtherAssignments()
    {
        // Arrange
        var tag = new TagModel { DispalyName = "Old School", NormalizedName = "OLD SCHOOL" };
        var rose = new DesignModel { Name = "Rose", Tags = [tag] };
        var anchor = new DesignModel { Name = "Anchor", Tags = [tag] };
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.AddRange(rose, anchor);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new AppDbContext(_options))
        {
            var stored = await context.Designs.Include(d => d.Tags).SingleAsync(d => d.Id == rose.Id);
            stored.Tags.Clear();
            await context.SaveChangesAsync();
        }

        // Assert
        await using var verification = new AppDbContext(_options);
        Assert.Equal(2, await verification.Designs.CountAsync());
        var storedTag = await verification.Set<TagModel>().Include(t => t.Designs).SingleAsync();
        Assert.Equal(tag.Id, storedTag.Id);
        Assert.Equal(anchor.Id, Assert.Single(storedTag.Designs).Id);
        Assert.Equal(1L, await CountAssignmentsAsync(verification));
    }

    [Fact]
    public async Task SaveTag_WithDuplicateNormalizedName_IsRejectedByTheDatabase()
    {
        // Arrange
        await using (var context = new AppDbContext(_options))
        {
            context.Add(new TagModel { DispalyName = "Old School", NormalizedName = "OLD SCHOOL" });
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new AppDbContext(_options))
        {
            context.Add(new TagModel { DispalyName = "old school", NormalizedName = "OLD SCHOOL" });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

            // Assert: SQLite extended error 2067 means UNIQUE constraint failed.
            Assert.Equal(2067, Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode);
        }

        await using var verification = new AppDbContext(_options);
        Assert.Equal("Old School", (await verification.Set<TagModel>().SingleAsync()).DispalyName);
    }

    [Fact]
    public async Task InsertDuplicateAssignment_IsRejectedByTheDatabase()
    {
        // Arrange
        var tag = new TagModel { DispalyName = "Old School", NormalizedName = "OLD SCHOOL" };
        var design = new DesignModel { Name = "Rose", Tags = [tag] };
        await using (var context = new AppDbContext(_options))
        {
            context.Designs.Add(design);
            await context.SaveChangesAsync();
        }

        // Act: bypass EF's deduplication to exercise the join table's primary key.
        await using var verification = new AppDbContext(_options);
        var exception = await Assert.ThrowsAsync<SqliteException>(() =>
            verification.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO DesignTags (DesignsId, TagsId) VALUES ({design.Id}, {tag.Id})"));

        // Assert: SQLite extended error 1555 means PRIMARY KEY constraint failed.
        Assert.Equal(1555, exception.SqliteExtendedErrorCode);
        Assert.Equal(1L, await CountAssignmentsAsync(verification));
    }

    private static Task<long> CountAssignmentsAsync(AppDbContext context) =>
        context.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM DesignTags").SingleAsync();
}