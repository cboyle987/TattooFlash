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

public class AddTagTests
{
    [Fact]
    public async Task WhenOpenedThenAnEmptyCreateFormIsDisplayed()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var service = new Mock<ITagService>(MockBehavior.Strict);
        context.Services.AddSingleton(service.Object);

        // Act
        var page = context.RenderWithProviders<AddTag>();

        // Assert
        Assert.Equal("Add New Tag", page.Find("h3").TextContent);
        Assert.Equal(string.Empty, page.FindComponent<TagInput>().Instance.Tag.DispalyName);
        Assert.Equal(Guid.Empty, page.FindComponent<TagInput>().Instance.Tag.Id);
        Assert.Equal("Create", page.FindComponent<MudButton>().Find("button").TextContent.Trim());
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("success")]
    [InlineData("duplicate")]
    [InlineData("exception")]
    public async Task WhenSubmittedThenSuccessNavigatesAndFailurePreservesInput(string outcome)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var service = new Mock<ITagService>(MockBehavior.Strict);
        if (outcome == "exception")
            service.Setup(x => x.CreateTagAsync("Old School")).ThrowsAsync(new InvalidOperationException("Save failed"));
        else
            service.Setup(x => x.CreateTagAsync("Old School")).ReturnsAsync(outcome == "success"
                ? TagSaveResult.Success(new TagModel { Id = Guid.NewGuid(), DispalyName = "Old School" })
                : TagSaveResult.Failure("Duplicate tag name"));
        context.Services.AddSingleton(service.Object);
        var page = context.RenderWithProviders<AddTag>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;

        // Act
        page.Find("input").Change("Old School");
        await page.FindComponent<MudButton>().Find("button").ClickAsync(new());

        // Assert
        service.Verify(x => x.CreateTagAsync("Old School"), Times.Once);
        service.VerifyNoOtherCalls();
        if (outcome == "success")
        {
            Assert.Equal("http://localhost/management/tag", navigation.Uri);
            Assert.Empty(page.FindComponents<MudAlert>());
        }
        else
        {
            Assert.Equal(originalUri, navigation.Uri);
            Assert.Equal("Old School", page.Find("input").GetAttribute("value"));
            Assert.Contains(outcome == "duplicate" ? "Duplicate tag name"
                : "An error occurred while saving the tag. Please try again.", page.FindComponent<MudAlert>().Markup);
            Assert.False(page.FindComponent<MudButton>().Find("button").HasAttribute("disabled"));
        }
    }

    [Fact]
    public async Task WhenRetriedAfterFailureThenOldErrorIsClearedAndSaveSucceeds()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var service = new Mock<ITagService>(MockBehavior.Strict);
        service.SetupSequence(x => x.CreateTagAsync("Flowers"))
            .ReturnsAsync(TagSaveResult.Failure("Duplicate tag name"))
            .ReturnsAsync(TagSaveResult.Success(new TagModel { Id = Guid.NewGuid(), DispalyName = "Flowers" }));
        context.Services.AddSingleton(service.Object);
        var page = context.RenderWithProviders<AddTag>();
        page.Find("input").Change("Flowers");
        await page.FindComponent<MudButton>().Find("button").ClickAsync(new());
        Assert.Single(page.FindComponents<MudAlert>());

        // Act
        await page.FindComponent<MudButton>().Find("button").ClickAsync(new());

        // Assert
        Assert.Empty(page.FindComponents<MudAlert>());
        Assert.Equal("http://localhost/management/tag", context.Services.GetRequiredService<NavigationManager>().Uri);
        service.Verify(x => x.CreateTagAsync("Flowers"), Times.Exactly(2));
    }
}