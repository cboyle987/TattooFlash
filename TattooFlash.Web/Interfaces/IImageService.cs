namespace TattooFlash.Web.Interfaces
{
    /// <summary>Provides image storage operations and public image URLs.</summary>
    public interface IImageService
    {
        /// <summary>Builds the public URL for a stored image without checking whether it exists.</summary>
        /// <param name="imageName">The stored image filename.</param>
        /// <returns>The image's public URL.</returns>
        string GetImageUrl(string imageName);
        /// <summary>Uploads image data using a generated unique filename.</summary>
        /// <param name="imageData">The image stream to upload. The caller remains responsible for disposing it.</param>
        /// <param name="contentType">The image's MIME type.</param>
        /// <param name="extension">The filename extension, including its leading dot, for example <c>.webp</c>.</param>
        /// <param name="cancellationToken">A token used to cancel the upload.</param>
        /// <returns>The stored filename to persist with the design.</returns>
        Task<string> UploadImage(Stream imageData, string contentType, string extension, CancellationToken cancellationToken);

        /// <summary>Deletes an image from storage.</summary>
        /// <param name="imageName">The stored filename of the image to delete.</param>
        /// <param name="cancellationToken">A token used to cancel the deletion.</param>
        /// <returns>A task representing the deletion operation.</returns>
        Task DeleteImageAsync(string imageName, CancellationToken cancellationToken);
    }
}
