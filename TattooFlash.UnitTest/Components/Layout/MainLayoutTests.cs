using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;
using TattooFlash.Web.Components.Layout;

namespace TattooFlash.UnitTest.Components.Layout;

public class MainLayoutTests
{
    [Fact]
    public async Task WhenUnauthenticatedThenLoginAndBodyAreDisplayedWithoutManagementNavigation()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        context.Authorization.SetNotAuthorized();
        RenderFragment body = builder => builder.AddMarkupContent(0, "<p id='page-content'>Page content</p>");

        // Act: the layout supplies its own theme and dialog providers.
        var layout = context.Render<MainLayout>(p => p.Add(x => x.Body, body));

        // Assert
        Assert.Contains("Tattoo Flash", layout.Markup);
        Assert.Equal("Page content", layout.Find("#page-content").TextContent);
        Assert.Equal("Login", layout.Find("a[href='/account/login']").TextContent.Trim());
        Assert.Empty(layout.FindAll("a[href='/account/logout']"));
        Assert.Empty(layout.FindComponents<NavMenu>());
        Assert.Empty(layout.FindComponents<MudDrawer>());
        Assert.Single(layout.FindComponents<MudDialogProvider>());
        var theme = layout.FindComponent<MudThemeProvider>();
        Assert.NotNull(theme.Instance.Theme);
        Assert.False(theme.Instance.GetState(x => x.IsDarkMode));
        Assert.Equal(Icons.Material.Outlined.DarkMode, layout.Instance.DarkLightModeButtonIcon);
        Assert.Contains("An unhandled error has occurred.", layout.Find("#blazor-error-ui").TextContent);
        Assert.Equal(".", layout.Find("#blazor-error-ui a").GetAttribute("href"));
    }

    [Fact]
    public async Task WhenAuthenticatedThenNavigationAndLogoutAreDisplayedAndMenuTogglesDrawer()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        context.Authorization.SetAuthorized("test-user");
        var layout = context.Render<MainLayout>();
        Assert.Equal("Logout", layout.Find("a[href='/account/logout']").TextContent.Trim());
        Assert.Empty(layout.FindAll("a[href='/account/login']"));
        Assert.Single(layout.FindComponents<NavMenu>());
        Assert.Equal("management", layout.Find("a[href='management']").GetAttribute("href"));
        var drawer = layout.FindComponent<MudDrawer>();
        Assert.False(drawer.Instance.GetState(x => x.Open));
        var menu = layout.FindComponent<MudIconButton>();

        // Act
        await menu.Find("button").ClickAsync(new());

        // Assert
        layout.WaitForAssertion(() => Assert.True(drawer.Instance.GetState(x => x.Open)));
        await menu.Find("button").ClickAsync(new());
        layout.WaitForAssertion(() => Assert.False(drawer.Instance.GetState(x => x.Open)));
    }
}
