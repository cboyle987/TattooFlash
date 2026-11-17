using Amazon.S3.Model;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Repositories;

/// <summary>Provides database queries and persistence for designs.</summary>
public interface IDesignRepository
{
    /// <summary>Retrieves all designs ordered by name without tracking changes.</summary>
    /// <returns>All designs, or an empty list when none exist.</returns>
    Task<List<DesignModel>> GetAllDesignsAsync(Guid SelectedTagId);
    /// <summary>Retrieves a design without tracking changes.</summary>
    /// <param name="id">The design identifier.</param>
    /// <returns>The matching design, or <see langword="null"/> when it does not exist.</returns>
    Task<DesignModel?> GetDesignByIdAsync(Guid id);
    /// <summary>Saves a new design, assigning a new identifier and UTC creation and update timestamps.</summary>
    /// <param name="design">The design to save. Its identifier and timestamps are set during creation.</param>
    /// <param name="selectedTagIds">The IDs of the tags associated with the design.</param>
    /// <returns>The saved design.</returns>
    Task<DesignModel> CreateDesignAsync(DesignModel design, IReadOnlyCollection<Guid> selectedTagIds);
    /// <summary>Updates a design's editable fields and UTC update timestamp, preserving its identifier and creation timestamp.</summary>
    /// <param name="id">The identifier of the existing design.</param>
    /// <param name="design">The name, price, image name, and availability values to save.</param>
    /// <returns>The updated design.</returns>
    /// <exception cref="KeyNotFoundException">The design does not exist.</exception>
    Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design, IReadOnlyCollection<Guid> selectedTagIds);
    /// <summary>Deletes a design record without deleting its stored image.</summary>
    /// <param name="id">The identifier of the design to delete.</param>
    /// <returns><see langword="true"/> if a record was deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteDesignAsync(Guid id);
    /// <summary>Atomically reverses a design's availability in the database.</summary>
    /// <param name="id">The identifier of the design to update.</param>
    /// <returns>A task representing the update operation.</returns>
    /// <exception cref="KeyNotFoundException">The design does not exist.</exception>
    Task ToggleAvailabilityAsync(Guid id);

    /// <summary>Retrieves an untracked page of designs ordered by creation time descending, then by identifier.</summary>
    /// <param name="pageNumber">The one-based page number to retrieve.</param>
    /// <returns>The requested designs, total design count, page number, and configured page size. Pages beyond the available results contain no designs.</returns>
    /// <exception cref="ArgumentException">The page number is less than one.</exception>
    Task<PaginatedDesignResults> GetDesignsAsync(int pageNumber, Guid selectedTagId);
}