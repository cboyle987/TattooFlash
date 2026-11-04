using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TattooFlash.Web.Components.Dialogs;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Dialogs;

public class BookingDialogTests
{
    [Theory]
    [InlineData("Swallow", 125, "swallow.webp", "tattoo_artist")]
    [InlineData("Rose", 0, "rose.webp", "another.artist")]
    public async Task WhenOpenedThenDesignDetailsAndConfiguredBookingLinkAreDisplayed(
        string name, int price, string imageName, string username)
    {
        // Arrange
        var design = new DesignModel { Id = Guid.NewGuid(), Name = name, Price = price, ImageName = imageName };
        var images = new Mock<IImageService>(MockBehavior.Strict);
        images.Setup(x => x.GetImageUrl(imageName)).Returns($"https://images.example/{imageName}");
        await using var context = CreateContext(images.Object, username);

        // Act: open through the real service so MudDialog receives its cascading dialog instance.
        var provider = await OpenDialogAsync(context, design);

        // Assert
        provider.WaitForAssertion(() =>
        {
            var dialog = provider.FindComponent<BookingDialog>();
            Assert.Same(design, dialog.Instance.Design);
            Assert.Equal(name, provider.Find("h4").TextContent);
            Assert.Equal($"Price: \u00a3{price}", provider.Find("h5").TextContent.Trim());
            Assert.Equal($"https://images.example/{imageName}", provider.Find("img").GetAttribute("src"));
            Assert.Equal(name, provider.Find("img").GetAttribute("alt"));
            Assert.Contains("Please include a screenshot of this design", provider.Markup);
            var bookingLink = provider.Find("a");
            Assert.Equal("Book Now", bookingLink.TextContent.Trim());
            Assert.Equal($"https://ig.me/m/{username}", bookingLink.GetAttribute("href"));
            Assert.Equal("_blank", bookingLink.GetAttribute("target"));
        });
        images.Verify(x => x.GetImageUrl(imageName), Times.AtLeastOnce);
        images.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WhenBookingUsernameIsMissingThenRenderingReportsTheMissingConfiguration()
    {
        // Arrange
        await using var context = CreateContext(Mock.Of<IImageService>());
        var design = new DesignModel { Name = "Swallow", ImageName = "swallow.webp" };

        // Act
        await OpenDialogAsync(context, design);
        var exception = await context.Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("Missing Booking:InstagramUserName configuration.", exception.Message);
    }

    // Each caller owns a fresh context and disposes it asynchronously.
    private static ComponentTestContext CreateContext(IImageService images, string? username = null)
    {
        var settings = new Dictionary<string, string?>();
        if (username is not null)
            settings["Booking:InstagramUserName"] = username;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var context = new ComponentTestContext();
        context.Services.AddSingleton<IConfiguration>(configuration);
        context.Services.AddSingleton(images);
        return context;
    }

    private static async Task<IRenderedComponent<MudDialogProvider>> OpenDialogAsync(
        ComponentTestContext context, DesignModel design)
    {
        context.Render<MudPopoverProvider>();
        var provider = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<BookingDialog> { { x => x.Design, design } };
        await provider.InvokeAsync(() => dialogs.ShowAsync<BookingDialog>("Booking", parameters));
        // MudBlazor renders the visible dialog content through this provider.
        return provider;
    }
}
