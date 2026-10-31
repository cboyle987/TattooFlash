using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;
using TattooFlash.Web.Repositories;

namespace TattooFlash.Web.Services;

public class DesignService(IDesignRepository repository, IImageProcessingService imageProcessingService, IImageService imageService, ILogger<DesignService> logger) : IDesignService
{

    public Task<List<DesignModel>> GetAllDesignsAsync() => repository.GetAllDesignsAsync();

    public Task<DesignModel?> GetDesignByIdAsync(Guid id) => repository.GetDesignByIdAsync(id);


    public async Task<DesignSaveResult> CreateDesignAsync(DesignModel design, Stream imageStream)
    {

        var processedResult = imageProcessingService.ProcessImage(imageStream);

        if (!processedResult.IsSuccess)
        {
            var error = processedResult.Error
                ?? throw new InvalidOperationException("Image processing failed without an error code.");
            return DesignSaveResult.Failure(error);
        }

        var image = processedResult.Image
            ?? throw new InvalidOperationException("Image processing succeeded without an image.");
        using var processedContent = image.Content;

        var resultFilename = await imageService.UploadImage(processedContent, image.ContentType, image.Extension, CancellationToken.None);
        var originalImageName = design.ImageName;
        design.ImageName = resultFilename;

        try
        {
            var model = await repository.CreateDesignAsync(design);
            return DesignSaveResult.Success(model);

        }
        catch
        {
            await HandleImageRollbackAsync(resultFilename, originalImageName, design);
            design.Id = Guid.Empty;
            throw;
        }

    }

    public async Task<DesignSaveResult> UpdateDesignAsync(Guid id, DesignModel design, Stream? imageStream)
    {

        // Image has not changed no updated required. 
        if (imageStream == null)
        {
            var model = await repository.UpdateDesignAsync(id, design);
            return DesignSaveResult.Success(model);
        }



        var processedResult = imageProcessingService.ProcessImage(imageStream);
        if (!processedResult.IsSuccess)
        {
            var error = processedResult.Error
                ?? throw new InvalidOperationException("Image processing failed without an error code.");
            return DesignSaveResult.Failure(error);
        }
        var image = processedResult.Image
            ?? throw new InvalidOperationException("Image processing succeeded without an image.");
        using var processedContent = image.Content;

        var resultFilename = await imageService.UploadImage(processedContent, image.ContentType, image.Extension, CancellationToken.None);
        var originalImageName = design.ImageName;
        design.ImageName = resultFilename;

        DesignModel result;
        try
        {
            result = await repository.UpdateDesignAsync(id, design);
        }
        catch
        {
            await HandleImageRollbackAsync(resultFilename, originalImageName, design);
            throw;
        }

        try
        {
            await imageService.DeleteImageAsync(originalImageName, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            logger.LogError(
                cleanupException,
                "Failed to delete previous image {ImageName} after design update", originalImageName);
        }

        return DesignSaveResult.Success(result);


    }

    public async Task<bool> DeleteDesignAsync(Guid id)
    {
        var design = await repository.GetDesignByIdAsync(id);

        if (design == null)
        {
            return false;
        }

        await imageService.DeleteImageAsync(design.ImageName, CancellationToken.None);
        await repository.DeleteDesignAsync(id);

        return true;
    }

    public Task ToggleAvailabilityAsync(Guid id) => repository.ToggleAvailabilityAsync(id);

    public Task<PaginatedDesignResults> GetPaginatedDesigns(int pageNumber)
    {
        return repository.GetDesignsAsync(pageNumber);
    }

    public async Task HandleImageRollbackAsync(string resultFilename, string originalFilename, DesignModel design)
    {
        try
        {
            await imageService.DeleteImageAsync(resultFilename, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            logger.LogError(
                cleanupException,
                "Failed to delete uploaded image {ImageName} after design save failure", resultFilename);
        }
        finally
        {
            design.ImageName = originalFilename;
        }
    }
}
