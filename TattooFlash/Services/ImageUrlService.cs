namespace TattooFlash.Services
{

    public interface IImageUrlService
    {
        string GetImageUrl(string imageName);
    }
    public class ImageUrlService: IImageUrlService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        public ImageUrlService(IWebHostEnvironment webHostEnvironment)
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
                return "/samples/default-image.jpg"; // Return a default image if the specified image doesn't exist
            }
        }
    }
}
