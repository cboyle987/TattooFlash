using TattooFlash.Web.Models;

namespace TattooFlash.Web.Interfaces
{
    /// <summary>Provides design catalogue operations, including image processing and storage.</summary>
    public interface IDesignService
    { 
        /// <summary>Retrieves all designs ordered by name.</summary>
        /// <returns>All designs, or an empty list when none exist.</returns>
        public Task<List<DesignModel>> GetAllDesignsAsync();
        /// <summary>Retrieves a page of designs, ordered newest first with ID as a tie-breaker.</summary>
        /// <param name="pageNumber">The one-based page number to retrieve.</param>
        /// <returns>The requested designs and pagination metadata, using the configured page size.</returns>
        /// <exception cref="ArgumentException">The page number is less than one.</exception>
        public Task<PaginatedDesignResults> GetPaginatedDesigns(int pageNumber);

        /// <summary>Retrieves a design by its identifier.</summary>
        /// <param name="id">The design identifier.</param>
        /// <returns>The matching design, or <see langword="null"/> when it does not exist.</returns>
        public Task<DesignModel?> GetDesignByIdAsync(Guid id);

        /// <summary>Processes and uploads an image, then saves a new design.</summary>
        /// <param name="design">The design to create. Its image name, identifier, and timestamps are assigned during creation.</param>
        /// <param name="imageStream">The source image data to process.</param>
        /// <returns>The saved design.</returns>
        /// <exception cref="NotSupportedException">The image cannot be processed or exceeds the supported limits.</exception>
        public Task<DesignModel> CreateDesignAsync(DesignModel design, Stream imageStream);

        /// <summary>Updates a design, optionally processing and uploading a replacement image and deleting the previous image.</summary>
        /// <param name="id">The identifier of the existing design.</param>
        /// <param name="design">The design values to save, including the existing image name when no replacement is supplied.</param>
        /// <param name="imageStream">Replacement image data, or <see langword="null"/> to save without uploading a new image.</param>
        /// <returns>The updated design, retaining its original identifier and creation timestamp.</returns>
        /// <exception cref="KeyNotFoundException">The design does not exist.</exception>
        /// <exception cref="NotSupportedException">The replacement image cannot be processed or exceeds the supported limits.</exception>
        public Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design, Stream? imageStream);

        /// <summary>Deletes a design's stored image and then its database record.</summary>
        /// <param name="id">The identifier of the design to delete.</param>
        /// <returns><see langword="false"/> if the design was not found; otherwise, <see langword="true"/> after the deletion operations complete.</returns>
        public Task<bool> DeleteDesignAsync(Guid id);

        /// <summary>Reverses a design's current availability.</summary>
        /// <param name="id">The identifier of the design to update.</param>
        /// <returns>A task representing the update operation.</returns>
        /// <exception cref="KeyNotFoundException">The design does not exist.</exception>
        public Task ToggleAvailabilityAsync(Guid id);

    }
}
