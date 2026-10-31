using Amazon.S3.Model;
using TattooFlash.Models;

namespace TattooFlash.Repositories;

public interface IDesignRepository
{
    Task<List<DesignModel>> GetAllDesignsAsync();
    Task<DesignModel?> GetDesignByIdAsync(Guid id);
    Task<DesignModel> CreateDesignAsync(DesignModel design);
    Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design);
    Task<bool> DeleteDesignAsync(Guid id);
    Task ToggleAvailabilityAsync(Guid id);

    Task<PaginatedDesignResults> GetDesignsAsync(int pageNumber);
}
