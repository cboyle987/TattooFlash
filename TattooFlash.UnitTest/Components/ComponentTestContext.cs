using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;

namespace TattooFlash.UnitTest.Components;

/// <summary>Creates an isolated Blazor test environment with MudBlazor and test authentication.</summary>
public sealed class ComponentTestContext : BunitContext
{
    private bool _providersRendered;

    public BunitAuthorizationContext Authorization { get; }

    public ComponentTestContext()
    {
        Services.AddMudServices();
        Authorization = AddAuthorization();

        // MudBlazor uses JavaScript for browser measurements and focus. These
        // tests exercise rendered .NET behaviour, so unconfigured calls return defaults.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Renders the component after installing MudBlazor's shared providers once.</summary>
    /// <remarks>Register service mocks before calling this method or resolving services.</remarks>
    public IRenderedComponent<TComponent> RenderWithProviders<TComponent>(
        Action<ComponentParameterCollectionBuilder<TComponent>>? parameters = null)
        where TComponent : IComponent
    {
        if (!_providersRendered)
        {
            Render<MudPopoverProvider>();
            Render<MudDialogProvider>();
            Render<MudSnackbarProvider>();
            _providersRendered = true;
        }

        return Render<TComponent>(parameters);
    }
}