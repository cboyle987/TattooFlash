using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Enums;
using TattooFlash.Web.Models;
using TattooFlash.Web.Interfaces;

using static TattooFlash.UnitTest.Components.Pages.Management.ManagementPageTestSupport;
using TattooFlash.Web.Components.Pages.Management.Design;

namespace TattooFlash.UnitTest.Components.Pages.Management;

public class EditDesignTests
{
    [Fact]
    public async Task WhenParametersChangeThenLoadingIsShownAndTheRequestedDesignIsLoaded()
    {
        // Arrange
        var first = CreateDesign();
        var second = CreateDesign("Rose");
        var firstLoad = new TaskCompletionSource<DesignModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondLoad = new TaskCompletionSource<DesignModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(first.Id)).Returns(firstLoad.Task);
        service.Setup(x => x.GetDesignByIdAsync(second.Id)).Returns(secondLoad.Task);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, first.Id));
        AssertLoading(page);
        firstLoad.SetResult(first);
        page.WaitForAssertion(() => Assert.Same(first, page.FindComponent<DesignInput>().Instance.Design));

        // Act
        page.Render(p => p.Add(x => x.DesignId, second.Id));
        AssertLoading(page);
        Assert.Null(page.Instance.Design);
        secondLoad.SetResult(second);

        // Assert
        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindComponents<MudProgressCircular>());
            Assert.Same(second, page.FindComponent<DesignInput>().Instance.Design);
            Assert.Equal("Edit Design", page.Find("h3").TextContent);
        });
        service.Verify(x => x.GetDesignByIdAsync(first.Id), Times.Once);
        service.Verify(x => x.GetDesignByIdAsync(second.Id), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenUpdatingWithoutAReplacementThenNoImageStreamIsPassed(bool fails)
    {
        // Arrange
        var selectedTagIds = new List<Guid> { Guid.NewGuid() };
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(design.Id)).ReturnsAsync(design);
        if (fails)
            service.Setup(x => x.UpdateDesignAsync(design.Id, design, null, selectedTagIds)).ThrowsAsync(new InvalidOperationException("Save failed"));
        else
            service.Setup(x => x.UpdateDesignAsync(design.Id, design, null, selectedTagIds)).ReturnsAsync(DesignSaveResult.Success(design));
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, design.Id));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;

        // Act
        await SubmitAsync(page, new DesignSumbission { Design = design, SelectedTagIds = selectedTagIds });

        // Assert
        service.Verify(x => x.UpdateDesignAsync(design.Id, design, null, selectedTagIds), Times.Once);
        if (fails)
        {
            Assert.Contains("An Error has occurred saving your design please try again later.", page.FindComponent<MudAlert>().Markup);
            Assert.Equal(originalUri, navigation.Uri);
        }
        else
        {
            Assert.Empty(page.FindComponents<MudAlert>());
            Assert.Equal("http://localhost/management/design", navigation.Uri);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenSubmissionOrItsDesignIsNullThenSavingIsRejected(bool nullSubmission)
    {
        // Arrange
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(design.Id)).ReturnsAsync(design);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, design.Id));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;

        // Act: the child callback triggers the page's normal event-driven render.
        await SubmitAsync(page, nullSubmission ? null! : new DesignSumbission { Design = null! });

        // Assert
        Assert.Contains("Design is null. Cannot save.", page.FindComponent<MudAlert>().Markup);
        Assert.Equal(originalUri, navigation.Uri);
        service.Verify(x => x.GetDesignByIdAsync(design.Id), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, "Design not found.")]
    [InlineData(true, "An error occurred while loading the design. Please try again later.")]
    public async Task WhenLookupReturnsNoDesignOrFailsThenErrorIsShownWithoutTheRealForm(bool fails, string error)
    {
        // Arrange: keep the real DesignInput so rendering it with null would fail this test.
        var id = Guid.NewGuid();
        var load = new TaskCompletionSource<DesignModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(id)).Returns(load.Task);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, id));
        AssertLoading(page);

        // Act
        if (fails)
            load.SetException(new InvalidOperationException("Load failed"));
        else
            load.SetResult(null);

        // Assert
        page.WaitForAssertion(() =>
        {
            Assert.Null(page.Instance.Design);
            Assert.Empty(page.FindComponents<MudProgressCircular>());
            Assert.Empty(page.FindComponents<DesignInput>());
            Assert.Empty(page.FindAll("form"));
            var alert = page.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains(error, alert.Markup);
        });
        service.Verify(x => x.GetDesignByIdAsync(id), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, "Design not found.")]
    [InlineData(true, "An error occurred while loading the design. Please try again later.")]
    public async Task WhenNextLookupReturnsNoDesignOrFailsThenPreviousDesignIsCleared(bool fails, string error)
    {
        // Arrange
        var first = CreateDesign();
        var nextId = Guid.NewGuid();
        var nextLoad = new TaskCompletionSource<DesignModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(first.Id)).ReturnsAsync(first);
        service.Setup(x => x.GetDesignByIdAsync(nextId)).Returns(nextLoad.Task);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, first.Id));
        Assert.Same(first, page.FindComponent<DesignInput>().Instance.Design);

        // Act
        page.Render(p => p.Add(x => x.DesignId, nextId));
        AssertLoading(page);
        Assert.Null(page.Instance.Design);
        if (fails)
            nextLoad.SetException(new InvalidOperationException("Load failed"));
        else
            nextLoad.SetResult(null);

        // Assert
        page.WaitForAssertion(() =>
        {
            Assert.Null(page.Instance.Design);
            Assert.Empty(page.FindComponents<MudProgressCircular>());
            Assert.Empty(page.FindComponents<DesignInput>());
            var alert = page.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains(error, alert.Markup);
        });
        service.Verify(x => x.GetDesignByIdAsync(first.Id), Times.Once);
        service.Verify(x => x.GetDesignByIdAsync(nextId), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, "Design not found.")]
    [InlineData(true, "An error occurred while loading the design. Please try again later.")]
    public async Task WhenParametersChangeAfterALoadErrorThenTheNextDesignCanBeEdited(bool fails, string error)
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var next = CreateDesign();
        var nextLoad = new TaskCompletionSource<DesignModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        if (fails)
            service.Setup(x => x.GetDesignByIdAsync(firstId)).ThrowsAsync(new InvalidOperationException("Load failed"));
        else
            service.Setup(x => x.GetDesignByIdAsync(firstId)).ReturnsAsync((DesignModel?)null);
        service.Setup(x => x.GetDesignByIdAsync(next.Id)).Returns(nextLoad.Task);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, firstId));
        Assert.Contains(error, page.FindComponent<MudAlert>().Markup);
        Assert.Empty(page.FindComponents<DesignInput>());

        // Act
        page.Render(p => p.Add(x => x.DesignId, next.Id));
        AssertLoading(page);
        Assert.Empty(page.FindComponents<MudAlert>());
        nextLoad.SetResult(next);

        // Assert
        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindComponents<MudProgressCircular>());
            Assert.Empty(page.FindComponents<MudAlert>());
            Assert.Same(next, page.FindComponent<DesignInput>().Instance.Design);
        });
        service.Verify(x => x.GetDesignByIdAsync(firstId), Times.Once);
        service.Verify(x => x.GetDesignByIdAsync(next.Id), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WhenParametersChangeAfterASaveErrorThenTheErrorIsClearedForTheNextDesign()
    {
        // Arrange
        var first = CreateDesign();
        var next = CreateDesign("Rose");
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(first.Id)).ReturnsAsync(first);
        service.Setup(x => x.GetDesignByIdAsync(next.Id)).ReturnsAsync(next);
        service.Setup(x => x.UpdateDesignAsync(first.Id, first, null, Array.Empty<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Save failed"));
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, first.Id));
        await SubmitAsync(page, new DesignSumbission { Design = first });
        Assert.Contains("An Error has occurred saving your design please try again later.", page.FindComponent<MudAlert>().Markup);

        // Act
        page.Render(p => p.Add(x => x.DesignId, next.Id));

        // Assert
        Assert.Same(next, page.FindComponent<DesignInput>().Instance.Design);
        Assert.Empty(page.FindComponents<MudAlert>());
        service.Verify(x => x.GetDesignByIdAsync(first.Id), Times.Once);
        service.Verify(x => x.GetDesignByIdAsync(next.Id), Times.Once);
        service.Verify(x => x.UpdateDesignAsync(first.Id, first, null, Array.Empty<Guid>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("success", null)]
    [InlineData("invalid", "The selected file is not a valid image. Please choose another image.")]
    [InlineData("unsupported", "This image format is not supported. Please choose a JPEG, PNG or WebP image.")]
    [InlineData("dimensions", "The image is too wide or too tall. Please resize it to no more than 8000 pixels in either dimension.")]
    [InlineData("pixels", "The image exceeds the 25 megapixel limit. Please reduce its dimensions and try again.")]
    [InlineData("service-error", "An Error has occurred saving your design please try again later.")]
    [InlineData("read-error", "An Error has occurred saving your design please try again later.")]
    public async Task WhenImageIsSubmittedThenResultControlsNavigationAndError(string outcome, string? error)
    {
        // Arrange
        var selectedTagIds = new List<Guid> { Guid.NewGuid() };
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(design.Id)).ReturnsAsync(design);
        var bytes = new byte[] { 1, 2, 3, 4 };
        var file = CreateBrowserFile(bytes);
        if (outcome == "read-error")
            file.Setup(x => x.OpenReadStream(10 * 1024 * 1024, It.IsAny<CancellationToken>()))
                .Throws(new IOException("Read failed"));
        Stream? submittedStream = null;
        if (outcome != "read-error")
        {
            var save = service.Setup(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>(), selectedTagIds))
                .Callback((Guid id, DesignModel model, Stream? stream, IReadOnlyCollection<Guid> tagIds) =>
                {
                    AssertBufferedImage(design, model, stream, bytes);
                    Assert.Equal(selectedTagIds, tagIds);
                    submittedStream = stream;
                });
            if (outcome == "service-error")
                save.ThrowsAsync(new InvalidOperationException("Save failed"));
            else
                save.ReturnsAsync(outcome switch
                {
                    "invalid" => DesignSaveResult.Failure(ImageProcessingError.InvalidImage),
                    "unsupported" => DesignSaveResult.Failure(ImageProcessingError.UnsupportedFormat),
                    "dimensions" => DesignSaveResult.Failure(ImageProcessingError.DimensionsExceeded),
                    "pixels" => DesignSaveResult.Failure(ImageProcessingError.PixelLimitExceeded),
                    _ => DesignSaveResult.Success(design)
                });
        }
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, design.Id));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;
        var submission = new DesignSumbission { Design = design, SelectedImage = file.Object, SelectedTagIds = selectedTagIds };

        // Act: test the page's wiring to the child form's public callback.
        await SubmitAsync(page, submission);

        // Assert
        AssertSaveOutcome(page, navigation, originalUri, error);
        file.Verify(x => x.OpenReadStream(10 * 1024 * 1024, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>(), selectedTagIds), outcome == "read-error" ? Times.Never() : Times.Once());
        if (submittedStream is not null)
            Assert.False(submittedStream.CanRead);
    }

    [Fact]
    public async Task WhenSaveIsRetriedSuccessfullyThenPreviousErrorIsCleared()
    {
        // Arrange
        var selectedTagIds = new List<Guid> { Guid.NewGuid() };
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(design.Id)).ReturnsAsync(design);
        service.SetupSequence(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>(), selectedTagIds))
            .ReturnsAsync(DesignSaveResult.Failure(ImageProcessingError.InvalidImage))
            .ReturnsAsync(DesignSaveResult.Success(design));
        var file = CreateBrowserFile([1, 2, 3]);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, design.Id));
        var submission = new DesignSumbission { Design = design, SelectedImage = file.Object, SelectedTagIds = selectedTagIds };
        await SubmitAsync(page, submission);
        Assert.Single(page.FindComponents<MudAlert>());

        // Act
        await SubmitAsync(page, submission);

        // Assert
        Assert.Empty(page.FindComponents<MudAlert>());
        Assert.Equal("http://localhost/management/design", context.Services.GetRequiredService<NavigationManager>().Uri);
        service.Verify(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>(), selectedTagIds), Times.Exactly(2));
    }

    private static void AssertLoading(IRenderedComponent<EditDesign> page)
    {
        Assert.Single(page.FindComponents<MudProgressCircular>());
        Assert.Empty(page.FindComponents<DesignInput>());
    }
}