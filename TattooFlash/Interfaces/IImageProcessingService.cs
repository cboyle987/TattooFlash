using TattooFlash.Models;

namespace TattooFlash.Interfaces
{
    /// <summary>Prepares uploaded images for storage and display.</summary>
    public interface IImageProcessingService
    {
        /// <summary>Decodes a JPEG, PNG, or WebP image, applies its orientation, and encodes it as WebP with a maximum dimension of 1800 pixels without upscaling.</summary>
        /// <param name="imageData">The source image data to decode.</param>
        /// <returns>The encoded image and its content type and extension. The caller must dispose the returned content stream.</returns>
        /// <exception cref="NotSupportedException">The image is invalid, unsupported, exceeds 8000 pixels in either dimension or 25 million total pixels, or cannot be decoded or encoded.</exception>
        ProcessedImage ProcessImage(Stream imageData);
    }
}
