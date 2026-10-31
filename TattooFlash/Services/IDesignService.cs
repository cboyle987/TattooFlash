using TattooFlash.Components;
using TattooFlash.Models;

namespace TattooFlash.Services
{
    public interface IDesignService
    { 
        public Task<List<DesignModel>> GetAllDesignsAsync();

        public Task<DesignModel?> GetDesignByIdAsync(Guid id);

        public Task<DesignModel> CreateDesignAsync(DesignModel design, Stream imageStream);

        public Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design, Stream? imageStream);

        public Task<bool> DeleteDesignAsync(Guid id);

        public Task ToggleAvailabilityAsync(Guid id);

    }
}
