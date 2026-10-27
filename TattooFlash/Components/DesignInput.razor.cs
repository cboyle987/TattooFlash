using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Models;

namespace TattooFlash.Components
{
    public partial class DesignInput
    {
        [Parameter, EditorRequired]
        public DesignModel Design { get; set; } = default!;
        [Inject]
        public ILogger<DesignInput> Logger { get; set; } = default!;

        private MudForm _form;

        private bool _isSaving = false;

        private string SaveButtonText() => Design.Id == Guid.Empty ? "Create" : "Update";

        [Parameter]
        public EventCallback<DesignModel> OnValidSubmit { get; set; }

        private async Task HandleSubmit()
        {
            if (_isSaving || _form is null)
            {
                return;
            }
            _isSaving = true;
            try
            {

                await _form.ValidateAsync();
                if (!_form.IsValid)
                {
                    _isSaving = false;
                    return;
                }


                await OnValidSubmit.InvokeAsync(Design);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving design");
            }
            finally
            {
                _isSaving = false;
            }
        }

    }
}
