using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Pages.Management;

// Shared mechanics only: each test keeps its service responses and expected outcomes visible.
internal static class ManagementPageTestSupport
{
    internal static ComponentTestContext CreateContext(Mock<IDesignService> service)
    {
        var context = new ComponentTestContext();
        context.Services.AddSingleton(service.Object);
        context.Services.AddSingleton(Mock.Of<IImageService>());
        var tags = new Mock<ITagService>(MockBehavior.Strict);
        tags.Setup(x => x.GetAllTagsAsync()).ReturnsAsync(new List<TagModel>());
        context.Services.AddSingleton(tags.Object);
        return context;
    }

    internal static DesignModel CreateDesign(string name = "Swallow", bool available = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        ImageName = "existing.webp",
        IsAvailable = available
    };

    internal static Mock<IBrowserFile> CreateBrowserFile(byte[] bytes)
    {
        var file = new Mock<IBrowserFile>(MockBehavior.Strict);
        file.Setup(x => x.OpenReadStream(10 * 1024 * 1024, It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream(bytes));
        return file;
    }

    internal static Task SubmitAsync<TPage>(IRenderedComponent<TPage> page, DesignSumbission submission)
        where TPage : class, IComponent =>
        page.InvokeAsync(() => page.FindComponent<DesignInput>().Instance.OnValidSubmit.InvokeAsync(submission));

    internal static void AssertBufferedImage(DesignModel expectedDesign, DesignModel submittedDesign, Stream? stream, byte[] expectedBytes)
    {
        Assert.Same(expectedDesign, submittedDesign);
        Assert.NotNull(stream);
        Assert.Equal(0, stream.Position);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        Assert.Equal(expectedBytes, copy.ToArray());
    }

    internal static void AssertSaveOutcome<TPage>(
        IRenderedComponent<TPage> page, NavigationManager navigation, string originalUri, string? error)
        where TPage : class, IComponent
    {
        if (error is null)
        {
            Assert.Equal("http://localhost/management/design", navigation.Uri);
            Assert.Empty(page.FindComponents<MudAlert>());
        }
        else
        {
            Assert.Equal(originalUri, navigation.Uri);
            var alert = page.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains(error, alert.Markup);
        }
    }
}
