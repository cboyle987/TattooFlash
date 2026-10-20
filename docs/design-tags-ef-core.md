# Design tags with EF Core code first

A shared tag catalogue allows a design to have multiple tags and each tag to belong to multiple designs. This is a many-to-many relationship.

Start with collection properties on both entities and let EF Core manage the join table. An explicit `DesignTag` C# class is only needed if the relationship needs additional data, such as when a tag was assigned.

Reference: [EF Core many-to-many relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many).

## Entities

These examples assume integer primary keys. Keep the other existing design properties when introducing the relationship.

```csharp
public class DesignModel
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

public class Tag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }

    public ICollection<DesignModel> Designs { get; set; }
        = new List<DesignModel>();
}
```

## DbContext configuration

```csharp
using Microsoft.EntityFrameworkCore;

public class TattooFlashDbContext(
    DbContextOptions<TattooFlashDbContext> options) : DbContext(options)
{
    public DbSet<DesignModel> Designs => Set<DesignModel>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DesignModel>()
            .HasMany(d => d.Tags)
            .WithMany(t => t.Designs)
            .UsingEntity("DesignTags");

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(t => t.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(t => t.NormalizedName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(t => t.NormalizedName)
                .IsUnique();
        });
    }
}
```

After configuring a database provider and creating a migration, the model produces three tables:

| Table | Purpose |
| --- | --- |
| `Designs` | Stores designs |
| `Tags` | Stores the shared catalogue |
| `DesignTags` | Stores design/tag pairs with foreign keys and a composite primary key |

The composite primary key prevents the same tag being assigned to the same design more than once.

## Assigning an existing tag

Load the design and tag from the same context, then add the tag to the design's collection:

```csharp
var design = await db.Designs
    .Include(d => d.Tags)
    .SingleAsync(d => d.Id == designId);

var tag = await db.Tags.SingleAsync(t => t.Id == tagId);

if (!design.Tags.Any(t => t.Id == tag.Id))
{
    design.Tags.Add(tag);
    await db.SaveChangesAsync();
}
```

EF inserts the relationship into `DesignTags`. Removing a tag from the loaded `design.Tags` collection and saving removes only that relationship; the shared tag remains in the catalogue.

## Creating tags

Keep a display name and populate `NormalizedName` consistently, for example:

```csharp
var displayName = name.Trim();
var normalizedName = displayName.ToUpperInvariant();
```

Validate that the name is non-empty and within the configured length. Search for an existing tag by its normalized name before creating one. The unique database index is the final safeguard against duplicates, including concurrent requests; the save flow should handle a uniqueness conflict by reusing the existing tag.

In the design form, selecting an existing suggestion references its tag ID. Newly entered tags can remain pending until the design is saved, when new catalogue entries and their relationships are persisted together.

Removing a chip from a design removes the relationship. Renaming or deleting a shared catalogue tag should be a separate management action.

## Filtering designs

The following examples assume `selectedTagIds` contains the IDs chosen in the filter.

### Match any selected tag

This returns designs tagged with at least one selected tag. An empty selection returns all designs.

```csharp
var query = db.Designs.AsQueryable();

if (selectedTagIds.Count > 0)
{
    query = query.Where(d =>
        d.Tags.Any(t => selectedTagIds.Contains(t.Id)));
}

var designs = await query.ToListAsync();
```

### Match all selected tags

Each selected tag adds a condition, narrowing the results. An empty selection returns all designs.

```csharp
var query = db.Designs.AsQueryable();

foreach (var tagId in selectedTagIds.Distinct())
{
    var requiredTagId = tagId;

    query = query.Where(d =>
        d.Tags.Any(t => t.Id == requiredTagId));
}

var designs = await query.ToListAsync();
```

`Include(d => d.Tags)` is needed when the returned entities must also contain their tags for display. Filtering through the navigation property alone does not require `Include`.

## Suggested tag input

Compose a reusable tag input from a MudBlazor autocomplete and removable chips:

1. Search the shared catalogue as the user types, excluding tags already selected.
2. Selecting an existing tag adds a chip and clears the input.
3. Offer an explicit create action when the user enters a new tag name.
4. Closing a chip removes the tag from the current design's selection.
5. Save the design, new tags, and relationships together.

This document describes a proposed implementation; it does not add EF Core persistence to the application.
