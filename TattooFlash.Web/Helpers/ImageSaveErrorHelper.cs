using TattooFlash.Web.Enums;

namespace TattooFlash.Web.Helpers
{
    public static class ImageSaveErrorHelper
    {

        public static string GetImageSaveErrorMessage(ImageProcessingError error)
        {
            switch (error)
            {
                case ImageProcessingError.InvalidImage:
                    return "The selected file is not a valid image. Please choose another image.";
                case ImageProcessingError.UnsupportedFormat:
                    return "This image format is not supported. Please choose a JPEG, PNG or WebP image.";
                case ImageProcessingError.DimensionsExceeded:
                    return "The image is too wide or too tall. Please resize it to no more than 8000 pixels in either dimension.";
                case ImageProcessingError.PixelLimitExceeded:
                    return "The image exceeds the 25 megapixel limit. Please reduce its dimensions and try again.";
                default:
                    return "There has been an error saving your image";
            }
                
        }
    }
}
