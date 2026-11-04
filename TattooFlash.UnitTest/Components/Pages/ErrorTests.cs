using System.Diagnostics;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using ErrorPage = TattooFlash.Web.Components.Pages.Error;

namespace TattooFlash.UnitTest.Components.Pages;

public class ErrorTests
{
    [Fact]
    public async Task WhenActivityHasAnIdThenItTakesPrecedenceOverTheHttpTraceId()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var originalActivity = Activity.Current;
        try
        {
            Activity.Current = null;
            using var activity = new Activity("error-page-test").Start();
            var http = new DefaultHttpContext { TraceIdentifier = "http-request-id" };

            // Act
            var page = context.Render<CascadingValue<HttpContext>>(p => p
                .Add(x => x.Value, http).AddChildContent<ErrorPage>());

            // Assert
            Assert.Equal(activity.Id, page.Find("code").TextContent);
            Assert.DoesNotContain("http-request-id", page.Markup);
            Assert.Equal("Error.", page.Find("h1").TextContent);
            Assert.Equal("An error occurred while processing your request.", page.Find("h2").TextContent);
            Assert.Contains(page.FindAll("p"), paragraph =>
                paragraph.TextContent.Contains("Development environment shouldn't be enabled for deployed applications"));
        }
        finally
        {
            Activity.Current = originalActivity;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenActivityIdIsAbsentThenHttpTraceIdIsDisplayed(bool unstartedActivity)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var originalActivity = Activity.Current;
        using var activity = new Activity("unstarted-error-page-test");
        try
        {
            Activity.Current = unstartedActivity ? activity : null;
            var http = new DefaultHttpContext { TraceIdentifier = "http-request-id" };

            // Act
            var page = context.Render<CascadingValue<HttpContext>>(p => p
                .Add(x => x.Value, http).AddChildContent<ErrorPage>());

            // Assert
            Assert.Equal("http-request-id", page.Find("code").TextContent);
            Assert.Contains("Request ID:", page.Markup);
        }
        finally
        {
            Activity.Current = originalActivity;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenNoRequestIdIsAvailableThenRequestIdIsOmitted(bool hasHttpContext)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var originalActivity = Activity.Current;
        try
        {
            Activity.Current = null;

            // Act
            if (hasHttpContext)
            {
                var http = new DefaultHttpContext { TraceIdentifier = "" };
                var page = context.Render<CascadingValue<HttpContext>>(p => p
                    .Add(x => x.Value, http).AddChildContent<ErrorPage>());

                // Assert
                Assert.Empty(page.FindAll("code"));
                Assert.DoesNotContain("Request ID:", page.Markup);
            }
            else
            {
                var page = context.Render<ErrorPage>();

                // Assert
                Assert.Empty(page.FindAll("code"));
                Assert.DoesNotContain("Request ID:", page.Markup);
                Assert.Equal("Error.", page.Find("h1").TextContent);
            }
        }
        finally
        {
            Activity.Current = originalActivity;
        }
    }
}