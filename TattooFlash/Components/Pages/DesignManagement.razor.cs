using Microsoft.AspNetCore.Components;
using TattooFlash.Models;
using TattooFlash.Services;

namespace TattooFlash.Components.Pages
{
    public partial class DesignManagement
    {
        [Inject]
        public IDesignService DesignService { get; set; } = default!;

        public List<DesignModel> Designs { get; set; } = new List<DesignModel>();
        public bool IsLoading { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadDesigns();
        }

        public async Task LoadDesigns()
        {
            IsLoading = true;
            Designs = await DesignService.GetAllDesignsAsync();
            IsLoading = false;
        }

        private void RemoveDesign(Guid id)
        {
            Designs.RemoveAll(design => design.Id == id);
        }

        private void UpdateAvailability(Guid id)
        {
            var design = Designs.FirstOrDefault(design => design.Id == id);
            if (design is not null)
                design.IsAvailable = !design.IsAvailable;
        }
    }
}
