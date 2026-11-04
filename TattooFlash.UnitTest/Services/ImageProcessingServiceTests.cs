using SkiaSharp;
using TattooFlash.Web.Enums;
using TattooFlash.Web.Services;

namespace TattooFlash.UnitTest.Services;

public class ImageProcessingServiceTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void ProcessImage_SupportedFormat_ReturnsReadableWebp(SKEncodedImageFormat format)
    {
        // Arrange
        using var source = CreateImage(24, 12, format);
        var sut = new ImageProcessingService();


        // Act
        var result = sut.ProcessImage(source);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var image = Assert.IsType<TattooFlash.Web.Models.ProcessedImage>(result.Image);
        using var content = image.Content;


        // Assert
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal(".webp", image.Extension);
        Assert.Equal(0, content.Position);
        using var codec = SKCodec.Create(content);
        Assert.NotNull(codec);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal(24, codec.Info.Width);
        Assert.Equal(12, codec.Info.Height);
    }

    [Theory]
    [InlineData(2400, 1200, 1800, 900)]
    [InlineData(1200, 2400, 900, 1800)]
    [InlineData(1800, 900, 1800, 900)]
    [InlineData(1, 2400, 1, 1800)]
    [InlineData(8000, 1, 1800, 1)]
    [InlineData(1, 8000, 1, 1800)]
    public void ProcessImage_ResizesWithoutUpscalingAndPreservesAspectRatio(int width, int height, int expectedWidth, int expectedHeight)
    {
        // Arrange
        using var source = CreateImage(width, height, SKEncodedImageFormat.Png);
        var sut = new ImageProcessingService();


        // Act
        var result = sut.ProcessImage(source);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var image = Assert.IsType<TattooFlash.Web.Models.ProcessedImage>(result.Image);
        using var content = image.Content;


        // Assert
        using var decoded = SKBitmap.Decode(content);
        Assert.NotNull(decoded);
        Assert.Equal(expectedWidth, decoded.Width);
        Assert.Equal(expectedHeight, decoded.Height);
    }

    [Theory]
    [InlineData(8001, 1, ImageProcessingError.DimensionsExceeded)]
    [InlineData(1, 8001, ImageProcessingError.DimensionsExceeded)]
    [InlineData(5001, 5000, ImageProcessingError.PixelLimitExceeded)]
    public void ProcessImage_ExceedingLimits_RejectsImage(int width, int height, ImageProcessingError expectedError)
    {
        // Arrange
        using var source = CreateImage(width, height, SKEncodedImageFormat.Png);
        var sut = new ImageProcessingService();


        // Act
        var result = sut.ProcessImage(source);


        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Image);
        Assert.Equal(expectedError, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an image")]
    public void ProcessImage_InvalidData_RejectsImage(string data)
    {
        // Arrange
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
        var sut = new ImageProcessingService();


        // Act
        var result = sut.ProcessImage(source);


        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Image);
        Assert.Equal(ImageProcessingError.InvalidImage, result.Error);
    }

    [Fact]
    public void ProcessImage_Gif_RejectsUnsupportedFormat()
    {
        // Arrange
        using var source = new MemoryStream(Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"));
        var sut = new ImageProcessingService();


        // Act
        var result = sut.ProcessImage(source);


        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Image);
        Assert.Equal(ImageProcessingError.UnsupportedFormat, result.Error);
    }

    [Fact]
    public void ProcessImage_TransparentPng_PreservesTransparency()
    {
        // Arrange
        using var source = CreateImage(2, 2, SKEncodedImageFormat.Png);
        var sut = new ImageProcessingService();


        // Act
        var result = sut.ProcessImage(source);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var image = Assert.IsType<TattooFlash.Web.Models.ProcessedImage>(result.Image);
        using var content = image.Content;


        // Assert
        using var decoded = SKBitmap.Decode(content);
        Assert.Equal(0, decoded.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void ProcessImage_RecognisedJpegWithMissingPixelData_ThrowsDecodeException()
    {
        // Arrange
        using var validImage = CreateImage(24, 12, SKEncodedImageFormat.Jpeg);
        var bytes = validImage.ToArray();
        var scanStart = -1;
        for (var i = 2; i < bytes.Length - 1; i++)
        {
            if (bytes[i] == 0xFF && bytes[i + 1] == 0xDA)
            {
                scanStart = i;
                break;
            }
        }
        Assert.True(scanStart >= 0, "Generated JPEG must contain a start-of-scan marker.");
        var scanHeaderLength = (bytes[scanStart + 2] << 8) | bytes[scanStart + 3];
        var truncated = bytes[..(scanStart + 2 + scanHeaderLength)];
        using var probe = new MemoryStream(truncated);
        using var codec = SKCodec.Create(probe);
        Assert.NotNull(codec); // Header is recognisable; failure occurs during pixel decoding.
        using var source = new MemoryStream(truncated);
        var sut = new ImageProcessingService();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => sut.ProcessImage(source));

        // Assert
        Assert.Contains("Failed to decode the image", exception.Message);
    }

    private static MemoryStream CreateImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return new MemoryStream(data.ToArray());
    }
}