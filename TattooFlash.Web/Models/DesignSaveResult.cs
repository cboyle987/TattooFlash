using TattooFlash.Web.Enums;

namespace TattooFlash.Web.Models
{
    public sealed class DesignSaveResult
    {
        public DesignModel? Design { get; }
        public ImageProcessingError? ImageError { get; }

        public bool IsSuccess => Design is not null;

        private DesignSaveResult(
            DesignModel? design,
            ImageProcessingError? imageError)
        {
            Design = design;
            ImageError = imageError;
        }

        public static DesignSaveResult Success(DesignModel design)
        {
            ArgumentNullException.ThrowIfNull(design);
            return new DesignSaveResult(design, null);
        }

        public static DesignSaveResult Failure(ImageProcessingError imageError)
        {
            return new DesignSaveResult(null, imageError);
        }
    }
}
