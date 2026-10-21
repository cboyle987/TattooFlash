using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Models;
using TattooFlash.Services;

namespace TattooFlash.Components
{
    public partial class BookingDialog
    {
        [Inject]
        public IImageUrlService ImageUrlService { get; set; }

        [Inject]
        private IConfiguration Configuration { get; set; } = default!;

        [Parameter]
        public DesignModel Design { get; set; }

        private string InstagramUserName => Configuration["Booking:InstagramUserName"]
            ?? throw new InvalidOperationException("Missing Booking:InstagramUserName configuration.");

        private string InstagramLink => $"https://ig.me/m/{InstagramUserName}";

        [CascadingParameter]
        private IMudDialogInstance MudDialog { get; set; }

        private string ImageUrl => ImageUrlService.GetImageUrl(Design.ImageName);


        private void Cancel() => MudDialog.Cancel();

    }
}
