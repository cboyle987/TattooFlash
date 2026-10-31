using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Interfaces;
using TattooFlash.Models;

namespace TattooFlash.Components.Dialogs
{
    public partial class BookingDialog
    {
        [Inject]
        public IImageService ImageUrlService { get; set; } = default!;

        [Inject]
        private IConfiguration Configuration { get; set; } = default!;

        [Parameter]
        public DesignModel Design { get; set; } = default!;

        private string InstagramUserName => Configuration["Booking:InstagramUserName"]
            ?? throw new InvalidOperationException("Missing Booking:InstagramUserName configuration.");

        private string InstagramLink => $"https://ig.me/m/{InstagramUserName}";

        [CascadingParameter]
        private IMudDialogInstance MudDialog { get; set; } = default!;

        private string ImageUrl => ImageUrlService.GetImageUrl(Design.ImageName);


        private void Cancel() => MudDialog.Cancel();

    }
}
