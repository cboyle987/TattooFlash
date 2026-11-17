using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TattooFlash.Web.Components.Pages.Management.Design;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components;

public class ComponentTestContextTests
{
    [Fact]
    public async Task RenderWithProviders_RendersAddDesignWithMudBlazorAndInjectedService()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        context.Services.AddSingleton(service.Object);
        var tags = new Mock<ITagService>(MockBehavior.Strict);
        tags.Setup(x => x.GetAllTagsAsync()).ReturnsAsync(new List<TagModel>());
        context.Services.AddSingleton(tags.Object);
        context.Authorization.SetAuthorized("test-user");

        // Act
        var page = context.RenderWithProviders<AddDesign>();

        // Assert
        Assert.Equal("Add New Design", page.Find("h3").TextContent);
        Assert.NotEmpty(page.FindAll("input"));
        Assert.Same(service.Object, page.Instance.DesignService);
        service.VerifyNoOtherCalls();
    }
}
