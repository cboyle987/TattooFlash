using Microsoft.AspNetCore.Components;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Pages.Management.Tags;

public partial class AddTag
{
    public TagModel NewTag { get; set; } = new();

    [Inject]
    public ITagService TagService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public ILogger<AddTag> Logger { get; set; } = default!;

    private string _saveError = string.Empty;

    private async Task CreateTag(TagModel tag)
    {
        _saveError = string.Empty;
        try
        {
            var result = await TagService.CreateTagAsync(tag.DisplayName);
            if (!result.IsSuccess)
            {
                _saveError = result.TagSaveError;
                return;
            }

            NavigationManager.NavigateTo("/management/tag");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating tag");
            _saveError = "An error occurred while saving the tag. Please try again.";
        }
    }
}