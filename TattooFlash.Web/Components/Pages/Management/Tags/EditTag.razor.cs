using Microsoft.AspNetCore.Components;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Pages.Management.Tags;

public partial class EditTag
{
    [Parameter]
    public Guid TagId { get; set; }

    public TagModel? Tag { get; set; }

    [Inject]
    public ITagService TagService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public ILogger<EditTag> Logger { get; set; } = default!;

    private bool _isLoading = true;
    private string _loadError = string.Empty;
    private string _saveError = string.Empty;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _loadError = string.Empty;
        _saveError = string.Empty;
        Tag = null;
        try
        {
            var stored = await TagService.GetTagByIdAsync(TagId);
            if (stored is null)
            {
                Logger.LogWarning("Tag with ID {TagId} not found", TagId);
                _loadError = "Tag not found.";
                return;
            }

            // Edit a local copy so a failed save cannot rename the loaded object.
            Tag = new TagModel { Id = stored.Id, DisplayName = stored.DisplayName };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading tag");
            _loadError = "An error occurred while loading the tag. Please try again later.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task UpdateTag(TagModel tag)
    {
        _saveError = string.Empty;
        try
        {
            var result = await TagService.UpdateTagAsync(TagId, tag.DisplayName);
            if (!result.IsSuccess)
            {
                _saveError = result.TagSaveError;
                return;
            }

            NavigationManager.NavigateTo("/management/tag");
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Tag with ID {TagId} was deleted before saving", TagId);
            _saveError = "Tag not found. It may have been deleted.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating tag");
            _saveError = "An error occurred while saving the tag. Please try again.";
        }
    }
}