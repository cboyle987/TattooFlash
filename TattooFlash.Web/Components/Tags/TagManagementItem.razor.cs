using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Web.Components.Dialogs;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Tags
{
    public partial class TagManagementItem
    {
        [Parameter]
        public TagModel Tag { get; set; } = default!;

        [Inject]
        public IDialogService DialogService { get; set; } = default!;

        [Inject]
        public ITagService TagService { get; set; } = default!;

        [Inject]
        public ISnackbar Snackbar { get; set; } = default!;

        [Parameter]
        public EventCallback<Guid> OnTagDeleted { get; set; } = default!;

        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;

        private bool IsBusy = false;

        public async Task DeleteTag()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            try
            {
                var dialogParameters = new DialogParameters
            {
                { "Name", Tag.DispalyName }
            };
                var confirmDialog = await DialogService.ShowAsync<DeleteConfirmationDialog>("Confirm Delete", dialogParameters);
                var confirmation = await confirmDialog.Result;

                if (confirmation is { Canceled: false })
                {
                    var result = await TagService.DeleteTagAsync(Tag.Id);
                    await OnTagDeleted.InvokeAsync(Tag.Id);


                    if (!result)
                    {
                        Snackbar.Add("This tag was already deleted.", Severity.Info);
                    }
                    else
                    {
                        Snackbar.Add("Tag deleted successfully.", Severity.Success);
                    }
                }
            }
            catch (Exception)
            {
                Snackbar.Add("Could not tag the design. Please try again.", Severity.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void EditTag()
        {
            NavigationManager.NavigateTo($"/management/tag/edit/{Tag.Id}");
        }
    }
}