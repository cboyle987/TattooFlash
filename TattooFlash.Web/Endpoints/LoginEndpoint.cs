using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace TattooFlash.Web.Endpoints;

public static class LoginEndpoint
{
    public static Task HandleAsync(HttpContext context, string redirectUri = "/")
    {
        var returnUrl = RedirectHttpResult.IsLocalUrl(redirectUri) ? redirectUri : "/";

        // Authentication redirects do not expand application-relative paths themselves.
        if (returnUrl.StartsWith("~/", StringComparison.Ordinal))
        {
            returnUrl = context.Request.PathBase + returnUrl[1..];
        }

        var authenticationProperties = new LoginAuthenticationPropertiesBuilder()
            .WithRedirectUri(returnUrl)
            .Build();

        return context.ChallengeAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
    }
}