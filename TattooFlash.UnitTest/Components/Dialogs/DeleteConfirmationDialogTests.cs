using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TattooFlash.Web.Components.Dialogs;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Dialogs;

public class DeleteConfirmationDialogTests
{
    [Theory]
    [InlineData("Swallow")]
    [InlineData("Rose & <script>alert('design')</script>")]
    public async Task WhenOpenedThenTheNamedDesignAndIrreversibleActionWarningAreDisplayed(string name)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = CreateDesign(name);
        // Act
        var (provider, reference) = await OpenDialogAsync(context, design);

        // Assert
        provider.WaitForAssertion(() =>
        {
            Assert.Same(design, provider.FindComponent<DeleteConfirmationDialog>().Instance.Design);
            Assert.Contains($"Are you sure you want to delete {name}? This action cannot be undone.",
                provider.Find("[role='dialog']").TextContent);
            Assert.Empty(provider.FindAll("script"));
            Assert.Collection(provider.FindAll("button"),
                button => Assert.Equal("Cancel", button.TextContent.Trim()),
                button => Assert.Equal("Delete", button.TextContent.Trim()));
        });
        Assert.False(reference.Result.IsCompleted);
    }

    [Fact]
    public async Task WhenDeleteIsClickedThenDialogClosesWithSuccessfulConfirmation()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = CreateDesign();
        var (provider, reference) = await OpenDialogAsync(context, design);

        // Act
        await provider.FindAll("button").Single(button => button.TextContent.Trim() == "Delete").ClickAsync(new());

        // Assert
        var result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.True(Assert.IsType<bool>(result.Data));
        provider.WaitForAssertion(() => Assert.Empty(provider.FindComponents<DeleteConfirmationDialog>()));
    }

    [Fact]
    public async Task WhenCancelIsClickedThenDialogClosesWithoutConfirmation()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = CreateDesign();
        var (provider, reference) = await OpenDialogAsync(context, design);

        // Act
        await provider.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").ClickAsync(new());

        // Assert
        var result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.True(result.Canceled);
        Assert.Null(result.Data);
        provider.WaitForAssertion(() => Assert.Empty(provider.FindComponents<DeleteConfirmationDialog>()));
    }

    private static DesignModel CreateDesign(string name = "Swallow") => new()
    {
        Id = Guid.NewGuid(),
        Name = name
    };

    private static async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Reference)> OpenDialogAsync(
        ComponentTestContext context, DesignModel design)
    {
        context.Render<MudPopoverProvider>();
        var provider = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<DeleteConfirmationDialog> { { x => x.Design, design } };
        IDialogReference? reference = null;
        await provider.InvokeAsync(async () => reference = await dialogs.ShowAsync<DeleteConfirmationDialog>("Confirm Delete", parameters));
        Assert.NotNull(reference);
        // The provider owns the visible content; the reference exposes the outcome to the caller.
        return (provider, reference);
    }
}
