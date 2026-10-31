using Microsoft.AspNetCore.Components;
using TattooFlash.Models;
using TattooFlash.Services;

namespace TattooFlash.Components.Pages
{
    public partial class EditDesign
    {
        [Parameter]
        public Guid DesignId { get; set; }

        private string _saveError = String.Empty;
        public DesignModel? Design = default;

        private bool _isLoading = true;

        [Inject]
        public IDesignService DesignService { get; set; } = default!;
        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;
        [Inject]
        public ILogger<EditDesign> Logger { get; set; } = default!;


        protected override async Task OnParametersSetAsync()
        {
            _isLoading = true;
            try
            {
                Design = await DesignService.GetDesignByIdAsync(DesignId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading design");
                _saveError = "An error occurred while loading the design. Please try again later.";
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
                if(submission is null || submission.Design is null)
                {
                    _saveError = "Design is null. Cannot save.";
                    return;
                };

                if (submission.SelectedImage != null)
                {
                    await using var input = submission.SelectedImage.OpenReadStream(
    maxAllowedSize: 10 * 1024 * 1024);

                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer);
                    buffer.Position = 0;
                    await DesignService.UpdateDesignAsync(DesignId, submission.Design, buffer);

                } else
                {
                    await DesignService.UpdateDesignAsync(DesignId, submission.Design, null);

                }
                NavigationManager.NavigateTo("/management");

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving design");
                _saveError = "An Error has occurred saving your design please try again later.";
            }
        }
    }
}
