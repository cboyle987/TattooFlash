using Microsoft.AspNetCore.Components;
using TattooFlash.Web.Models;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Amazon.Runtime.SharedInterfaces;
using TattooFlash.Web.Interfaces;

namespace TattooFlash.Web.Components.Designs
{
    public partial class DesignsList
    {
        public PaginatedDesignResults Results { get; set; } = new PaginatedDesignResults() { TotalItems = 0, Designs = [] };

        [Inject]
        public IDesignService DesignService { get; set; } = default!;
        [Inject]
        public ILogger<DesignsList> Logger { get; set; } = default!;

        private bool _Loading = false;
        private string _loadError = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            await LoadDesigns(1);
        }

        private async Task LoadDesigns(int pageNumber)
        {
            try
            {
                _loadError = string.Empty;
                _Loading = true;
                Results = await DesignService.GetPaginatedDesigns(pageNumber);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retriveing designs");
                _loadError = "There has been an error retrieving available designs please try again later.";

            }
            finally
            {
                _Loading = false;
            }
        }
    }
}