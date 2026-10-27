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

        public async Task UpdateDesign()
        {
            _saveError = String.Empty;
            try
            {
                await DesignService.UpdateDesignAsync(DesignId, Design);
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
