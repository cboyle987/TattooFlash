using TattooFlash.Models;

namespace TattooFlash.Components.Interfaces
{
    public interface IImageProcessingService
    {
        ProcessedImage ProcessImage(Stream imageData);
    }
}
