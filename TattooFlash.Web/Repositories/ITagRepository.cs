using TattooFlash.Web.Models;

namespace TattooFlash.Web.Repositories
{
    public interface ITagRepository
    {
        public Task<List<TagModel>> GetAllTags();

        public Task<TagModel?> GetTagById(Guid id);

        public Task<TagSaveResult> CreateTag(TagModel tag);

        public Task<TagSaveResult> UpdateTag(Guid id, TagModel tag);

        public Task<bool> DeleteTag(Guid id);
    }
}