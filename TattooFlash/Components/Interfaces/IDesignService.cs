using TattooFlash.Components;
using TattooFlash.Models;

namespace TattooFlash.Components.Interfaces
{
    public interface IDesignService
    { 
        public Task<List<DesignModel>> GetAllDesignsAsync();
        public Task<PaginatedDesignResults> GetPaginatedDesigns(int pageNumber);

        public Task<DesignModel?> GetDesignByIdAsync(Guid id);

        public Task<DesignModel> CreateDesignAsync(DesignModel design, Stream imageStream);

        public Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design, Stream? imageStream);

        public Task<bool> DeleteDesignAsync(Guid id);

        public Task ToggleAvailabilityAsync(Guid id);

    }
}
