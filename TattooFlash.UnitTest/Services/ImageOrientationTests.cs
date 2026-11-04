using System.Buffers.Binary;
using SkiaSharp;
using TattooFlash.Web.Services;

namespace TattooFlash.UnitTest.Services;

public class ImageOrientationTests
{
    [Theory]
    [InlineData(1, false, 255, 0, 0)]
    [InlineData(2, false, 0, 128, 0)]
    [InlineData(3, false, 255, 255, 0)]
    [InlineData(4, false, 0, 0, 255)]
    [InlineData(5, true, 255, 0, 0)]
    [InlineData(6, true, 0, 0, 255)]
    [InlineData(7, true, 255, 255, 0)]
    [InlineData(8, true, 0, 128, 0)]
    public void ProcessImage_ExifOrientation_TransformsDimensionsAndPixels(ushort orientation, bool swapsDimensions, int red, int green, int blue)
    {
        // Arrange
        using var source = CreateOrientedJpeg(orientation);
        var sut = new ImageProcessingService();


        // Act
        var result = sut.ProcessImage(source);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var image = Assert.IsType<TattooFlash.Web.Models.ProcessedImage>(result.Image);
        using var content = image.Content;


        // Assert
        using var decoded = SKBitmap.Decode(content);
        Assert.Equal(swapsDimensions ? 20 : 40, decoded.Width);
        Assert.Equal(swapsDimensions ? 40 : 20, decoded.Height);
        var topLeft = decoded.GetPixel(4, 4);
        // JPEG compression can slightly alter the input colours.
        Assert.InRange((int)topLeft.Red, Math.Max(0, red - 15), Math.Min(255, red + 15));
        Assert.InRange((int)topLeft.Green, Math.Max(0, green - 15), Math.Min(255, green + 15));
        Assert.InRange((int)topLeft.Blue, Math.Max(0, blue - 15), Math.Min(255, blue + 15));
    }

    private static MemoryStream CreateOrientedJpeg(ushort orientation)
    {
        using var bitmap = new SKBitmap(40, 20);
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, (x < 20, y < 10) switch
                {
                    (true, true) => SKColors.Red,
                    (false, true) => SKColors.Green,
                    (true, false) => SKColors.Blue,
                    _ => SKColors.Yellow
                });
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        var jpeg = data.ToArray();

        // APP1 Exif segment: little-endian TIFF with one SHORT orientation entry (0x0112).
        byte[] exif = [
            0xFF, 0xE1, 0x00, 0x22, 0x45, 0x78, 0x69, 0x66, 0, 0,
            0x49, 0x49, 0x2A, 0, 8, 0, 0, 0, 1, 0,
            0x12, 1, 3, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(28, 2), orientation);
        var stream = new MemoryStream();
        stream.Write(jpeg.AsSpan(0, 2));
        stream.Write(exif);
        stream.Write(jpeg.AsSpan(2));
        stream.Position = 0;
        return stream;
    }
}