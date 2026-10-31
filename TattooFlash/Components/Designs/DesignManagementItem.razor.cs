using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Components.Dialogs;
using TattooFlash.Components.Interfaces;
using TattooFlash.Models;

namespace TattooFlash.Components.Designs
{
    public partial class DesignManagementItem
    {
        [Parameter]
        public DesignModel Design { get; set; } = default!;
        [Parameter]
        public EventCallback<Guid> OnDesignDeleted { get; set; }
        [Parameter]
        public EventCallback<Guid> OnAvailabilityChanged { get; set; }
        [Inject]
        public IDesignService DesignService { get; set; } = default!;
        [Inject]
        public IDialogService DialogService { get; set; } = default!;
        [Inject]
        public IImageService imageUrlService { get; set; } = default!;
        [Inject]
        public ISnackbar Snackbar { get; set; } = default!;

        private bool IsBusy { get; set; }
        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;

        private string ImageUrl => imageUrlService.GetImageUrl(Design.ImageName);

        private void EditDesign()
        {
            NavigationManager.NavigateTo($"/management/edit-design/{Design.Id.ToString()}");
        }

        private async Task DeleteDesign()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            try
            {
                var dialogParameters = new DialogParameters
            {
                { "Design", Design }
            };
                var confirmDialog = await DialogService.ShowAsync<DeleteConfirmationDialog>("Confirm Delete", dialogParameters);
                var confirmation = await confirmDialog.Result;

                if (confirmation is { Canceled: false })
                {
                    var result = await DesignService.DeleteDesignAsync(Design.Id);
                    await OnDesignDeleted.InvokeAsync(Design.Id);
                    if (!result)
                        Snackbar.Add("This design was already deleted.", Severity.Info);
                }
            }
            catch (Exception)
            {
                Snackbar.Add("Could not delete the design. Please try again.", Severity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ToggleAvailability()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            try
            {
                await DesignService.ToggleAvailabilityAsync(Design.Id);
                await OnAvailabilityChanged.InvokeAsync(Design.Id);
            }
            catch (Exception)
            {
                Snackbar.Add("Could not change availability. Please try again.", Severity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
