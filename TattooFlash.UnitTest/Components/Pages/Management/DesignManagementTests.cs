using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Extensions;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Components.Pages.Management;
using TattooFlash.Web.Components.Placeholders;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

using static TattooFlash.UnitTest.Components.Pages.Management.ManagementPageTestSupport;

namespace TattooFlash.UnitTest.Components.Pages.Management;

public class DesignManagementTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task WhenLoadedThenItemsAndPaginationReflectTheResults(int totalItems)
    {
        // Arrange
        var designs = Enumerable.Range(0, Math.Min(totalItems, 2)).Select(i => new DesignModel
        {
            Id = Guid.NewGuid(),
            Name = $"Design {i}",
            ImageName = $"design-{i}.webp"
        }).ToList();
        var pending = new TaskCompletionSource<PaginatedDesignResults>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetPaginatedDesigns(1)).Returns(pending.Task);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<DesignManagement>();
        AssertLoading(page);

        // Act
        pending.SetResult(new PaginatedDesignResults { Designs = designs, PageNumber = 1, PageSize = 2, TotalItems = totalItems });

        // Assert
        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindComponents<SkeletonDesignManagementItem>());
            Assert.Empty(page.FindComponents<MudAlert>());
            Assert.Equal(designs.Select(x => x.Id), page.FindComponents<DesignManagementItem>().Select(x => x.Instance.Design.Id));
            Assert.Equal("/management/add-design", page.Find("a").GetAttribute("href"));
            if (totalItems > 2)
            {
                var pagination = page.FindComponent<MudPagination>();
                Assert.Equal(2, pagination.Instance.GetState(x => x.Count));
                Assert.Equal(1, pagination.Instance.GetState(x => x.Selected));
            }
            else
                Assert.Empty(page.FindComponents<MudPagination>());
        });
        service.Verify(x => x.GetPaginatedDesigns(1), Times.Once);
    }

    [Fact]
    public async Task WhenPageIsSelectedThenNewResultsReplacePreviousItems()
    {
        // Arrange
        var first = CreateDesign();
        var second = CreateDesign("Rose");
        var pending = new TaskCompletionSource<PaginatedDesignResults>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetPaginatedDesigns(1)).ReturnsAsync(new PaginatedDesignResults
        {
            Designs = [first],
            TotalItems = 2,
            PageSize = 1,
            PageNumber = 1
        });
        service.Setup(x => x.GetPaginatedDesigns(2)).Returns(pending.Task);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<DesignManagement>();
        Assert.Same(first, page.FindComponent<DesignManagementItem>().Instance.Design);
        var pagination = page.FindComponent<MudPagination>();

        // Act
        var selection = page.InvokeAsync(() => pagination.Instance.SelectedChanged.InvokeAsync(2));
        page.WaitForAssertion(() => AssertLoading(page));
        pending.SetResult(new PaginatedDesignResults { Designs = [second], TotalItems = 2, PageSize = 1, PageNumber = 2 });
        await selection;

        // Assert
        page.WaitForAssertion(() =>
        {
            Assert.Same(second, page.FindComponent<DesignManagementItem>().Instance.Design);
            Assert.Equal(2, page.FindComponent<MudPagination>().Instance.GetState(x => x.Selected));
            Assert.Empty(page.FindComponents<SkeletonDesignManagementItem>());
        });
        service.Verify(x => x.GetPaginatedDesigns(2), Times.Once);
    }

    [Fact]
    public async Task WhenLoadingFailsThenErrorIsDisplayedAndSuccessfulReloadClearsIt()
    {
        // Arrange
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.SetupSequence(x => x.GetPaginatedDesigns(1))
            .ThrowsAsync(new InvalidOperationException("Load failed"))
            .ReturnsAsync(new PaginatedDesignResults { PageNumber = 1, PageSize = 2 });
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<DesignManagement>();
        Assert.Contains("There has been an error loading designs please try again later", page.FindComponent<MudAlert>().Markup);
        Assert.Empty(page.FindComponents<SkeletonDesignManagementItem>());
        Assert.Empty(page.FindComponents<DesignManagementItem>());
        Assert.Empty(page.FindComponents<MudPagination>());

        // Act: the page exposes LoadDesigns publicly, but has no retry control.
        await page.InvokeAsync(() => page.Instance.LoadDesigns(1));
        // A direct public-method call does not schedule the render that a UI event would.
        page.Render();

        // Assert
        Assert.Empty(page.FindComponents<MudAlert>());
        Assert.Equal("/management/add-design", page.Find("a").GetAttribute("href"));
        service.Verify(x => x.GetPaginatedDesigns(1), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenChildReportsDeletionThenItemAndCountAreUpdatedAndEmptyPageReloads(bool lastItem)
    {
        // Arrange
        var deleted = CreateDesign();
        var remaining = CreateDesign("Rose");
        var results = new PaginatedDesignResults
        {
            Designs = lastItem ? [deleted] : [deleted, remaining],
            TotalItems = lastItem ? 1 : 2,
            PageSize = 12,
            PageNumber = 1
        };
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetPaginatedDesigns(1)).ReturnsAsync(results);
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<DesignManagement>();
        var child = page.FindComponents<DesignManagementItem>().Single(x => x.Instance.Design.Id == deleted.Id);
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();

        // Act
        await page.InvokeAsync(() => child.Instance.OnDesignDeleted.InvokeAsync(deleted.Id));

        // Assert
        Assert.Equal(lastItem ? 0 : 1, page.Instance.Results.TotalItems);
        Assert.DoesNotContain(page.FindComponents<DesignManagementItem>(), x => x.Instance.Design.Id == deleted.Id);
        if (lastItem)
        {
            Assert.Empty(page.FindComponents<DesignManagementItem>());
            var reload = Assert.Single(navigation.History);
            Assert.Equal(navigation.Uri, reload.Uri);
            Assert.True(reload.Options.ForceLoad);
        }
        else
        {
            Assert.Same(remaining, page.FindComponent<DesignManagementItem>().Instance.Design);
            Assert.Empty(navigation.History);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task WhenChildReportsAvailabilityChangeThenOnlyMatchingDesignIsToggled(bool available, bool found)
    {
        // Arrange
        var design = CreateDesign(available: available);
        var other = CreateDesign("Rose");
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.GetPaginatedDesigns(1)).ReturnsAsync(new PaginatedDesignResults
        {
            Designs = [design, other],
            PageNumber = 1,
            PageSize = 12,
            TotalItems = 2
        });
        await using var context = CreateContext(service);
        var page = context.RenderWithProviders<DesignManagement>();
        var child = page.FindComponents<DesignManagementItem>().First();

        // Act
        await page.InvokeAsync(() => child.Instance.OnAvailabilityChanged.InvokeAsync(found ? design.Id : Guid.NewGuid()));

        // Assert
        Assert.Equal(found ? !available : available, design.IsAvailable);
        Assert.True(other.IsAvailable);
        Assert.Equal(2, page.Instance.Results.TotalItems);
        service.Verify(x => x.GetPaginatedDesigns(1), Times.Once);
        service.VerifyNoOtherCalls();
    }

    private static void AssertLoading(IRenderedComponent<DesignManagement> page)
    {
        Assert.Equal(4, page.FindComponents<SkeletonDesignManagementItem>().Count);
        Assert.Empty(page.FindComponents<DesignManagementItem>());
    }
}