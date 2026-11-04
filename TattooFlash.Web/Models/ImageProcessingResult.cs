using TattooFlash.Web.Enums;

namespace TattooFlash.Web.Models
{
    public class ImageProcessingResult
    {
        public ProcessedImage? Image { get; }

        public ImageProcessingError? Error { get; }

        public bool IsSuccess => Image is not null;

        private ImageProcessingResult(
        ProcessedImage? image,
        ImageProcessingError? error)
        {
            Image = image;
            Error = error;
        }

        public static ImageProcessingResult Success(ProcessedImage image)
        {
            ArgumentNullException.ThrowIfNull(image);
            return new ImageProcessingResult(image, null);
        }

        public static ImageProcessingResult Failure(ImageProcessingError error)
        {
            return new ImageProcessingResult(null, error);
        }
    }
}