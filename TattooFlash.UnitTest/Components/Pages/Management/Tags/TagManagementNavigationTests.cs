using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TattooFlash.Web.Components.Pages.Management.Tags;
using TattooFlash.Web.Components.Placeholders;
using TattooFlash.Web.Components.Tags;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Pages.Management.Tags;

public class TagManagementNavigationTests
{
    [Fact]
    public async Task WhenCatalogueLoadsThenAddAndEditActionsUseTheTagRoutes()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var tag = new TagModel { Id = Guid.NewGuid(), DispalyName = "Flowers" };
        var pending = new TaskCompletionSource<List<TagModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<ITagService>(MockBehavior.Strict);
        service.Setup(x => x.GetAllTagsAsync()).Returns(pending.Task);
        context.Services.AddSingleton(service.Object);
        var page = context.RenderWithProviders<TagManagement>();
        Assert.Equal(4, page.FindComponents<SkeletonManagementItem>().Count);

        // Act
        pending.SetResult([tag]);
        page.WaitForAssertion(() => Assert.Single(page.FindComponents<TagManagementItem>()));

        // Assert
        Assert.Equal("/management/tag/add", page.Find("a").GetAttribute("href"));
        await page.FindAll("button").Single(b => b.TextContent.Contains("Edit Tag")).ClickAsync(new());
        Assert.Equal($"http://localhost/management/tag/edit/{tag.Id}",
            context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Theory]
    [InlineData(typeof(AddTag))]
    [InlineData(typeof(EditTag))]
    public async Task WhenSignedOutThenAuthorizationPreventsOpeningTheTagForm(Type pageType)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        context.Authorization.SetNotAuthorized();
        var service = new Mock<ITagService>(MockBehavior.Strict);
        context.Services.AddSingleton(service.Object);
        var route = new RouteData(pageType, new Dictionary<string, object?> { ["TagId"] = Guid.NewGuid() });

        // Act: use the authorization-aware route view, rather than rendering the page directly.
        var view = context.RenderWithProviders<AuthorizeRouteView>(p => p.Add(x => x.RouteData, route));

        // Assert
        Assert.Empty(view.FindComponents<TagInput>());
        service.VerifyNoOtherCalls();
    }
}