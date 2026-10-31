# Shared component test setup

`ComponentTestContext` wraps bUnit's `BunitContext` with MudBlazor services,
controllable test authentication, and a `RenderWithProviders<TComponent>` helper.
The helper renders popover, dialog and snackbar providers once, then the requested
component. It does not render the application layout or start the application.

Create a context per test and dispose it asynchronously. MudBlazor services may
require asynchronous disposal. Do not share a context through an xUnit fixture;
service registrations, rendered components, navigation and authentication state
must remain isolated between tests.

```csharp
// Arrange
await using var context = new ComponentTestContext();
var service = new Mock<IDesignService>(MockBehavior.Strict);
context.Services.AddSingleton(service.Object);
context.Authorization.SetAuthorized("test-user");

// Act
var page = context.RenderWithProviders<AddDesign>();

// Assert
Assert.Equal("Add New Design", page.Find("h3").TextContent);
```

Use `using Bunit;` for rendering/query/event extension methods and
`using Microsoft.Extensions.DependencyInjection;` for service registration.
Register mocks before rendering or resolving services: bUnit freezes service
registrations when its service provider is first used. Configure only the calls
needed by each scenario; the shared context does not supply application mocks.

Authentication starts unauthenticated. Use `Authorization.SetAuthorized(...)`,
`SetNotAuthorized()`, or role/policy setup to arrange the required scenario.
Rendering a page directly does not exercise the router's `[Authorize]` handling;
route access tests need an authorization-aware router or wrapper.

bUnit supplies test navigation automatically. Resolve `NavigationManager` after
registering mocks to inspect its URI after an action. For asynchronous rendering,
use bUnit's `WaitForAssertion` helpers rather than arbitrary delays.

JavaScript interop uses loose mode to accommodate MudBlazor's browser measurement
and focus calls. JavaScript is not executed. Tests that need to verify interop
should switch `context.JSInterop.Mode` to `JSRuntimeMode.Strict` and configure the
expected calls explicitly. These tests do not verify browser layout or JavaScript.

Tests for layouts that render their own providers should use bUnit's plain
`Render<TComponent>` rather than `RenderWithProviders` to avoid duplicate providers.

Run component tests from the repository root:

```powershell
dotnet test TattooFlash.slnx --filter FullyQualifiedName~TattooFlash.UnitTest.Components
```
