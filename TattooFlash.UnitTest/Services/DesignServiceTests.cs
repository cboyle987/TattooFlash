using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TattooFlash.Web.Enums;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;
using TattooFlash.Web.Repositories;
using TattooFlash.Web.Services;

namespace TattooFlash.UnitTest.Services;

public class DesignServiceTests
{
    private readonly Mock<IDesignRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IImageProcessingService> _processor = new(MockBehavior.Strict);
    private readonly Mock<IImageService> _images = new(MockBehavior.Strict);
    private DesignService CreateService() => new(_repository.Object, _processor.Object, _images.Object, NullLogger<DesignService>.Instance);

    [Fact]
    public async Task GetAllDesignsAsync_ReturnsRepositoryResults()
    {
        // Arrange
        var expected = new List<DesignModel> { new() };
        _repository.Setup(x => x.GetAllDesignsAsync()).ReturnsAsync(expected);
        var sut = CreateService();

        // Act
        var result = await sut.GetAllDesignsAsync();

        // Assert
        Assert.Same(expected, result);
        _repository.Verify(x => x.GetAllDesignsAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetDesignByIdAsync_ReturnsRepositoryResult(bool exists)
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = exists ? new DesignModel { Id = id } : null;
        _repository.Setup(x => x.GetDesignByIdAsync(id)).ReturnsAsync(expected);
        var sut = CreateService();

        // Act
        var result = await sut.GetDesignByIdAsync(id);

        // Assert
        Assert.Same(expected, result);
        _repository.Verify(x => x.GetDesignByIdAsync(id), Times.Once);
    }

    [Fact]
    public async Task GetPaginatedDesigns_ForwardsPageNumberAndReturnsResult()
    {
        // Arrange
        var expected = new PaginatedDesignResults { PageNumber = 3, PageSize = 12 };
        _repository.Setup(x => x.GetDesignsAsync(3)).ReturnsAsync(expected);
        var sut = CreateService();

        // Act
        var result = await sut.GetPaginatedDesigns(3);

        // Assert
        Assert.Same(expected, result);
        _repository.Verify(x => x.GetDesignsAsync(3), Times.Once);
    }

    [Fact]
    public async Task ToggleAvailabilityAsync_ForwardsIdentifier()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.Setup(x => x.ToggleAvailabilityAsync(id)).Returns(Task.CompletedTask);
        var sut = CreateService();

        // Act
        await sut.ToggleAvailabilityAsync(id);

        // Assert
        _repository.Verify(x => x.ToggleAvailabilityAsync(id), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveDesignAsync_WithImage_UploadsThenSaves(bool update)
    {
        // Arrange
        var selectedTagIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        using var source = new MemoryStream([1]);
        using var processed = new MemoryStream([2]);
        var design = new DesignModel { Id = Guid.NewGuid(), ImageName = "old.webp" };
        var saved = new DesignModel { Id = design.Id };
        ArrangeUpload(source, processed);
        if (update)
        {
            _repository.Setup(x => x.UpdateDesignAsync(design.Id, design))
                .Callback(() => Assert.Equal("new.webp", design.ImageName)).ReturnsAsync(saved);
            _images.Setup(x => x.DeleteImageAsync("old.webp", CancellationToken.None))
                .Callback(() => _repository.Verify(x => x.UpdateDesignAsync(design.Id, design), Times.Once))
                .Returns(Task.CompletedTask);
        }
        else
            _repository.Setup(x => x.CreateDesignAsync(design, selectedTagIds))
                .Callback(() => Assert.Equal("new.webp", design.ImageName)).ReturnsAsync(saved);
        var sut = CreateService();

        // Act
        var result = update ? await sut.UpdateDesignAsync(design.Id, design, source) : await sut.CreateDesignAsync(design, source, selectedTagIds);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ImageError);
        Assert.Same(saved, result.Design);
        Assert.False(processed.CanRead);
        Assert.True(source.CanRead);
        if (!update) _repository.Verify(x => x.CreateDesignAsync(design, selectedTagIds), Times.Once);
        _processor.Verify(x => x.ProcessImage(source), Times.Once);
        _images.Verify(x => x.UploadImage(processed, "image/webp", ".webp", CancellationToken.None), Times.Once);
        if (update) _images.Verify(x => x.DeleteImageAsync("old.webp", CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SaveDesignAsync_WhenSaveFails_CleansUploadAndPreservesOriginalException(bool update, bool cleanupFails)
    {
        // Arrange
        using var source = new MemoryStream([1]);
        using var processed = new MemoryStream([2]);
        var id = Guid.NewGuid();
        var design = new DesignModel { Id = id, ImageName = "old.webp" };
        var failure = new InvalidOperationException("Save failed");
        ArrangeUpload(source, processed);
        if (update) _repository.Setup(x => x.UpdateDesignAsync(id, design)).ThrowsAsync(failure);
        else _repository.Setup(x => x.CreateDesignAsync(design, It.IsAny<IReadOnlyCollection<Guid>>())).ThrowsAsync(failure);
        var cleanup = _images.Setup(x => x.DeleteImageAsync("new.webp", CancellationToken.None));
        if (cleanupFails) cleanup.ThrowsAsync(new IOException("Cleanup failed"));
        else cleanup.Returns(Task.CompletedTask);
        var sut = CreateService();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => update
            ? sut.UpdateDesignAsync(id, design, source) : sut.CreateDesignAsync(design, source, Array.Empty<Guid>()));

        // Assert
        Assert.Same(failure, exception);
        Assert.Equal("old.webp", design.ImageName);
        Assert.Equal(update ? id : Guid.Empty, design.Id);
        Assert.False(processed.CanRead);
        _images.Verify(x => x.DeleteImageAsync("new.webp", CancellationToken.None), Times.Once);
        _images.Verify(x => x.DeleteImageAsync("old.webp", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SaveDesignAsync_WhenImagePreparationFails_DoesNotSaveOrDelete(bool update, bool uploadFails)
    {
        // Arrange
        using var source = new MemoryStream([1]);
        using var processed = new MemoryStream([2]);
        var design = new DesignModel { Id = Guid.NewGuid(), ImageName = "old.webp" };
        var id = design.Id;
        var failure = new InvalidOperationException("Image failed");
        if (uploadFails)
        {
            _processor.Setup(x => x.ProcessImage(source)).Returns(ImageProcessingResult.Success(new ProcessedImage
            { Content = processed, ContentType = "image/webp", Extension = ".webp" }));
            _images.Setup(x => x.UploadImage(processed, "image/webp", ".webp", CancellationToken.None)).ThrowsAsync(failure);
        }
        else _processor.Setup(x => x.ProcessImage(source)).Throws(failure);
        var sut = CreateService();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => update
            ? sut.UpdateDesignAsync(id, design, source) : sut.CreateDesignAsync(design, source, Array.Empty<Guid>()));

        // Assert
        Assert.Same(failure, exception);
        Assert.Equal(id, design.Id);
        Assert.Equal("old.webp", design.ImageName);
        if (uploadFails) Assert.False(processed.CanRead);
        _repository.VerifyNoOtherCalls();
        _images.Verify(x => x.DeleteImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDesignAsync_WithoutImage_SavesWithoutImageOperations()
    {
        // Arrange
        var id = Guid.NewGuid();
        var design = new DesignModel { ImageName = "existing.webp" };
        _repository.Setup(x => x.UpdateDesignAsync(id, design)).ReturnsAsync(design);
        var sut = CreateService();

        // Act
        var result = await sut.UpdateDesignAsync(id, design, null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ImageError);
        Assert.Same(design, result.Design);
        _repository.Verify(x => x.UpdateDesignAsync(id, design), Times.Once);
        _processor.VerifyNoOtherCalls();
        _images.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteDesignAsync_ReturnsWhetherDesignExists(bool exists)
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.Setup(x => x.GetDesignByIdAsync(id)).ReturnsAsync(exists ? new DesignModel { ImageName = "image.webp" } : null);
        if (exists)
        {
            _images.Setup(x => x.DeleteImageAsync("image.webp", CancellationToken.None)).Returns(Task.CompletedTask);
            _repository.Setup(x => x.DeleteDesignAsync(id))
                .Callback(() => _images.Verify(x => x.DeleteImageAsync("image.webp", CancellationToken.None), Times.Once)).ReturnsAsync(true);
        }
        var sut = CreateService();

        // Act
        var result = await sut.DeleteDesignAsync(id);

        // Assert
        Assert.Equal(exists, result);
        _repository.Verify(x => x.DeleteDesignAsync(id), exists ? Times.Once() : Times.Never());
        _images.Verify(x => x.DeleteImageAsync("image.webp", CancellationToken.None), exists ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task DeleteDesignAsync_WhenImageDeletionFails_DoesNotDeleteRecord()
    {
        // Arrange
        var id = Guid.NewGuid();
        var failure = new IOException("Storage unavailable");
        _repository.Setup(x => x.GetDesignByIdAsync(id)).ReturnsAsync(new DesignModel { ImageName = "image.webp" });
        _images.Setup(x => x.DeleteImageAsync("image.webp", CancellationToken.None)).ThrowsAsync(failure);
        var sut = CreateService();

        // Act
        var exception = await Assert.ThrowsAsync<IOException>(() => sut.DeleteDesignAsync(id));

        // Assert
        Assert.Same(failure, exception);
        _repository.Verify(x => x.DeleteDesignAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDesignAsync_WhenOldImageDeletionFails_PreservesNewImage()
    {
        // Arrange
        using var source = new MemoryStream([1]);
        using var processed = new MemoryStream([2]);
        var id = Guid.NewGuid();
        var design = new DesignModel { Id = id, ImageName = "old.webp" };
        ArrangeUpload(source, processed);
        _repository.Setup(x => x.UpdateDesignAsync(id, design)).ReturnsAsync(design);
        _images.Setup(x => x.DeleteImageAsync("old.webp", CancellationToken.None)).ThrowsAsync(new IOException("Deletion failed"));
        _images.Setup(x => x.DeleteImageAsync("new.webp", CancellationToken.None)).Returns(Task.CompletedTask);
        var sut = CreateService();

        // Act
        var result = await sut.UpdateDesignAsync(id, design, source);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ImageError);
        Assert.Same(design, result.Design);
        _repository.Verify(x => x.UpdateDesignAsync(id, design), Times.Once);
        _images.Verify(x => x.DeleteImageAsync("old.webp", CancellationToken.None), Times.Once);
        _images.Verify(x => x.DeleteImageAsync("new.webp", It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("new.webp", design.ImageName);
        Assert.Equal(id, design.Id);
    }

    [Theory]
    [InlineData(false, ImageProcessingError.InvalidImage)]
    [InlineData(false, ImageProcessingError.UnsupportedFormat)]
    [InlineData(false, ImageProcessingError.DimensionsExceeded)]
    [InlineData(false, ImageProcessingError.PixelLimitExceeded)]
    [InlineData(true, ImageProcessingError.InvalidImage)]
    [InlineData(true, ImageProcessingError.UnsupportedFormat)]
    [InlineData(true, ImageProcessingError.DimensionsExceeded)]
    [InlineData(true, ImageProcessingError.PixelLimitExceeded)]
    public async Task SaveDesignAsync_WhenImageValidationFails_ReturnsErrorWithoutSideEffects(bool update, ImageProcessingError error)
    {
        // Arrange
        using var source = new MemoryStream([1]);
        var id = Guid.NewGuid();
        var created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var design = new DesignModel
        {
            Id = id,
            Name = "Rose",
            Price = 100,
            ImageName = "old.webp",
            IsAvailable = true,
            Created = created,
            Updated = created
        };
        _processor.Setup(x => x.ProcessImage(source)).Returns(ImageProcessingResult.Failure(error));
        var sut = CreateService();

        // Act
        var result = update
            ? await sut.UpdateDesignAsync(id, design, source)
            : await sut.CreateDesignAsync(design, source, Array.Empty<Guid>());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Design);
        Assert.Equal(error, result.ImageError);
        Assert.Equal(id, design.Id);
        Assert.Equal("old.webp", design.ImageName);
        Assert.Equal("Rose", design.Name);
        Assert.Equal(100, design.Price);
        Assert.True(design.IsAvailable);
        Assert.Equal(created, design.Created);
        Assert.Equal(created, design.Updated);
        Assert.True(source.CanRead);
        _processor.Verify(x => x.ProcessImage(source), Times.Once);
        _images.VerifyNoOtherCalls();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateDesignAsync_WithoutImage_WhenRepositoryFails_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var design = new DesignModel { Id = id, ImageName = "old.webp" };
        var failure = new KeyNotFoundException("Design not found");
        _repository.Setup(x => x.UpdateDesignAsync(id, design)).ThrowsAsync(failure);
        var sut = CreateService();

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateDesignAsync(id, design, null));

        // Assert
        Assert.Same(failure, exception);
        Assert.Equal(id, design.Id);
        Assert.Equal("old.webp", design.ImageName);
        _images.VerifyNoOtherCalls();
        _processor.VerifyNoOtherCalls();
    }

    private void ArrangeUpload(Stream source, Stream processed)
    {
        _processor.Setup(x => x.ProcessImage(source)).Returns(ImageProcessingResult.Success(new ProcessedImage
        { Content = processed, ContentType = "image/webp", Extension = ".webp" }));
        _images.Setup(x => x.UploadImage(processed, "image/webp", ".webp", CancellationToken.None)).ReturnsAsync("new.webp");
    }
}
