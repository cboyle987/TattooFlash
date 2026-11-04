using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Components.Pages.Management;
using TattooFlash.Web.Enums;
using TattooFlash.Web.Models;
using TattooFlash.Web.Interfaces;

using static TattooFlash.UnitTest.Components.Pages.Management.ManagementPageTestSupport;

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
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(design.Id)).ReturnsAsync(design);
        if (fails)
            service.Setup(x => x.UpdateDesignAsync(design.Id, design, null)).ThrowsAsync(new InvalidOperationException("Save failed"));
        else
            service.Setup(x => x.UpdateDesignAsync(design.Id, design, null)).ReturnsAsync(DesignSaveResult.Success(design));
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, design.Id));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;

        // Act
        await SubmitAsync(page, new DesignSumbission { Design = design });

        // Assert
        service.Verify(x => x.UpdateDesignAsync(design.Id, design, null), Times.Once);
        if (fails)
        {
            Assert.Contains("An Error has occurred saving your design please try again later.", page.FindComponent<MudAlert>().Markup);
            Assert.Equal(originalUri, navigation.Uri);
        }
        else
        {
            Assert.Empty(page.FindComponents<MudAlert>());
            Assert.Equal("http://localhost/management", navigation.Uri);
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

    [Fact]
    public async Task WhenLookupFailsThenPageSetsItsLoadError()
    {
        // Arrange: isolate page error handling from DesignInput's non-null parameter requirement.
        var id = Guid.NewGuid();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(id)).ThrowsAsync(new InvalidOperationException("Load failed"));
        await using var context = CreateContext(service);
        context.ComponentFactories.AddStub<DesignInput>();

        // Act
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, id));

        // Assert
        Assert.Null(page.Instance.Design);
        Assert.Empty(page.FindComponents<MudProgressCircular>());
        var alert = page.FindComponent<MudAlert>();
        Assert.Equal(Severity.Error, alert.Instance.Severity);
        Assert.Contains("An error occurred while loading the design. Please try again later.", alert.Markup);
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
            var save = service.Setup(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>()))
                .Callback((Guid id, DesignModel model, Stream? stream) =>
                {
                    AssertBufferedImage(design, model, stream, bytes);
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
        var submission = new DesignSumbission { Design = design, SelectedImage = file.Object };

        // Act: test the page's wiring to the child form's public callback.
        await SubmitAsync(page, submission);

        // Assert
        AssertSaveOutcome(page, navigation, originalUri, error);
        file.Verify(x => x.OpenReadStream(10 * 1024 * 1024, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>()), outcome == "read-error" ? Times.Never() : Times.Once());
        if (submittedStream is not null)
            Assert.False(submittedStream.CanRead);
    }

    [Fact]
    public async Task WhenSaveIsRetriedSuccessfullyThenPreviousErrorIsCleared()
    {
        // Arrange
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetDesignByIdAsync(design.Id)).ReturnsAsync(design);
        service.SetupSequence(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>()))
            .ReturnsAsync(DesignSaveResult.Failure(ImageProcessingError.InvalidImage))
            .ReturnsAsync(DesignSaveResult.Success(design));
        var file = CreateBrowserFile([1, 2, 3]);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<EditDesign>(p => p.Add(x => x.DesignId, design.Id));
        var submission = new DesignSumbission { Design = design, SelectedImage = file.Object };
        await SubmitAsync(page, submission);
        Assert.Single(page.FindComponents<MudAlert>());

        // Act
        await SubmitAsync(page, submission);

        // Assert
        Assert.Empty(page.FindComponents<MudAlert>());
        Assert.Equal("http://localhost/management", context.Services.GetRequiredService<NavigationManager>().Uri);
        service.Verify(x => x.UpdateDesignAsync(design.Id, design, It.IsAny<Stream?>()), Times.Exactly(2));
    }

    private static void AssertLoading(IRenderedComponent<EditDesign> page)
    {
        Assert.Single(page.FindComponents<MudProgressCircular>());
        Assert.Empty(page.FindComponents<DesignInput>());
    }
}
