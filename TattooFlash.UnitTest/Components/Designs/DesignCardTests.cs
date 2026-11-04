using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Extensions;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Components.Dialogs;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Designs;

public class DesignCardTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenDesignIsRenderedThenDetailsAndAvailabilityAreDisplayed(bool isAvailable)
    {
        // Arrange
        var design = CreateDesign(isAvailable);
        var imageService = CreateImageService(design);
        await using var context = CreateContext(imageService);

        // Act
        var card = context.RenderWithProviders<DesignCard>(parameters => parameters.Add(x => x.Design, design));

        // Assert
        Assert.Equal(design.Name, card.Find("h6").TextContent);
        Assert.Contains("\u00a3125", card.FindComponent<MudCardContent>().Markup);
        var image = card.Find("img");
        Assert.Equal("https://images.example/swallow.webp", image.GetAttribute("src"));
        Assert.Equal(design.Name, image.GetAttribute("alt"));
        Assert.Equal("lazy", image.GetAttribute("loading"));
        var button = card.Find("button");
        Assert.Equal("Details & booking", button.TextContent.Trim());
        Assert.Equal(!isAvailable, button.HasAttribute("disabled"));
        Assert.Equal(!isAvailable, card.FindComponent<MudButton>().Instance.Disabled);

        if (isAvailable)
        {
            Assert.Empty(card.FindComponents<MudOverlay>());
            Assert.Empty(card.FindComponents<MudChip<string>>());
        }
        else
        {
            Assert.True(card.FindComponent<MudOverlay>().Instance.GetState(x => x.Visible));
            Assert.Contains("Not available", card.FindComponent<MudChip<string>>().Markup);
        }
        imageService.Verify(x => x.GetImageUrl(design.ImageName), Times.AtLeastOnce);
        imageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WhenAvailableDesignIsClickedThenBookingDialogReceivesDesignAndOptions()
    {
        // Arrange
        var design = CreateDesign(isAvailable: true);
        var imageService = CreateImageService(design);
        var dialogService = new Mock<IDialogService>(MockBehavior.Strict);
        dialogService.Setup(x => x.ShowAsync<BookingDialog>(
                It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(Mock.Of<IDialogReference>());
        await using var context = CreateContext(imageService, dialogService);
        var card = context.RenderWithProviders<DesignCard>(parameters => parameters.Add(x => x.Design, design));

        // Act
        await card.Find("button").ClickAsync(new());

        // Assert
        dialogService.Verify(x => x.ShowAsync<BookingDialog>(
            "Booking",
            It.Is<DialogParameters>(p => ReferenceEquals(p.Get<DesignModel>(nameof(BookingDialog.Design)), design)),
            It.Is<DialogOptions>(o => o.MaxWidth == MaxWidth.ExtraLarge && o.CloseButton == true)), Times.Once);
    }

    [Fact]
    public async Task WhenUnavailableDesignIsClickedThenBookingDialogIsNotOpened()
    {
        // Arrange
        var design = CreateDesign(isAvailable: false);
        var imageService = CreateImageService(design);
        var dialogService = new Mock<IDialogService>(MockBehavior.Strict);
        await using var context = CreateContext(imageService, dialogService);
        var card = context.RenderWithProviders<DesignCard>(parameters => parameters.Add(x => x.Design, design));

        // Act: bUnit can dispatch the event; MudButton must still respect its disabled state.
        await card.Find("button").ClickAsync(new());

        // Assert
        Assert.True(card.Find("button").HasAttribute("disabled"));
        dialogService.Verify(x => x.ShowAsync<BookingDialog>(
            It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()), Times.Never);
    }

    [Fact]
    public async Task WhenDesignParameterChangesThenCardDisplaysTheNewDesign()
    {
        // Arrange
        var original = CreateDesign(isAvailable: true);
        var replacement = new DesignModel { Name = "Rose", Price = 80, ImageName = "rose.webp", IsAvailable = false };
        var imageService = CreateImageService(original, replacement);
        await using var context = CreateContext(imageService);
        var card = context.RenderWithProviders<DesignCard>(parameters => parameters.Add(x => x.Design, original));

        // Act
        card.Render(parameters => parameters.Add(x => x.Design, replacement));

        // Assert
        Assert.Equal(replacement.Name, card.Find("h6").TextContent);
        Assert.Contains("\u00a380", card.FindComponent<MudCardContent>().Markup);
        Assert.Equal("https://images.example/rose.webp", card.Find("img").GetAttribute("src"));
        Assert.Equal(replacement.Name, card.Find("img").GetAttribute("alt"));
        Assert.True(card.Find("button").HasAttribute("disabled"));
        Assert.Contains("Not available", card.FindComponent<MudChip<string>>().Markup);
    }

    private static DesignModel CreateDesign(bool isAvailable) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Swallow",
        Price = 125,
        ImageName = "swallow.webp",
        IsAvailable = isAvailable
    };

    private static Mock<IImageService> CreateImageService(params DesignModel[] designs)
    {
        var imageService = new Mock<IImageService>(MockBehavior.Strict);
        foreach (var design in designs)
        {
            imageService.Setup(x => x.GetImageUrl(design.ImageName))
                .Returns($"https://images.example/{design.ImageName}");
        }
        return imageService;
    }

    // The caller owns a fresh context and disposes it asynchronously in each test.
    private static ComponentTestContext CreateContext(
        Mock<IImageService> imageService, Mock<IDialogService>? dialogService = null)
    {
        var context = new ComponentTestContext();
        context.Services.AddSingleton(imageService.Object);
        if (dialogService is not null)
            context.Services.AddSingleton(dialogService.Object);
        return context;
    }
}