using Moq;
using TattooFlash.Web.Models;
using TattooFlash.Web.Repositories;
using TattooFlash.Web.Services;

namespace TattooFlash.UnitTest.Services;

public class TagServiceTests
{
    private readonly Mock<ITagRepository> _repository = new(MockBehavior.Strict);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAllTagsAsync_ReturnsRepositoryResultsIncludingAnEmptyCatalogue(bool populated)
    {
        // Arrange
        var expected = populated
            ? new List<TagModel> { new() { DispalyName = "Flowers", NormalizedName = "FLOWERS" } }
            : [];
        _repository.Setup(r => r.GetAllTags()).ReturnsAsync(expected);
        var sut = new TagService(_repository.Object);

        // Act
        var result = await sut.GetAllTagsAsync();

        // Assert
        Assert.Same(expected, result);
        _repository.Verify(r => r.GetAllTags(), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetTagByIdAsync_ForwardsIdentifierAndReturnsTagOrNull(bool exists)
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = exists ? new TagModel { Id = id } : null;
        _repository.Setup(r => r.GetTagById(id)).ReturnsAsync(expected);
        var sut = new TagService(_repository.Object);

        // Act
        var result = await sut.GetTagByIdAsync(id);

        // Assert
        Assert.Same(expected, result);
        _repository.Verify(r => r.GetTagById(id), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteTagAsync_ForwardsIdentifierAndReturnsDeletionOutcome(bool deleted)
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.Setup(r => r.DeleteTag(id)).ReturnsAsync(deleted);
        var sut = new TagService(_repository.Object);

        // Act
        var result = await sut.DeleteTagAsync(id);

        // Assert
        Assert.Equal(deleted, result);
        _repository.Verify(r => r.DeleteTag(id), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveTagAsync_TrimsNamePreservesDisplaySpellingAndNormalizesBeforePersistence(bool update)
    {
        // Arrange
        var id = Guid.NewGuid();
        TagModel? submitted = null;
        var saved = new TagModel { Id = id, DispalyName = "Old School", NormalizedName = "OLD SCHOOL" };
        var expected = TagSaveResult.Success(saved);
        if (update)
            _repository.Setup(r => r.UpdateTag(id, It.IsAny<TagModel>()))
                .Callback<Guid, TagModel>((_, tag) => submitted = tag).ReturnsAsync(expected);
        else
            _repository.Setup(r => r.CreateTag(It.IsAny<TagModel>()))
                .Callback<TagModel>(tag => submitted = tag).ReturnsAsync(expected);
        var sut = new TagService(_repository.Object);

        // Act
        var result = update ? await sut.UpdateTagAsync(id, " \tOld School\n ")
            : await sut.CreateTagAsync(" \tOld School\n ");

        // Assert
        Assert.Same(expected, result);
        Assert.NotNull(submitted);
        Assert.Equal("Old School", submitted.DispalyName);
        Assert.Equal("OLD SCHOOL", submitted.NormalizedName);
        Assert.Equal(update ? id : Guid.Empty, submitted.Id);
        Assert.Empty(submitted.Designs);
        if (update)
            _repository.Verify(r => r.UpdateTag(id, submitted), Times.Once);
        else
            _repository.Verify(r => r.CreateTag(submitted), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, "   ")]
    [InlineData(true, "   ")]
    [InlineData(false, "\t\r\n")]
    [InlineData(true, "\t\r\n")]
    public async Task SaveTagAsync_WhenNameIsMissing_ReturnsValidationFailureWithoutCallingRepository(
        bool update, string? name)
    {
        // Arrange
        var sut = new TagService(_repository.Object);

        // Act
        var result = update ? await sut.UpdateTagAsync(Guid.NewGuid(), name) : await sut.CreateTagAsync(name);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Tag);
        Assert.Equal("Tag name is required.", result.TagSaveError);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveTagAsync_WhenTrimmedNameExceeds100Characters_DoesNotCallRepository(bool update)
    {
        // Arrange
        var sut = new TagService(_repository.Object);
        var name = " " + new string('a', 101) + " ";

        // Act
        var result = update ? await sut.UpdateTagAsync(Guid.NewGuid(), name) : await sut.CreateTagAsync(name);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Tag);
        Assert.Equal("Tag name must be 100 characters or fewer.", result.TagSaveError);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 100)]
    [InlineData(true, 100)]
    public async Task SaveTagAsync_AcceptsBoundaryLengthsAfterTrimming(bool update, int length)
    {
        // Arrange
        var id = Guid.NewGuid();
        var displayName = new string('a', length);
        var normalizedName = new string('A', length);
        var expected = TagSaveResult.Success(new TagModel { Id = id });
        if (update)
            _repository.Setup(r => r.UpdateTag(id,
                It.Is<TagModel>(t => t.DispalyName == displayName && t.NormalizedName == normalizedName)))
                .ReturnsAsync(expected);
        else
            _repository.Setup(r => r.CreateTag(
                It.Is<TagModel>(t => t.DispalyName == displayName && t.NormalizedName == normalizedName)))
                .ReturnsAsync(expected);
        var sut = new TagService(_repository.Object);

        // Act
        var result = update ? await sut.UpdateTagAsync(id, " " + displayName + " ")
            : await sut.CreateTagAsync(" " + displayName + " ");

        // Assert
        Assert.Same(expected, result);
        _repository.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveTagAsync_WhenRepositoryReportsDuplicateName_ReturnsItsFailure(bool update)
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = TagSaveResult.Failure("Duplicate tag name");
        if (update)
            _repository.Setup(r => r.UpdateTag(id, It.Is<TagModel>(t => t.NormalizedName == "ROSE")))
                .ReturnsAsync(expected);
        else
            _repository.Setup(r => r.CreateTag(It.Is<TagModel>(t => t.NormalizedName == "ROSE")))
                .ReturnsAsync(expected);
        var sut = new TagService(_repository.Object);

        // Act
        var result = update ? await sut.UpdateTagAsync(id, " rose ") : await sut.CreateTagAsync(" rose ");

        // Assert
        Assert.Same(expected, result);
        Assert.False(result.IsSuccess);
        Assert.Equal("Duplicate tag name", result.TagSaveError);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateTagAsync_WhenTagIsMissing_PropagatesRepositoryException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = new KeyNotFoundException("Tag not found");
        _repository.Setup(r => r.UpdateTag(id, It.IsAny<TagModel>())).ThrowsAsync(expected);
        var sut = new TagService(_repository.Object);

        // Act
        var actual = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateTagAsync(id, "Rose"));

        // Assert
        Assert.Same(expected, actual);
        _repository.Verify(r => r.UpdateTag(id, It.IsAny<TagModel>()), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("list")]
    [InlineData("lookup")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task CatalogueOperation_WhenRepositoryFails_PropagatesOriginalException(string operation)
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = new InvalidOperationException("Database unavailable");
        var sut = new TagService(_repository.Object);
        Func<Task> action;
        switch (operation)
        {
            case "list":
                _repository.Setup(r => r.GetAllTags()).ThrowsAsync(expected);
                action = async () => await sut.GetAllTagsAsync();
                break;
            case "lookup":
                _repository.Setup(r => r.GetTagById(id)).ThrowsAsync(expected);
                action = async () => await sut.GetTagByIdAsync(id);
                break;
            case "create":
                _repository.Setup(r => r.CreateTag(It.IsAny<TagModel>())).ThrowsAsync(expected);
                action = async () => await sut.CreateTagAsync("Rose");
                break;
            case "update":
                _repository.Setup(r => r.UpdateTag(id, It.IsAny<TagModel>())).ThrowsAsync(expected);
                action = async () => await sut.UpdateTagAsync(id, "Rose");
                break;
            default:
                _repository.Setup(r => r.DeleteTag(id)).ThrowsAsync(expected);
                action = async () => await sut.DeleteTagAsync(id);
                break;
        }

        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(action);

        // Assert
        Assert.Same(expected, actual);
        _repository.VerifyAll();
    }
}