using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Models;
using TattooFlash.Services;

namespace TattooFlash.Components.Pages
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

        override protected Task OnInitializedAsync()
        {
            NewDesign = new DesignModel() { Name = String.Empty, Price = 0, ImageName = String.Empty, IsAvailable = true };

            return base.OnInitializedAsync();
        }

        private async Task CreateDesign()
        {
            _saveError = String.Empty;
            try {
                await DesignService.CreateDesignAsync(NewDesign);
                NavigationManager.NavigateTo("/management");

            } catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving design");
                _saveError = "An Error has occurred saving your design please try again later.";
            } 
        }

    }
}
