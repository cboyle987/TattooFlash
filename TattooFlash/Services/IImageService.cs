namespace TattooFlash.Services
{
    public interface IImageService
    {
        string GetImageUrl(string imageName);
        Task<string> UploadImage(Stream imageData, string contentType, string extension, CancellationToken cancellationToken);

        Task DeleteImageAsync(string imageName, CancellationToken cancellationToken);
    }
}
