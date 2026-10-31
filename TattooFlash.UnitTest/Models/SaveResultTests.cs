using TattooFlash.Web.Enums;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Models;

public class SaveResultTests
{
    [Fact]
    public void ImageSuccess_ContainsImageAndNoError()
    {
        // Arrange
        using var content = new MemoryStream([1]);
        var image = new ProcessedImage { Content = content, ContentType = "image/webp", Extension = ".webp" };

        // Act
        var result = ImageProcessingResult.Success(image);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(image, result.Image);
        Assert.Null(result.Error);
        Assert.True(content.CanRead);
    }

    [Fact]
    public void DesignSuccess_ContainsDesignAndNoError()
    {
        // Arrange
        var design = new DesignModel { Id = Guid.NewGuid() };

        // Act
        var result = DesignSaveResult.Success(design);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(design, result.Design);
        Assert.Null(result.ImageError);
    }

    [Theory]
    [InlineData(ImageProcessingError.InvalidImage)]
    [InlineData(ImageProcessingError.UnsupportedFormat)]
    [InlineData(ImageProcessingError.DimensionsExceeded)]
    [InlineData(ImageProcessingError.PixelLimitExceeded)]
    public void FailureResults_ContainErrorWithoutSuccessData(ImageProcessingError error)
    {
        // Arrange
        var expected = error;

        // Act
        var imageResult = ImageProcessingResult.Failure(expected);
        var designResult = DesignSaveResult.Failure(expected);

        // Assert
        Assert.False(imageResult.IsSuccess);
        Assert.Null(imageResult.Image);
        Assert.Equal(expected, imageResult.Error);
        Assert.False(designResult.IsSuccess);
        Assert.Null(designResult.Design);
        Assert.Equal(expected, designResult.ImageError);
    }

    [Fact]
    public void ImageSuccess_WithNullImage_Throws()
    {
        // Arrange
        ProcessedImage image = null!;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => ImageProcessingResult.Success(image));

        // Assert
        Assert.Equal("image", exception.ParamName);
    }

    [Fact]
    public void DesignSuccess_WithNullDesign_Throws()
    {
        // Arrange
        DesignModel design = null!;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => DesignSaveResult.Success(design));

        // Assert
        Assert.Equal("design", exception.ParamName);
    }
}
