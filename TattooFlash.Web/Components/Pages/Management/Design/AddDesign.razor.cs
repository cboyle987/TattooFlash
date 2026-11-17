using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using MudBlazor;
using TattooFlash.Web.Helpers;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Pages.Management.Design
{

    public partial class AddDesign
    {
        public DesignModel NewDesign { get; set; } = default!;

        [Inject]
        public IDesignService DesignService { get; set; } = default!;

        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        public ILogger<AddDesign> Logger { get; set; } = default!;
        private string _saveError = String.Empty;

        [Inject]
        public ITagService TagService { get; set; } = default!;

        private List<TagModel> _availableTags { get; set; } = new List<TagModel>();

        private bool _isLoading = false;
        private string _loadError = String.Empty;

        override protected async Task OnInitializedAsync()
        {
            NewDesign = new DesignModel() { Name = String.Empty, Price = 0, ImageName = String.Empty, IsAvailable = true };

            _isLoading = true;
            _loadError = string.Empty;
            try
            {
                _availableTags = await TagService.GetAllTagsAsync();

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading tags");
                _loadError = "An error occurred while loading the tags. Please try again later.";
            }
            finally
            {
                _isLoading = false;
            }

        }

        private async Task CreateDesign(DesignSumbission submission)
        {
            _saveError = String.Empty;
            try
            {
                if (submission.SelectedImage == null)
                {
                    _saveError = "Unable to read selected Image.";
                    return;
                }

                await using var input = submission.SelectedImage.OpenReadStream(
    maxAllowedSize: 10 * 1024 * 1024);

                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer);
                buffer.Position = 0;


                var result = await DesignService.CreateDesignAsync(submission.Design, buffer, submission.SelectedTagIds);
                if (!result.IsSuccess)
                {
                    var error = result.ImageError
                        ?? throw new InvalidOperationException("Design save failed without an image error code.");
                    _saveError = ImageSaveErrorHelper.GetImageSaveErrorMessage(error);
                    return;
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