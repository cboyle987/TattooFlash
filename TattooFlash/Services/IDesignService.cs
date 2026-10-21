using TattooFlash.Models;

namespace TattooFlash.Services
{
    public interface IDesignService
    { 
        public Task<List<DesignModel>> GetAllDesignsAsync();

        public Task<DesignModel?> GetDesignByIdAsync(Guid id);

        public Task<DesignModel> CreateDesignAsync(DesignModel design);

        public Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design);

        public Task<bool> DeleteDesignAsync(Guid id);

        public Task ToggleAvailabilityAsync(Guid id);

    }
}
