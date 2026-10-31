using Microsoft.AspNetCore.Components;
using TattooFlash.Components.Interfaces;
using TattooFlash.Models;

namespace TattooFlash.Components.Pages.Management
{
    public partial class DesignManagement
    {
        [Inject]
        public IDesignService DesignService { get; set; } = default!;
        [Inject]
        public ILogger<DesignManagement> Logger { get; set; } = default!;
        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;

        public PaginatedDesignResults Results { get; set; } = new PaginatedDesignResults { TotalItems = 0, Designs = [] };
        private bool IsLoading = false;
        private string LoadingError = String.Empty;

        protected override async Task OnInitializedAsync()
        {
            await LoadDesigns(1);
        }

        public async Task LoadDesigns(int pageNumber)
        {
            try
            {
                LoadingError = string.Empty;
                IsLoading = true;
                Results = await DesignService.GetPaginatedDesigns(pageNumber);
            } catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading designs for management page");
                LoadingError = "There has been an error loading designs please try again later";
            } finally
            {
                IsLoading = false;
            }
        }

        private void RemoveDesign(Guid id)
        {
            Results.Designs.RemoveAll(design => design.Id == id);
            Results.TotalItems -= 1;
            if(Results.Designs.Count == 0)
            {
                NavigationManager.NavigateTo(NavigationManager.Uri, true);
            }
        }

        private void UpdateAvailability(Guid id)
        {
            var design = Results.Designs.FirstOrDefault(design => design.Id == id);
            if (design is not null)
                design.IsAvailable = !design.IsAvailable;
        }
    }
}
