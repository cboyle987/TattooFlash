using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using MudBlazor;
using TattooFlash.Web.Components.Dialogs;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Designs
{
    public partial class DesignCard
    {
        [Parameter]
        public DesignModel Design { get; set; } = default!;
        [Inject]
        public IDialogService DialogService { get; set; } = default!;

        [Inject]
        public IImageService ImageUrlService { get; set; } = default!;

        private string ImageUrl => ImageUrlService.GetImageUrl(Design.ImageName);

        private async Task OpenBookingDialog()
        {
            // Logic to open the booking dialog for the selected design
            var parameters = new DialogParameters<BookingDialog> { { x => x.Design, Design } };

            await DialogService.ShowAsync<BookingDialog>("Booking", parameters, options: new DialogOptions{
                MaxWidth = MaxWidth.ExtraLarge,
            });

        }
    }
}
