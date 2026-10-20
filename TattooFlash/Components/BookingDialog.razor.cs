using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Models;

namespace TattooFlash.Components
{
    public partial class BookingDialog
    {
        [Parameter]
        public DesignModel Design { get; set; }

        public string InstagramUserName { get; set; } = "emmamccormickart";

        private string InstagramLink => $"https://ig.me/m/{InstagramUserName}";

        [CascadingParameter]
        private IMudDialogInstance MudDialog { get; set; }


        private void Cancel() => MudDialog.Cancel();

    }
}
