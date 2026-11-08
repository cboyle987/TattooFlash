using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Components.Dialogs;
using TattooFlash.Web.Components.Layout;
using TattooFlash.Web.Components.Pages.Management.Design;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

using static TattooFlash.UnitTest.Components.Pages.Management.ManagementPageTestSupport;

namespace TattooFlash.UnitTest.Components.Pages.Management;

public class ManagementNotificationTests
{
    [Theory]
    [InlineData("deleted", "Design deleted successfully.", Severity.Success)]
    [InlineData("missing", "This design was already deleted.", Severity.Info)]
    [InlineData("error", "Could not delete the design. Please try again.", Severity.Error)]
    public async Task WhenDeletionCompletesThenTheLayoutDisplaysTheNotification(string outcome, string message, Severity severity)
    {
        // Arrange: retain a second design so deletion does not force a full page reload.
        var design = CreateDesign();
        var remaining = CreateDesign("Rose");
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        var mockConfig = new Mock<IConfiguration>();
        service.Setup(x => x.GetPaginatedDesigns(1)).ReturnsAsync(new PaginatedDesignResults
        {
            Designs = [design, remaining],
            PageNumber = 1,
            PageSize = 12,
            TotalItems = 2
        });
        if (outcome == "error")
            service.Setup(x => x.DeleteDesignAsync(design.Id)).ThrowsAsync(new InvalidOperationException("Delete failed"));
        else
            service.Setup(x => x.DeleteDesignAsync(design.Id)).ReturnsAsync(outcome == "deleted");
        await using var context = CreateContext(service);
        context.Services.AddSingleton(mockConfig.Object);
        context.Authorization.SetAuthorized("test-user");
        RenderFragment body = builder =>
        {
            builder.OpenComponent<DesignManagement>(0);
            builder.CloseComponent();
        };
        // Render the real layout without the test helper's additional snackbar provider.
        var layout = context.Render<MainLayout>(p => p.Add(x => x.Body, body));
        var item = layout.FindComponents<DesignManagementItem>().Single(x => x.Instance.Design.Id == design.Id);

        // Act: exercise the real confirmation dialog as well as the page's deletion callback.
        var deletion = item.FindAll("button").Single(b => b.TextContent.Contains("Delete Design")).ClickAsync(new());
        layout.WaitForAssertion(() => Assert.Single(layout.FindComponents<DeleteConfirmationDialog>()));
        await layout.FindComponent<DeleteConfirmationDialog>().FindAll("button")
            .Single(b => b.TextContent.Trim() == "Delete").ClickAsync(new());
        await deletion;

        // Assert: a queued service message alone would not catch a missing layout provider.
        layout.WaitForAssertion(() =>
        {
            var provider = layout.FindComponent<MudSnackbarProvider>();
            Assert.Contains(message, provider.Find("[role='alert']").TextContent);
            var notification = Assert.Single(context.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
            Assert.Equal(message, notification.Message);
            Assert.Equal(severity, notification.Severity);
            Assert.Equal(outcome == "error" ? 2 : 1, layout.FindComponents<DesignManagementItem>().Count);
        });
        service.Verify(x => x.GetPaginatedDesigns(1), Times.Once);
        service.Verify(x => x.DeleteDesignAsync(design.Id), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, false, "Design availability changed to Unavailable.", Severity.Success)]
    [InlineData(false, false, "Design availability changed to Available.", Severity.Success)]
    [InlineData(true, true, "Could not change availability. Please try again.", Severity.Error)]
    [InlineData(false, true, "Could not change availability. Please try again.", Severity.Error)]
    public async Task WhenAvailabilityCompletesThenTheVisibleMessageMatchesTheDesignState(
        bool initiallyAvailable, bool fails, string message, Severity severity)
    {
        // Arrange: use the real page so its callback updates the shared design model.
        var design = CreateDesign(available: initiallyAvailable);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        var mockConfig = new Mock<IConfiguration>();
        service.Setup(x => x.GetPaginatedDesigns(1)).ReturnsAsync(new PaginatedDesignResults
        {
            Designs = [design],
            PageNumber = 1,
            PageSize = 12,
            TotalItems = 1
        });
        if (fails)
            service.Setup(x => x.ToggleAvailabilityAsync(design.Id)).ThrowsAsync(new InvalidOperationException("Toggle failed"));
        else
            service.Setup(x => x.ToggleAvailabilityAsync(design.Id)).Returns(Task.CompletedTask);
        await using var context = CreateContext(service);
        context.Services.AddSingleton(mockConfig.Object);
        context.Authorization.SetAuthorized("test-user");
        RenderFragment body = builder =>
        {
            builder.OpenComponent<DesignManagement>(0);
            builder.CloseComponent();
        };
        var layout = context.Render<MainLayout>(p => p.Add(x => x.Body, body));
        var item = layout.FindComponent<DesignManagementItem>();

        // Act
        await item.FindAll("button").Single(b => b.TextContent.Contains("Toggle Availability")).ClickAsync(new());

        // Assert
        layout.WaitForAssertion(() =>
        {
            var expectedAvailability = fails ? initiallyAvailable : !initiallyAvailable;
            Assert.Equal(expectedAvailability, design.IsAvailable);
            Assert.Single(item.FindAll($"[aria-label='{(expectedAvailability ? "Available" : "Not available")}']"));
            var provider = layout.FindComponent<MudSnackbarProvider>();
            Assert.Contains(message, provider.Find("[role='alert']").TextContent);
            var notification = Assert.Single(context.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
            Assert.Equal(message, notification.Message);
            Assert.Equal(severity, notification.Severity);
        });
        service.Verify(x => x.GetPaginatedDesigns(1), Times.Once);
        service.Verify(x => x.ToggleAvailabilityAsync(design.Id), Times.Once);
        service.VerifyNoOtherCalls();
    }
}