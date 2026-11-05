using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Components.Dialogs;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Designs;

public class DesignManagementItemTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenRenderedThenDesignDetailsAndAvailabilityAreDisplayed(bool available)
    {
        // Arrange
        var design = CreateDesign(available);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        await using var context = CreateContext(design, service);

        // Act
        var item = context.RenderWithProviders<DesignManagementItem>(p => p.Add(x => x.Design, design));

        // Assert
        Assert.Equal("Swallow", item.Find("h5").TextContent);
        Assert.Equal("https://images.example/swallow.webp", item.Find("img").GetAttribute("src"));
        Assert.Equal("Swallow", item.Find("img").GetAttribute("alt"));
        var label = available ? "Available" : "Not available";
        Assert.Equal(label, item.FindComponent<MudTooltip>().Instance.Text);
        Assert.Single(item.FindAll($"[aria-label='{label}']"));
        Assert.Collection(item.FindComponents<MudButton>(),
            b => Assert.Contains("Edit Design", b.Markup),
            b => Assert.Contains("Delete Design", b.Markup),
            b => Assert.Contains("Toggle Availability", b.Markup));
        AssertActionsEnabled(item);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WhenEditIsClickedThenNavigationUsesTheDesignId()
    {
        // Arrange
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        await using var context = CreateContext(design, service);
        var item = context.RenderWithProviders<DesignManagementItem>(p => p.Add(x => x.Design, design));

        // Act
        await ClickAction(item, "Edit Design");

        // Assert
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        Assert.Equal($"http://localhost/management/edit-design/{design.Id}", navigation.Uri);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenDeletionIsConfirmedThenServiceAndParentReceiveTheId(bool exists)
    {
        // Arrange
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        service.Setup(x => x.DeleteDesignAsync(design.Id)).ReturnsAsync(exists);
        var dialogs = CreateDialogService(Task.FromResult<DialogResult?>(DialogResult.Ok(true)));
        await using var context = CreateContext(design, service, dialogs);
        var deleted = new List<Guid>();
        var item = context.RenderWithProviders<DesignManagementItem>(p => p
            .Add(x => x.Design, design).Add(x => x.OnDesignDeleted, (Guid id) => deleted.Add(id)));

        // Act
        await ClickAction(item, "Delete Design");

        // Assert
        dialogs.Verify(x => x.ShowAsync<DeleteConfirmationDialog>("Confirm Delete",
            It.Is<DialogParameters>(p => ReferenceEquals(p.Get<DesignModel>("Design"), design))), Times.Once);
        service.Verify(x => x.DeleteDesignAsync(design.Id), Times.Once);
        service.VerifyNoOtherCalls();
        Assert.Equal(new[] { design.Id }, deleted);
        if (exists)
            AssertNotification(context, "Design deleted successfully.", Severity.Success);
        else
        {
            AssertNotification(context, "This design was already deleted.", Severity.Info);
        }
        AssertActionsEnabled(item);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenDeletionIsCanceledOrDialogResultIsNullThenNothingIsDeleted(bool nullResult)
    {
        // Arrange
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        var design = CreateDesign();
        var dialogs = CreateDialogService(Task.FromResult<DialogResult?>(nullResult ? null : DialogResult.Cancel()));
        await using var context = CreateContext(design, service, dialogs);
        var deleted = new List<Guid>();
        var item = context.RenderWithProviders<DesignManagementItem>(p => p
            .Add(x => x.Design, design).Add(x => x.OnDesignDeleted, (Guid id) => deleted.Add(id)));

        // Act
        await ClickAction(item, "Delete Design");

        // Assert
        service.VerifyNoOtherCalls();
        Assert.Empty(deleted);
        Assert.Empty(context.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        AssertActionsEnabled(item);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenDeletionFailsThenErrorIsDisplayedAndParentIsNotNotified(bool dialogFails)
    {
        // Arrange
        var design = CreateDesign();
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        var dialogs = CreateDialogService(Task.FromResult<DialogResult?>(DialogResult.Ok(true)));
        if (dialogFails)
            dialogs.Setup(x => x.ShowAsync<DeleteConfirmationDialog>("Confirm Delete", It.IsAny<DialogParameters>()))
                .ThrowsAsync(new InvalidOperationException("Dialog failed"));
        else
        {
            service.Setup(x => x.DeleteDesignAsync(design.Id)).ThrowsAsync(new InvalidOperationException("Delete failed"));
        }
        await using var context = CreateContext(design, service, dialogs);
        var deleted = new List<Guid>();
        var item = context.RenderWithProviders<DesignManagementItem>(p => p
            .Add(x => x.Design, design).Add(x => x.OnDesignDeleted, (Guid id) => deleted.Add(id)));

        // Act
        await ClickAction(item, "Delete Design");

        // Assert
        Assert.Empty(deleted);
        service.Verify(x => x.DeleteDesignAsync(design.Id), dialogFails ? Times.Never() : Times.Once());
        AssertNotification(context, "Could not delete the design. Please try again.", Severity.Error);
        AssertActionsEnabled(item);
    }

    [Theory]
    [InlineData(false, true, "Design availability changed to Unavailable.")]
    [InlineData(false, false, "Design availability changed to Available.")]
    [InlineData(true, true, "Could not change availability. Please try again.")]
    [InlineData(true, false, "Could not change availability. Please try again.")]
    public async Task WhenAvailabilityIsToggledThenParentIsNotifiedOnlyOnSuccess(bool fails, bool available, string message)
    {
        // Arrange
        var design = CreateDesign(available);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        if (fails)
            service.Setup(x => x.ToggleAvailabilityAsync(design.Id)).ThrowsAsync(new InvalidOperationException("Toggle failed"));
        else
            service.Setup(x => x.ToggleAvailabilityAsync(design.Id)).Returns(Task.CompletedTask);
        await using var context = CreateContext(design, service);
        var changed = new List<Guid>();
        var item = context.RenderWithProviders<DesignManagementItem>(p => p
            .Add(x => x.Design, design).Add(x => x.OnAvailabilityChanged, (Guid id) =>
            {
                changed.Add(id);
                // Match DesignManagement: the parent updates the model before the callback returns.
                design.IsAvailable = !design.IsAvailable;
            }));

        // Act
        await ClickAction(item, "Toggle Availability");

        // Assert
        service.Verify(x => x.ToggleAvailabilityAsync(design.Id), Times.Once);
        service.VerifyNoOtherCalls();
        if (fails)
        {
            Assert.Empty(changed);
            AssertNotification(context, message, Severity.Error);
        }
        else
        {
            Assert.Equal(new[] { design.Id }, changed);
            AssertNotification(context, message, Severity.Success);
        }
        Assert.Equal(fails ? available : !available, design.IsAvailable);
        AssertActionsEnabled(item);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhileAnOperationIsPendingThenActionsAreDisabledAndDuplicateCallbacksAreIgnored(bool deleting)
    {
        // Arrange
        var design = CreateDesign();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmation = new TaskCompletionSource<DialogResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = deleting ? CreateDialogService(confirmation.Task) : new Mock<IDialogService>(MockBehavior.Strict);
        var service = new Mock<IDesignService>(MockBehavior.Strict);
        if (!deleting)
            service.Setup(x => x.ToggleAvailabilityAsync(design.Id)).Returns(pending.Task);
        await using var context = CreateContext(design, service, dialogs);
        var deleted = new List<Guid>();
        var changed = new List<Guid>();
        var item = context.RenderWithProviders<DesignManagementItem>(p => p
            .Add(x => x.Design, design)
            .Add(x => x.OnDesignDeleted, (Guid id) => deleted.Add(id))
            .Add(x => x.OnAvailabilityChanged, (Guid id) =>
            {
                changed.Add(id);
                design.IsAvailable = !design.IsAvailable;
            }));
        var deleteButton = item.FindComponents<MudButton>().Single(b => b.Markup.Contains("Delete Design"));
        var toggleButton = item.FindComponents<MudButton>().Single(b => b.Markup.Contains("Toggle Availability"));

        // Act
        var operation = ClickAction(item, deleting ? "Delete Design" : "Toggle Availability");
        item.WaitForAssertion(() => Assert.All(item.FindAll("button"), b => Assert.True(b.HasAttribute("disabled"))));

        // Invoke the public callbacks directly to test the parent's busy guards,
        // independently of MudButton's own suppression of disabled clicks.
        await item.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await item.InvokeAsync(() => toggleButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        Assert.Empty(deleted);
        Assert.Empty(changed);
        if (deleting)
        {
            dialogs.Verify(x => x.ShowAsync<DeleteConfirmationDialog>("Confirm Delete", It.IsAny<DialogParameters>()), Times.Once);
            service.VerifyNoOtherCalls();
            confirmation.SetResult(DialogResult.Cancel());
        }
        else
        {
            service.Verify(x => x.ToggleAvailabilityAsync(design.Id), Times.Once);
            service.VerifyNoOtherCalls();
            dialogs.Verify(x => x.ShowAsync<DeleteConfirmationDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>()), Times.Never);
            pending.SetResult(true);
        }
        await operation;

        // Assert
        item.WaitForAssertion(() => AssertActionsEnabled(item));
        Assert.Empty(deleted);
        Assert.Equal(deleting ? Array.Empty<Guid>() : new[] { design.Id }, changed);
        if (deleting)
            Assert.Empty(context.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        else
            AssertNotification(context, "Design availability changed to Unavailable.", Severity.Success);
    }

    private static DesignModel CreateDesign(bool available = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Swallow",
        ImageName = "swallow.webp",
        IsAvailable = available
    };

    // Each test owns and asynchronously disposes its context. Service behaviours remain in the test.
    private static ComponentTestContext CreateContext(
        DesignModel design, Mock<IDesignService> service, Mock<IDialogService>? dialogs = null)
    {
        var context = new ComponentTestContext();
        var images = new Mock<IImageService>(MockBehavior.Strict);
        images.Setup(x => x.GetImageUrl(design.ImageName)).Returns("https://images.example/swallow.webp");
        context.Services.AddSingleton(service.Object);
        context.Services.AddSingleton(images.Object);
        if (dialogs is not null)
            context.Services.AddSingleton(dialogs.Object);
        return context;
    }

    private static Mock<IDialogService> CreateDialogService(Task<DialogResult?> result)
    {
        var reference = new Mock<IDialogReference>();
        reference.SetupGet(x => x.Result).Returns(result);
        var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
        dialogs.Setup(x => x.ShowAsync<DeleteConfirmationDialog>("Confirm Delete", It.IsAny<DialogParameters>()))
            .ReturnsAsync(reference.Object);
        return dialogs;
    }

    private static Task ClickAction(IRenderedComponent<DesignManagementItem> item, string text) =>
        item.FindAll("button").Single(b => b.TextContent.Contains(text)).ClickAsync(new());

    private static void AssertActionsEnabled(IRenderedComponent<DesignManagementItem> item) =>
        Assert.All(item.FindAll("button"), b => Assert.False(b.HasAttribute("disabled")));

    private static void AssertNotification(ComponentTestContext context, string message, Severity severity)
    {
        var notification = Assert.Single(context.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal(message, notification.Message);
        Assert.Equal(severity, notification.Severity);
    }
}