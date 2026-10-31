using TattooFlash.Components.Interfaces;
using TattooFlash.Models;
using TattooFlash.Repositories;

namespace TattooFlash.Services;

public class DesignService(IDesignRepository repository, IImageProcessingService imageProcessingService, IImageService imageService, ILogger<DesignService> logger) : IDesignService
{

    public Task<List<DesignModel>> GetAllDesignsAsync() => repository.GetAllDesignsAsync();

    public Task<DesignModel?> GetDesignByIdAsync(Guid id) => repository.GetDesignByIdAsync(id);
   

    public async Task<DesignModel> CreateDesignAsync(DesignModel design, Stream imageStream){

        var processedResult = imageProcessingService.ProcessImage(imageStream);
        using var processedContent = processedResult.Content;

        var resultFilename = await imageService.UploadImage(processedContent, processedResult.ContentType, processedResult.Extension, CancellationToken.None);
        var originalImageName = design.ImageName;
        design.ImageName = resultFilename;

        try
        {
            return  await repository.CreateDesignAsync(design);

        } catch
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
                design.ImageName = originalImageName;
                design.Id = Guid.Empty;
            }

            throw;
        }

      }

    public async Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design, Stream? imageStream) {
        
        
        if(imageStream != null) {
            var processedResult = imageProcessingService.ProcessImage(imageStream);
            using var processedContent = processedResult.Content;

            var resultFilename = await imageService.UploadImage(processedContent, processedResult.ContentType, processedResult.Extension, CancellationToken.None);
            var originalImageName = design.ImageName;
            design.ImageName = resultFilename;

            try
            {
                var result = await repository.UpdateDesignAsync(id, design);
                await imageService.DeleteImageAsync(originalImageName, CancellationToken.None);
                return result;
            } catch
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
                    design.ImageName = originalImageName;
                    design.Id = Guid.Empty;
                }

                throw;
            }

        } else
        {
            return await repository.UpdateDesignAsync(id, design);
        }
    }

    public async Task<bool> DeleteDesignAsync(Guid id) {
        var design = await repository.GetDesignByIdAsync(id);

        if(design == null)
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
}
