using SkiaSharp;
using TattooFlash.Web.Enums;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly List<SKEncodedImageFormat> _supportedFormats = new List<SKEncodedImageFormat>
        {
            SKEncodedImageFormat.Jpeg,
            SKEncodedImageFormat.Png,
            SKEncodedImageFormat.Webp
        };

        public ImageProcessingResult ProcessImage(Stream imageData)
        {
            using var codec = CreateCodec(imageData);
            if (codec == null)
            {
                return ImageProcessingResult.Failure(ImageProcessingError.InvalidImage);
            }

            var validationError = ValidateImage(codec);

            if (validationError != null)
            {
                return ImageProcessingResult.Failure((ImageProcessingError)validationError);
            }

            using var bitmap = DecodeImage(codec);
            using var orientatedBitmap = ApplyOrientation(bitmap, codec.EncodedOrigin);
            using var finalBitmap = ResizeImage(orientatedBitmap);

            var image = EncodeWebp(finalBitmap);
            return ImageProcessingResult.Success(image);
        }

        private static SKCodec? CreateCodec(Stream imageData)
        {
            return SKCodec.Create(imageData);
        }

        private ImageProcessingError? ValidateImage(SKCodec codec)
        {
            if (!_supportedFormats.Contains(codec.EncodedFormat))
            {
                return ImageProcessingError.UnsupportedFormat;
            }

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
            {
                return ImageProcessingError.InvalidImage;
            }

            if (info.Width > 8000 || info.Height > 8000)
            {
                return ImageProcessingError.DimensionsExceeded;
            }

            var totalPixels = (long)info.Width * info.Height;
            if (totalPixels > 25_000_000)
            {
                return ImageProcessingError.PixelLimitExceeded;
            }

            return null;

        }

        private static SKBitmap DecodeImage(SKCodec codec)
        {
            var info = codec.Info;
            var bitmap = new SKBitmap(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            try
            {
                var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
                if (result != SKCodecResult.Success)
                {
                    throw new NotSupportedException($"Failed to decode the image. Codec result: {result}");
                }

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private static SKBitmap ResizeImage(SKBitmap source)
        {
            var scale = Math.Min(1.0, 1800.0 / Math.Max(source.Width, source.Height));
            var scaledWidth = (int)Math.Max(source.Width * scale, 1);
            var scaledHeight = (int)Math.Max(source.Height * scale, 1);
            var result = new SKBitmap(scaledWidth, scaledHeight, source.ColorType, source.AlphaType);
            try
            {
                using var canvas = new SKCanvas(result);
                canvas.Clear(SKColors.Transparent);
                var destRect = new SKRect(0, 0, scaledWidth, scaledHeight);
                canvas.DrawBitmap(source, destRect, new SKSamplingOptions(SKFilterMode.Linear), null);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private static ProcessedImage EncodeWebp(SKBitmap bitmap)
        {
            var options = new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 75);
            using var pixelView = bitmap.PeekPixels();
            var imageStream = new MemoryStream();
            try
            {
                if (!pixelView.Encode(imageStream, options))
                {
                    throw new NotSupportedException("Failed to encode the image to WebP format.");
                }

                imageStream.Seek(0, SeekOrigin.Begin);
                return new ProcessedImage
                {
                    Content = imageStream,
                    ContentType = "image/webp",
                    Extension = ".webp"
                };
            }
            catch
            {
                imageStream.Dispose();
                throw;
            }
        }

        private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
        {
            var swapDimensions = origin == SKEncodedOrigin.LeftBottom || origin == SKEncodedOrigin.RightTop || origin == SKEncodedOrigin.RightBottom || origin == SKEncodedOrigin.LeftTop;

            var result = new SKBitmap(swapDimensions ? source.Height : source.Width, swapDimensions ? source.Width : source.Height, source.ColorType, source.AlphaType);

            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);


            switch (origin)
            {
                case SKEncodedOrigin.TopLeft:
                    // Already correctly oriented.
                    break;

                case SKEncodedOrigin.TopRight:
                    // Mirror horizontally.
                    canvas.Translate(source.Width, 0);
                    canvas.Scale(-1, 1);
                    break;

                case SKEncodedOrigin.BottomRight:
                    // Rotate 180 degrees.
                    canvas.Translate(source.Width, source.Height);
                    canvas.RotateDegrees(180);
                    break;

                case SKEncodedOrigin.BottomLeft:
                    // Mirror vertically.
                    canvas.Translate(0, source.Height);
                    canvas.Scale(1, -1);
                    break;

                case SKEncodedOrigin.LeftTop:
                    // Transpose: swap the x and y axes.
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    break;

                case SKEncodedOrigin.RightTop:
                    // Rotate 90 degrees clockwise.
                    canvas.Translate(source.Height, 0);
                    canvas.RotateDegrees(90);
                    break;

                case SKEncodedOrigin.RightBottom:
                    // Reflect across the opposite diagonal.
                    canvas.Translate(source.Height, source.Width);
                    canvas.RotateDegrees(90);
                    canvas.Scale(-1, 1);
                    break;

                case SKEncodedOrigin.LeftBottom:
                    // Rotate 90 degrees counterclockwise.
                    canvas.Translate(0, source.Width);
                    canvas.RotateDegrees(-90);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(origin), origin, "Unsupported image orientation.");
            }
            canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
            return result;
        }
    }
}