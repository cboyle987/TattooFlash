using TattooFlash.Web.Enums;
using TattooFlash.Web.Helpers;

namespace TattooFlash.UnitTest.Helpers;

public class ImageSaveErrorHelperTests
{
    [Theory]
    [InlineData(ImageProcessingError.InvalidImage, "The selected file is not a valid image. Please choose another image.")]
    [InlineData(ImageProcessingError.UnsupportedFormat, "This image format is not supported. Please choose a JPEG, PNG or WebP image.")]
    [InlineData(ImageProcessingError.DimensionsExceeded, "The image is too wide or too tall. Please resize it to no more than 8000 pixels in either dimension.")]
    [InlineData(ImageProcessingError.PixelLimitExceeded, "The image exceeds the 25 megapixel limit. Please reduce its dimensions and try again.")]
    [InlineData((ImageProcessingError)int.MaxValue, "There has been an error saving your image")]
    public void GetImageSaveErrorMessage_ReturnsAppropriateMessage(ImageProcessingError error, string expected)
    {
        // Arrange
        var sut = error;

        // Act
        var result = ImageSaveErrorHelper.GetImageSaveErrorMessage(sut);

        // Assert
        Assert.Equal(expected, result);
    }
}