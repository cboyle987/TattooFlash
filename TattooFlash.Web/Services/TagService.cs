using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;
using TattooFlash.Web.Repositories;

namespace TattooFlash.Web.Services;

public class TagService(ITagRepository repository) : ITagService
{
    private const int MaximumNameLength = 100;

    public Task<List<TagModel>> GetAllTagsAsync() => repository.GetAllTags();

    public Task<TagModel?> GetTagByIdAsync(Guid id) => repository.GetTagById(id);

    public Task<TagSaveResult> CreateTagAsync(string? name) => SaveTagAsync(null, name);

    public Task<TagSaveResult> UpdateTagAsync(Guid id, string? name) => SaveTagAsync(id, name);

    public Task<bool> DeleteTagAsync(Guid id) => repository.DeleteTag(id);

    private Task<TagSaveResult> SaveTagAsync(Guid? id, string? name)
    {
        var displayName = name?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return Task.FromResult(TagSaveResult.Failure("Tag name is required."));

        if (displayName.Length > MaximumNameLength)
            return Task.FromResult(TagSaveResult.Failure("Tag name must be 100 characters or fewer."));

        var tag = new TagModel
        {
            Id = id ?? Guid.Empty,
            DispalyName = displayName,
            NormalizedName = displayName.ToUpperInvariant()
        };

        return id.HasValue ? repository.UpdateTag(id.Value, tag) : repository.CreateTag(tag);
    }
}