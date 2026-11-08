using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TattooFlash.Web.Components.Pages.Management.Tags;
using TattooFlash.Web.Components.Tags;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Pages.Management.Tags;

public class EditTagTests
{
    [Fact]
    public async Task WhenLoadingThenProgressIsDisplayedBeforeALocalEditCopy()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var stored = new TagModel { Id = Guid.NewGuid(), DispalyName = "Old School", NormalizedName = "OLD SCHOOL" };
        var pending = new TaskCompletionSource<TagModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<ITagService>(MockBehavior.Strict);
        service.Setup(x => x.GetTagByIdAsync(stored.Id)).Returns(pending.Task);
        context.Services.AddSingleton(service.Object);
        var page = context.RenderWithProviders<EditTag>(p => p.Add(x => x.TagId, stored.Id));
        Assert.Single(page.FindComponents<MudProgressCircular>());
        Assert.Empty(page.FindComponents<TagInput>());

        // Act
        pending.SetResult(stored);

        // Assert
        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindComponents<MudProgressCircular>());
            Assert.Equal("Edit Tag", page.Find("h3").TextContent);
            var editable = page.FindComponent<TagInput>().Instance.Tag;
            Assert.NotSame(stored, editable);
            Assert.Equal(stored.Id, editable.Id);
            Assert.Equal("Old School", editable.DispalyName);
            Assert.Equal("Update", page.FindComponent<MudButton>().Find("button").TextContent.Trim());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenLoadingFailsOrTagIsMissingThenErrorReplacesForm(bool throws)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var id = Guid.NewGuid();
        var service = new Mock<ITagService>(MockBehavior.Strict);
        if (throws)
            service.Setup(x => x.GetTagByIdAsync(id)).ThrowsAsync(new InvalidOperationException("Load failed"));
        else
            service.Setup(x => x.GetTagByIdAsync(id)).ReturnsAsync((TagModel?)null);
        context.Services.AddSingleton(service.Object);

        // Act
        var page = context.RenderWithProviders<EditTag>(p => p.Add(x => x.TagId, id));

        // Assert
        Assert.Empty(page.FindComponents<TagInput>());
        Assert.Empty(page.FindComponents<MudProgressCircular>());
        Assert.Contains(throws ? "An error occurred while loading the tag. Please try again later."
            : "Tag not found.", page.FindComponent<MudAlert>().Markup);
        service.Verify(x => x.GetTagByIdAsync(id), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("success")]
    [InlineData("duplicate")]
    [InlineData("exception")]
    [InlineData("deleted")]
    public async Task WhenSubmittedThenRouteIdAndEnteredNameAreSavedWithTheExpectedOutcome(string outcome)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var stored = new TagModel { Id = Guid.NewGuid(), DispalyName = "Old School" };
        var service = new Mock<ITagService>(MockBehavior.Strict);
        service.Setup(x => x.GetTagByIdAsync(stored.Id)).ReturnsAsync(stored);
        if (outcome == "exception")
            service.Setup(x => x.UpdateTagAsync(stored.Id, "Traditional")).ThrowsAsync(new InvalidOperationException("Save failed"));
        else if (outcome == "deleted")
            service.Setup(x => x.UpdateTagAsync(stored.Id, "Traditional")).ThrowsAsync(new KeyNotFoundException("Tag not found"));
        else
            service.Setup(x => x.UpdateTagAsync(stored.Id, "Traditional")).ReturnsAsync(outcome == "success"
                ? TagSaveResult.Success(new TagModel { Id = stored.Id, DispalyName = "Traditional" })
                : TagSaveResult.Failure("Duplicate tag name"));
        context.Services.AddSingleton(service.Object);
        var page = context.RenderWithProviders<EditTag>(p => p.Add(x => x.TagId, stored.Id));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;

        // Act
        page.Find("input").Change("Traditional");
        await page.FindComponent<MudButton>().Find("button").ClickAsync(new());

        // Assert
        Assert.Equal("Old School", stored.DispalyName);
        service.Verify(x => x.UpdateTagAsync(stored.Id, "Traditional"), Times.Once);
        if (outcome == "success")
        {
            Assert.Equal("http://localhost/management/tag", navigation.Uri);
            Assert.Empty(page.FindComponents<MudAlert>());
        }
        else
        {
            Assert.Equal(originalUri, navigation.Uri);
            var error = outcome switch
            {
                "duplicate" => "Duplicate tag name",
                "deleted" => "Tag not found. It may have been deleted.",
                _ => "An error occurred while saving the tag. Please try again."
            };
            Assert.Contains(error, page.FindComponent<MudAlert>().Markup);
            Assert.Equal("Traditional", page.Find("input").GetAttribute("value"));
            Assert.False(page.FindComponent<MudButton>().Find("button").HasAttribute("disabled"));
        }
    }

    [Fact]
    public async Task WhenRouteParameterChangesThenPreviousErrorsAndInputAreReplaced()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var missingId = Guid.NewGuid();
        var stored = new TagModel { Id = Guid.NewGuid(), DispalyName = "Flowers" };
        var service = new Mock<ITagService>(MockBehavior.Strict);
        service.Setup(x => x.GetTagByIdAsync(missingId)).ReturnsAsync((TagModel?)null);
        service.Setup(x => x.GetTagByIdAsync(stored.Id)).ReturnsAsync(stored);
        context.Services.AddSingleton(service.Object);
        var page = context.RenderWithProviders<EditTag>(p => p.Add(x => x.TagId, missingId));
        Assert.Single(page.FindComponents<MudAlert>());

        // Act
        page.Render(p => p.Add(x => x.TagId, stored.Id));

        // Assert
        Assert.Empty(page.FindComponents<MudAlert>());
        Assert.Equal("Flowers", page.Find("input").GetAttribute("value"));
        Assert.Equal(stored.Id, page.FindComponent<TagInput>().Instance.Tag.Id);
    }

    [Fact]
    public async Task WhenRenameIsRetriedThenThePreviousSaveErrorClears()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var stored = new TagModel { Id = Guid.NewGuid(), DispalyName = "Flowers" };
        var service = new Mock<ITagService>(MockBehavior.Strict);
        service.Setup(x => x.GetTagByIdAsync(stored.Id)).ReturnsAsync(stored);
        service.SetupSequence(x => x.UpdateTagAsync(stored.Id, "Floral"))
            .ReturnsAsync(TagSaveResult.Failure("Duplicate tag name"))
            .ReturnsAsync(TagSaveResult.Success(stored));
        context.Services.AddSingleton(service.Object);
        var page = context.RenderWithProviders<EditTag>(p => p.Add(x => x.TagId, stored.Id));
        page.Find("input").Change("Floral");
        await page.FindComponent<MudButton>().Find("button").ClickAsync(new());
        Assert.Single(page.FindComponents<MudAlert>());

        // Act
        await page.FindComponent<MudButton>().Find("button").ClickAsync(new());

        // Assert
        Assert.Empty(page.FindComponents<MudAlert>());
        Assert.Equal("http://localhost/management/tag", context.Services.GetRequiredService<NavigationManager>().Uri);
    }
}