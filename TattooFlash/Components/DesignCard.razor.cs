using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using MudBlazor;
using TattooFlash.Models;

namespace TattooFlash.Components
{
    public partial class DesignCard
    {
        [Parameter]
        public DesignModel Design { get; set; }
        [Inject]
        public IDialogService DialogService { get; set; }

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
