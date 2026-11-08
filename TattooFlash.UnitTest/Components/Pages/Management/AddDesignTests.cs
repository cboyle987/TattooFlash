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

public class AddDesignTests
{
    [Fact]
    public async Task WhenOpenedThenAnAvailableEmptyDesignIsPassedToTheForm()
    {
        // Arrange
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        await using var context = CreateContext(service);

        // Act
        var page = context.RenderWithProviders<AddDesign>();

        // Assert
        Assert.Equal("Add New Design", page.Find("h3").TextContent);
        var design = page.FindComponent<DesignInput>().Instance.Design;
        Assert.Same(page.Instance.NewDesign, design);
        Assert.Equal(Guid.Empty, design.Id);
        Assert.Empty(design.Name);
        Assert.Empty(design.ImageName);
        Assert.Equal(0, design.Price);
        Assert.True(design.IsAvailable);
        Assert.Empty(page.FindComponents<MudAlert>());
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WhenNoFileIsSubmittedThenErrorIsShownWithoutSavingOrNavigating()
    {
        // Arrange
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<AddDesign>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;

        // Act
        await SubmitAsync(page, new DesignSumbission { Design = page.Instance.NewDesign });

        // Assert
        Assert.Contains("Unable to read selected Image.", page.FindComponent<MudAlert>().Markup);
        Assert.Equal(originalUri, navigation.Uri);
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
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        var bytes = new byte[] { 1, 2, 3, 4 };
        var file = CreateBrowserFile(bytes);
        if (outcome == "read-error")
            file.Setup(x => x.OpenReadStream(10 * 1024 * 1024, It.IsAny<CancellationToken>()))
                .Throws(new IOException("Read failed"));
        Stream? submittedStream = null;
        if (outcome != "read-error")
        {
            var save = service.Setup(x => x.CreateDesignAsync(design, It.IsAny<Stream>()))
                .Callback((DesignModel model, Stream stream) =>
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
        var page = context.RenderWithProviders<AddDesign>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;
        var submission = new DesignSumbission { Design = design, SelectedImage = file.Object };

        // Act: test the page's wiring to the child form's public callback.
        await SubmitAsync(page, submission);

        // Assert
        AssertSaveOutcome(page, navigation, originalUri, error);
        file.Verify(x => x.OpenReadStream(10 * 1024 * 1024, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.CreateDesignAsync(design, It.IsAny<Stream>()), outcome == "read-error" ? Times.Never() : Times.Once());
        if (submittedStream is not null)
            Assert.False(submittedStream.CanRead);
    }

    [Fact]
    public async Task WhenSaveIsRetriedSuccessfullyThenPreviousErrorIsCleared()
    {
        // Arrange
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.SetupSequence(x => x.CreateDesignAsync(design, It.IsAny<Stream>()))
            .ReturnsAsync(DesignSaveResult.Failure(ImageProcessingError.InvalidImage))
            .ReturnsAsync(DesignSaveResult.Success(design));
        var file = CreateBrowserFile([1, 2, 3]);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<AddDesign>();
        var submission = new DesignSumbission { Design = design, SelectedImage = file.Object };
        await SubmitAsync(page, submission);
        Assert.Single(page.FindComponents<MudAlert>());

        // Act
        await SubmitAsync(page, submission);

        // Assert
        Assert.Empty(page.FindComponents<MudAlert>());
        Assert.Equal("http://localhost/management/design", context.Services.GetRequiredService<NavigationManager>().Uri);
        service.Verify(x => x.CreateDesignAsync(design, It.IsAny<Stream>()), Times.Exactly(2));
    }
}