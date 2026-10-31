using SkiaSharp;
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


        public ProcessedImage ProcessImage(Stream imageData)
        {
            using var codec = SKCodec.Create(imageData);

            if (codec == null)
            {
                throw new NotSupportedException($"The image format for the provided data is not supported or the data is not a valid image.");
            }

            if (!_supportedFormats.Contains(codec.EncodedFormat))
            {
                throw new NotSupportedException($"The image format {codec.EncodedFormat} is not supported.");
            }
            ;


            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
            {
                throw new NotSupportedException($"The image dimensions are invalid: {info.Width}x{info.Height}.");
            }

            if (info.Width > 8000 || info.Height > 8000)
            {
                throw new NotSupportedException($"The image dimensions {info.Width}x{info.Height} exceed the maximum allowed size of 8000x8000 pixels.");
            }
            var totalPixels = (long)info.Width * info.Height;
            if (totalPixels > 25_000_000)
            {
                throw new NotSupportedException($"The image has {totalPixels} pixels, which exceeds the maximum allowed size of 25,000,000 pixels.");
            }

            using var bitmap = new SKBitmap(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());

            if (result != SKCodecResult.Success)
            {
                throw new NotSupportedException($"Failed to decode the image. Codec result: {result}");
            }

            using var orientatedBitmap = ApplyOrientation(bitmap, codec.EncodedOrigin);

            var scale = Math.Min(1.0, 1800.0 / Math.Max(orientatedBitmap.Width, orientatedBitmap.Height));


            var scaledWidth = (int)(Math.Max(orientatedBitmap.Width * scale, 1));
            var scaledHeight = (int)(Math.Max(orientatedBitmap.Height * scale, 1));

            using var finalBitmap = new SKBitmap(scaledWidth, scaledHeight, orientatedBitmap.ColorType, orientatedBitmap.AlphaType);

            using (var canvas = new SKCanvas(finalBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                var destRect = new SKRect(0, 0, scaledWidth, scaledHeight);
                canvas.DrawBitmap(orientatedBitmap, destRect, new SKSamplingOptions(SKFilterMode.Linear), null);

            }

            var options = new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 75);
            using var pixelView = finalBitmap.PeekPixels();
            var imageStream = new MemoryStream();
            try
            {
                var encodeResult = pixelView.Encode(imageStream, options);
                if (encodeResult == false)
                {
                    throw new NotSupportedException($"Failed to encode the image to WebP format.");
                };
            imageStream.Seek(0, SeekOrigin.Begin);
            } catch
            {
                imageStream.Dispose();
                throw;
            }

            return new ProcessedImage
            {
                Content = imageStream,
                ContentType = "image/webp",
                Extension = ".webp"
            };
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
