using Microsoft.AspNetCore.Components;
using TattooFlash.Web.Helpers;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Pages.Management.Design
{
    public partial class EditDesign
    {
        [Parameter]
        public Guid DesignId { get; set; }

        private string _saveError = String.Empty;
        private string _loadError = String.Empty;

        private List<TagModel> _availableTags = [];
        public DesignModel? Design = default;

        private bool _isLoading = true;

        [Inject]
        public IDesignService DesignService { get; set; } = default!;
        [Inject]
        public ITagService TagService { get; set; } = default!;
        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;
        [Inject]
        public ILogger<EditDesign> Logger { get; set; } = default!;


        protected override async Task OnParametersSetAsync()
        {
            _isLoading = true;
            _loadError = String.Empty;
            Design = null;
            _saveError = String.Empty;
            try
            {
                Design = await DesignService.GetDesignByIdAsync(DesignId);
                if (Design is null)
                {
                    Logger.LogWarning("Design with ID {DesignId} not found.", DesignId);
                    _loadError = "Design not found.";
                }
                _availableTags = await TagService.GetAllTagsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading design");
                _loadError = "An error occurred while loading the design. Please try again later.";
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Task UpdateDesign(DesignSumbission submission)
        {
            _saveError = String.Empty;
            try
            {
                if (submission is null || submission.Design is null)
                {
                    _saveError = "Design is null. Cannot save.";
                    return;
                }
                ;

                if (submission.SelectedImage != null)
                {
                    await using var input = submission.SelectedImage.OpenReadStream(
    maxAllowedSize: 10 * 1024 * 1024);

                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer);
                    buffer.Position = 0;
                    var result = await DesignService.UpdateDesignAsync(DesignId, submission.Design, buffer, submission.SelectedTagIds);
                    if (!result.IsSuccess)
                    {
                        var error = result.ImageError
                            ?? throw new InvalidOperationException("Design save failed without an image error code.");
                        _saveError = ImageSaveErrorHelper.GetImageSaveErrorMessage(error);
                        return;
                    }

                }
                else
                {
                    await DesignService.UpdateDesignAsync(DesignId, submission.Design, null, submission.SelectedTagIds);

                }
                NavigationManager.NavigateTo("/management/design");

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving design");
                _saveError = "An Error has occurred saving your design please try again later.";
            }
        }
    }
}