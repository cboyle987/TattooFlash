using TattooFlash.Models;
using TattooFlash.Repositories;

namespace TattooFlash.Services;

public class DesignService(IDesignRepository repository) : IDesignService
{
    public Task<List<DesignModel>> GetAllDesignsAsync() => repository.GetAllDesignsAsync();

    public Task<DesignModel?> GetDesignByIdAsync(Guid id) => repository.GetDesignByIdAsync(id);

    public Task<DesignModel> CreateDesignAsync(DesignModel design) => repository.CreateDesignAsync(design);

    public Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design) => repository.UpdateDesignAsync(id, design);

    public Task<bool> DeleteDesignAsync(Guid id) => repository.DeleteDesignAsync(id);

    public Task ToggleAvailabilityAsync(Guid id) => repository.ToggleAvailabilityAsync(id);
}
