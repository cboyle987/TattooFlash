namespace TattooFlash.Services
{
    public class LocalImageUrlService: IImageService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        public LocalImageUrlService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public Task DeleteImageAsync(string imageName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public string GetImageUrl(string imageName)
        {
            var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "samples", imageName);
            if (File.Exists(imagePath))
            {
                return $"/samples/{imageName}";
            }
            else
            {
                return "/samples/default-image.png"; // Return a default image if the specified image doesn't exist
            }
        }

        public Task<string> UploadImage(Stream imageData, string contentType, string extension, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
