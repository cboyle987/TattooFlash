using Microsoft.EntityFrameworkCore;
using Npgsql;
using TattooFlash.Web.Data;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Repositories
{
    public class TagRepository(IDbContextFactory<AppDbContext> dbContextFactory) : ITagRepository
    {
        public async Task<TagSaveResult> CreateTag(TagModel tag)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            try
            {
                context.Tags.Add(tag);
                await context.SaveChangesAsync();
                return TagSaveResult.Success(tag);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Tags_NormalizedName"
            })
            {
                return TagSaveResult.Failure("Duplicate tag name");
            }

        }

        public async Task<bool> DeleteTag(Guid id)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Tags.Where(t => t.Id == id).ExecuteDeleteAsync() > 0;
        }

        public async Task<List<TagModel>> GetAllTags()
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();

            return await context.Tags.AsNoTracking().OrderBy(t => t.NormalizedName).ToListAsync();
        }

        public async Task<TagModel?> GetTagById(Guid id)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();

            return await context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TagSaveResult> UpdateTag(Guid id, TagModel tag)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();

            var existing = await context.Tags.SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Tag not found");

            try
            {
                existing.NormalizedName = tag.NormalizedName;
                existing.DispalyName = tag.DispalyName;

                await context.SaveChangesAsync();
                return TagSaveResult.Success(existing);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Tags_NormalizedName"
            })
            {
                return TagSaveResult.Failure("Duplicate tag name");
            }
        }
    }
}