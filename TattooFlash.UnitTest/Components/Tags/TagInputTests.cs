using Bunit;
using MudBlazor;
using TattooFlash.Web.Components.Tags;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Tags;

public class TagInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenRenderedThenNameAndSaveLabelReflectTheTag(bool existing)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var tag = new TagModel { Id = existing ? Guid.NewGuid() : Guid.Empty, DisplayName = "Old School" };

        // Act
        var form = context.RenderWithProviders<TagInput>(p => p.Add(x => x.Tag, tag));

        // Assert
        Assert.Equal("Old School", form.Find("input").GetAttribute("value"));
        Assert.Equal(existing ? "Update" : "Create", form.FindComponent<MudButton>().Find("button").TextContent.Trim());
        Assert.False(form.Find("input").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task WhenNameIsBlankThenSubmissionIsBlocked(string name)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var submissions = new List<TagModel>();
        var form = context.RenderWithProviders<TagInput>(p => p
            .Add(x => x.Tag, new TagModel { DisplayName = name })
            .Add(x => x.OnValidSubmit, (TagModel tag) => submissions.Add(tag)));

        // Act
        await form.FindComponent<MudButton>().Find("button").ClickAsync(new());

        // Assert
        Assert.Empty(submissions);
        Assert.Contains("Tag name is required.", form.Markup);
        Assert.False(form.FindComponent<MudButton>().Find("button").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task WhenNameLengthIsValidatedThenTheTrimmedBoundaryIsRespected(int length, bool valid)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var submissions = new List<TagModel>();
        var tag = new TagModel { DisplayName = " " + new string('a', length) + " " };
        var form = context.RenderWithProviders<TagInput>(p => p
            .Add(x => x.Tag, tag).Add(x => x.OnValidSubmit, (TagModel submitted) => submissions.Add(submitted)));

        // Act
        await form.FindComponent<MudButton>().Find("button").ClickAsync(new());

        // Assert
        Assert.Equal(valid, form.FindComponent<MudForm>().Instance.IsValid);
        if (valid)
            Assert.Same(tag, Assert.Single(submissions));
        else
        {
            Assert.Empty(submissions);
            Assert.Contains("Tag name must be 100 characters or fewer.", form.Markup);
        }
    }

    [Fact]
    public async Task WhenNameIsCorrectedThenSubmissionCanBeRetried()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var submissions = new List<TagModel>();
        var tag = new TagModel();
        var form = context.RenderWithProviders<TagInput>(p => p
            .Add(x => x.Tag, tag).Add(x => x.OnValidSubmit, (TagModel submitted) => submissions.Add(submitted)));
        await form.FindComponent<MudButton>().Find("button").ClickAsync(new());
        Assert.Empty(submissions);

        // Act
        form.Find("input").Change("Flowers");
        await form.FindComponent<MudButton>().Find("button").ClickAsync(new());

        // Assert
        Assert.Equal("Flowers", Assert.Single(submissions).DisplayName);
        Assert.DoesNotContain("Tag name is required.", form.Markup);
    }

    [Fact]
    public async Task WhenSavingThenControlsAreDisabledAndRepeatedSubmissionIsIgnored()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var submissions = 0;
        var form = context.RenderWithProviders<TagInput>(p => p
            .Add(x => x.Tag, new TagModel { DisplayName = "Flowers" })
            .Add(x => x.OnValidSubmit, async (TagModel _) => { submissions++; await pending.Task; }));

        // Act
        var saving = form.FindComponent<MudButton>().Find("button").ClickAsync(new());
        form.WaitForAssertion(() =>
        {
            Assert.Contains("Saving", form.FindComponent<MudButton>().Find("button").TextContent);
            Assert.True(form.FindComponent<MudButton>().Find("button").HasAttribute("disabled"));
            Assert.True(form.Find("input").HasAttribute("disabled"));
        });
        // Invoke the event explicitly to check the guard even if an event is queued.
        await form.InvokeAsync(() => form.FindComponent<MudButton>().Instance.OnClick.InvokeAsync(new()));
        Assert.Equal(1, submissions);
        pending.SetResult();
        await saving;

        // Assert
        Assert.False(form.FindComponent<MudButton>().Find("button").HasAttribute("disabled"));
        Assert.False(form.Find("input").HasAttribute("disabled"));
        Assert.Equal("Create", form.FindComponent<MudButton>().Find("button").TextContent.Trim());
    }
}