using Microsoft.AspNetCore.Components;
using TattooFlash.Services;
using TattooFlash.Models;

namespace TattooFlash.Components
{
    public partial class DesignsList
    {
        public List<DesignModel>  Designs { get; set; }  = new List<DesignModel>();
        [Inject]
        public IDesignService DesignService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await Task.Delay(4000);
            Designs = await DesignService.GetAllDesignsAsync();
        }
    }
}
