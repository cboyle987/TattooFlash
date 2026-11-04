using Microsoft.EntityFrameworkCore;
using TattooFlash.Web.Data;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Repositories;

public class DesignRepository(IDbContextFactory<AppDbContext> contextFactory, IConfiguration config) : IDesignRepository
{

    private int PageSize
    {
        get
        {
            var pageSize = Int32.Parse(config["DefaultPageSize"] ?? throw new InvalidOperationException("Missing Page Size config."));
            if (pageSize < 0)
            {
                throw new InvalidOperationException("Page size config must be greater than 0");
            }

            return pageSize;
        }
    }

    public async Task<List<DesignModel>> GetAllDesignsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Designs.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<DesignModel?> GetDesignByIdAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Designs.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DesignModel> CreateDesignAsync(DesignModel design)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        design.Id = Guid.NewGuid();
        design.Created = DateTime.UtcNow;
        design.Updated = DateTime.UtcNow;
        context.Designs.Add(design);
        await context.SaveChangesAsync();
        return design;
    }

    public async Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var existing = await context.Designs.SingleOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Design not found");

        // Keep the stored identity even if the submitted model has a different ID.
        existing.Name = design.Name;
        existing.Price = design.Price;
        existing.ImageName = design.ImageName;
        existing.IsAvailable = design.IsAvailable;
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

        var designs = await context.Designs.AsNoTracking().OrderByDescending(x => x.Created).ThenBy(x => x.Id).Skip(skip).Take(PageSize).ToListAsync();

        return new PaginatedDesignResults()
        {
            Designs = designs,
            TotalItems = totalItems,
            PageNumber = pageNumber,
            PageSize = PageSize
        };
    }
}