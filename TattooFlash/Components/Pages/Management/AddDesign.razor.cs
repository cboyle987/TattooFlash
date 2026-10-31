using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using MudBlazor;
using TattooFlash.Interfaces;
using TattooFlash.Models;

namespace TattooFlash.Components.Pages.Management
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

        private async Task CreateDesign(DesignSumbission submission)
        {
            _saveError = String.Empty;
            try {
                if(submission.SelectedImage == null)
                {
                    _saveError = "Unable to read selected Image.";
                    return;
                }

                await using var input = submission.SelectedImage.OpenReadStream(
    maxAllowedSize: 10 * 1024 * 1024);

                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer);
                buffer.Position = 0;


                await DesignService.CreateDesignAsync(submission.Design, buffer);
                NavigationManager.NavigateTo("/management");

            } catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving design");
                _saveError = "An Error has occurred saving your design please try again later.";
            } 
        }

    }
}
