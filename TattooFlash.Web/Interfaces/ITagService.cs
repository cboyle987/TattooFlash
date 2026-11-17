using TattooFlash.Web.Models;

namespace TattooFlash.Web.Interfaces;

/// <summary>Provides shared tag catalogue operations and tag-name validation.</summary>
public interface ITagService
{
    /// <summary>Retrieves tags ordered by normalized name, or an empty list.</summary>
    Task<List<TagModel>> GetAllTagsAsync();

    /// <summary>Retrieves a tag, or null when the identifier does not exist.</summary>
    Task<TagModel?> GetTagByIdAsync(Guid id);

    /// <summary>Trims and validates a name, then creates a tag with an invariant normalized name.</summary>
    /// <returns>The saved tag, or a validation or duplicate-name failure. Other persistence errors propagate.</returns>
    Task<TagSaveResult> CreateTagAsync(string? name);

    /// <summary>Trims and validates a name, then renames a tag without changing its identity or assignments.</summary>
    /// <returns>The saved tag, or a validation or duplicate-name failure. Other persistence errors propagate.</returns>
    /// <exception cref="KeyNotFoundException">The name is valid but the tag does not exist.</exception>
    Task<TagSaveResult> UpdateTagAsync(Guid id, string? name);

    /// <summary>Deletes a tag and its assignments, preserving designs. Returns false when the tag does not exist.</summary>
    Task<bool> DeleteTagAsync(Guid id);
}