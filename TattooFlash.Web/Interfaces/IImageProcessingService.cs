using TattooFlash.Web.Models;

namespace TattooFlash.Web.Interfaces
{
    /// <summary>Prepares uploaded images for storage and display.</summary>
    public interface IImageProcessingService
    {
        /// <summary>Decodes a JPEG, PNG, or WebP image, applies its orientation, and encodes it as WebP with a maximum dimension of 1800 pixels without upscaling.</summary>
        /// <param name="imageData">The source image data to decode.</param>
        /// <returns>
        /// A successful result containing the encoded image, content type and extension, or a failure
        /// result when the data is unrecognisable, the format is unsupported, dimensions are invalid,
        /// either dimension exceeds 8000 pixels, or the image exceeds 25 million pixels.
        /// On success, the caller must dispose <see cref="ProcessedImage.Content"/> on the result's image.
        /// </returns>
        /// <exception cref="NotSupportedException">The recognised image cannot be decoded or the processed image cannot be encoded as WebP.</exception>
        ImageProcessingResult ProcessImage(Stream imageData);
    }
}
