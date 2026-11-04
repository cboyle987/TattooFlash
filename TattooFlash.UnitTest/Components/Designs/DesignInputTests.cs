using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MudBlazor;
using MudBlazor.Extensions;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Designs;

public class DesignInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenRenderedThenFieldsAndSaveLabelReflectTheDesign(bool existing)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = new DesignModel { Id = existing ? Guid.NewGuid() : Guid.Empty, Name = "Swallow", Price = 125 };

        // Act
        var form = context.RenderWithProviders<DesignInput>(p => p.Add(x => x.Design, design));

        // Assert
        Assert.Equal("Swallow", form.FindComponent<MudTextField<string>>().Find("input").GetAttribute("value"));
        Assert.Equal(125, form.FindComponent<MudNumericField<int>>().Instance.GetState(x => x.Value));
        Assert.Equal(0, form.FindComponent<MudNumericField<int>>().Instance.Min);
        Assert.Equal(existing ? "Update" : "Create", FindSaveButton(form).Find("button").TextContent.Trim());
        Assert.Equal("image/jpeg,image/png,image/webp", form.FindComponent<MudFileUpload<IBrowserFile>>().Instance.Accept);
        AssertSaveControlsEnabled(form);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("existing.webp", false)]
    public async Task WhenRenderedThenImageIsRequiredOnlyWithoutAnExistingImage(string? imageName, bool required)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = new DesignModel { Name = "Swallow", ImageName = imageName! };

        // Act
        var form = context.RenderWithProviders<DesignInput>(p => p.Add(x => x.Design, design));

        // Assert
        Assert.Equal(required, form.FindComponent<MudFileUpload<IBrowserFile>>().Instance.Required);
    }

    [Theory]
    [InlineData("", "existing.webp", "Design name is required.")]
    [InlineData("  ", "existing.webp", "Design name is required.")]
    [InlineData("Swallow", "", "Please select an image")]
    public async Task WhenRequiredDataIsMissingThenValidationPreventsSubmission(string name, string imageName, string error)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = new DesignModel { Name = name, ImageName = imageName };
        var submissions = new List<DesignSumbission>();
        var form = RenderForm(context, design, submissions);

        // Act
        await FindSaveButton(form).Find("button").ClickAsync(new());

        // Assert
        Assert.Empty(submissions);
        Assert.False(form.FindComponent<MudForm>().Instance.IsValid);
        Assert.Contains(error, form.Markup);
        AssertSaveControlsEnabled(form);
    }

    [Fact]
    public async Task WhenValidationErrorIsCorrectedThenTheFormCanBeSubmitted()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = new DesignModel { Name = "", ImageName = "existing.webp" };
        var submissions = new List<DesignSumbission>();
        var form = RenderForm(context, design, submissions);
        var saveButton = FindSaveButton(form);
        await saveButton.Find("button").ClickAsync(new());
        Assert.Empty(submissions);
        Assert.Contains("Design name is required.", form.Markup);

        // Act
        form.FindComponent<MudTextField<string>>().Find("input").Change("Swallow");
        await saveButton.Find("button").ClickAsync(new());

        // Assert
        Assert.Same(design, Assert.Single(submissions).Design);
        Assert.True(form.FindComponent<MudForm>().Instance.IsValid);
        Assert.DoesNotContain("Design name is required.", form.Markup);
        Assert.False(saveButton.Instance.Disabled);
    }

    [Fact]
    public async Task WhenExistingDesignIsEditedThenSubmissionContainsUpdatedFieldsWithoutAReplacementFile()
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = CreateExistingDesign();
        var submissions = new List<DesignSumbission>();
        var form = RenderForm(context, design, submissions);

        // Act
        form.FindComponent<MudTextField<string>>().Find("input").Change("Rose");
        form.FindComponent<MudNumericField<int>>().Find("input").Change("80");
        await FindSaveButton(form).Find("button").ClickAsync(new());

        // Assert
        var submission = Assert.Single(submissions);
        Assert.Same(design, submission.Design);
        Assert.Equal("Rose", submission.Design.Name);
        Assert.Equal(80, submission.Design.Price);
        Assert.Null(submission.SelectedImage);
        Assert.True(form.FindComponent<MudForm>().Instance.IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenFileIsSelectedThenSubmissionContainsTheSelectedFile(bool existing)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var design = new DesignModel
        {
            Id = existing ? Guid.NewGuid() : Guid.Empty,
            Name = "Swallow", ImageName = existing ? "existing.webp" : ""
        };
        var file = Mock.Of<IBrowserFile>(f => f.Name == "replacement.webp" && f.ContentType == "image/webp" && f.Size == 100);
        var submissions = new List<DesignSumbission>();
        var form = RenderForm(context, design, submissions);

        // Act: exercise the upload component's public callback, which updates the parent's binding.
        var upload = form.FindComponent<MudFileUpload<IBrowserFile>>();
        await form.InvokeAsync(() => upload.Instance.FilesChanged.InvokeAsync(file));
        await FindSaveButton(form).Find("button").ClickAsync(new());

        // Assert
        var submission = Assert.Single(submissions);
        Assert.Same(design, submission.Design);
        Assert.Same(file, submission.SelectedImage);
        Assert.True(form.FindComponent<MudForm>().Instance.IsValid);
        Assert.False(FindSaveButton(form).Instance.Disabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhileSavingThenDuplicateSubmissionsAreIgnoredAndCompletionAllowsRetry(bool fails)
    {
        // Arrange
        await using var context = new ComponentTestContext();
        var logger = new Mock<ILogger<DesignInput>>();
        context.Services.AddSingleton(logger.Object);
        var design = CreateExistingDesign();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var submissions = new List<DesignSumbission>();
        var form = context.RenderWithProviders<DesignInput>(p => p
            .Add(x => x.Design, design)
            .Add(x => x.OnValidSubmit, (DesignSumbission value) =>
            {
                submissions.Add(value);
                return submissions.Count == 1 ? pending.Task : Task.CompletedTask;
            }));
        var button = FindSaveButton(form);

        // Act
        var saving = button.Find("button").ClickAsync(new());
        form.WaitForAssertion(() =>
        {
            Assert.Single(submissions);
            Assert.True(button.Instance.Disabled);
            Assert.True(form.FindComponent<MudFileUpload<IBrowserFile>>().Instance.Disabled);
            Assert.Single(form.FindComponents<MudProgressCircular>());
            Assert.Contains("Saving", button.Markup);
        });

        // Invoke the public callback directly to exercise DesignInput's busy guard.
        await form.InvokeAsync(() => button.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        Assert.Single(submissions);
        var exception = new InvalidOperationException("Save failed");
        if (fails)
            pending.SetException(exception);
        else
            pending.SetResult(true);
        await saving;

        // Assert
        form.WaitForAssertion(() =>
        {
            AssertSaveControlsEnabled(form);
            Assert.Equal("Update", button.Find("button").TextContent.Trim());
        });
        logger.Verify(x => x.Log(
            LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, type) => value.ToString() == "Error saving design"),
            exception, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), fails ? Times.Once() : Times.Never());

        await button.Find("button").ClickAsync(new());
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, submission => Assert.Same(design, submission.Design));
    }

    private static DesignModel CreateExistingDesign() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Swallow",
        Price = 125,
        ImageName = "existing.webp"
    };

    // The caller owns the context and captured submissions; neither is shared between tests.
    private static IRenderedComponent<DesignInput> RenderForm(
        ComponentTestContext context, DesignModel design, List<DesignSumbission> submissions) =>
        context.RenderWithProviders<DesignInput>(p => p
            .Add(x => x.Design, design)
            .Add(x => x.OnValidSubmit, (DesignSumbission value) => submissions.Add(value)));

    private static IRenderedComponent<MudButton> FindSaveButton(IRenderedComponent<DesignInput> form) =>
        // MudFileUpload renders an internal button too; identify the save action by its visible label.
        form.FindComponents<MudButton>().Single(button =>
            button.Find("button").TextContent.Trim() is "Create" or "Update" or "Saving");

    private static void AssertSaveControlsEnabled(IRenderedComponent<DesignInput> form)
    {
        Assert.False(FindSaveButton(form).Instance.Disabled);
        Assert.False(form.FindComponent<MudFileUpload<IBrowserFile>>().Instance.Disabled);
        Assert.Empty(form.FindComponents<MudProgressCircular>());
    }
}
