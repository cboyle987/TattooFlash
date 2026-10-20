using Microsoft.AspNetCore.Components;
using TattooFlash.Models;
using TattooFlash.Services;

namespace TattooFlash.Components.Pages
{
    public partial class DesignManagement
    {
        [Inject]
        public IDesignService DesignService { get; set; }

        public List<DesignModel> Designs { get; set; } = new List<DesignModel>();

        protected override async Task OnInitializedAsync()
        {
            await Task.Delay(300);
            Designs = await DesignService.GetAllDesignsAsync();
        }
    }
}
