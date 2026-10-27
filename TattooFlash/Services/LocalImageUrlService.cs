namespace TattooFlash.Services
{

    public interface IImageUrlService
    {
        string GetImageUrl(string imageName);
    }
    public class LocalImageUrlService: IImageUrlService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        public LocalImageUrlService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
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
    }
}
