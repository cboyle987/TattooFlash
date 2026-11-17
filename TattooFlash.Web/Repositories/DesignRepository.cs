using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TattooFlash.Web.Configuration;
using TattooFlash.Web.Data;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Repositories;

public class DesignRepository(IDbContextFactory<AppDbContext> contextFactory, IOptions<PaginationConfiguration> paginationOptions) : IDesignRepository
{
    private readonly int PageSize = paginationOptions.Value.DefaultPageSize;

    public async Task<List<DesignModel>> GetAllDesignsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Designs.AsNoTracking().Include(x => x.Tags).OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<DesignModel?> GetDesignByIdAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Designs.AsNoTracking().Include(x => x.Tags).SingleOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DesignModel> CreateDesignAsync(DesignModel design, IReadOnlyCollection<Guid> selectedTagIds)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        design.Tags = await context.Tags.Where(t => selectedTagIds.Contains(t.Id)).ToListAsync();
        design.Id = Guid.NewGuid();
        design.Created = DateTime.UtcNow;
        design.Updated = DateTime.UtcNow;
        context.Designs.Add(design);
        await context.SaveChangesAsync();
        return design;
    }

    public async Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design, IReadOnlyCollection<Guid> selectedTagIds)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var existing = await context.Designs.Include(x => x.Tags).SingleOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Design not found");

        // Keep the stored identity even if the submitted model has a different ID.
        existing.Name = design.Name;
        existing.Price = design.Price;
        existing.ImageName = design.ImageName;
        existing.IsAvailable = design.IsAvailable;
        existing.Tags = await context.Tags.Where(t => selectedTagIds.Contains(t.Id)).ToListAsync();
        existing.Updated = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteDesignAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Designs.Where(d => d.Id == id).ExecuteDeleteAsync() > 0;
    }

    public async Task ToggleAvailabilityAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        // Toggle in the database so simultaneous requests do not overwrite each other.
        var affected = await context.Designs.Where(d => d.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.IsAvailable, d => !d.IsAvailable));

        if (affected == 0)
            throw new KeyNotFoundException("Design not found");
    }

    public async Task<PaginatedDesignResults> GetDesignsAsync(int pageNumber)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentException("Invalid page number");
        }

        await using var context = await contextFactory.CreateDbContextAsync();

        var totalItems = await context.Designs.AsNoTracking().CountAsync();

        var skip = (pageNumber - 1) * PageSize;

        var designs = await context.Designs.AsNoTracking().Include(x => x.Tags).OrderByDescending(x => x.Created).ThenBy(x => x.Id).Skip(skip).Take(PageSize).ToListAsync();

        return new PaginatedDesignResults()
        {
            Designs = designs,
            TotalItems = totalItems,
            PageNumber = pageNumber,
            PageSize = PageSize
        };
    }
}