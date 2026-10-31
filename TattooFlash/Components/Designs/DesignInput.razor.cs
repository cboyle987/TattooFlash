using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.ComponentModel.DataAnnotations;
using TattooFlash.Models;

namespace TattooFlash.Components.Designs
{
    public partial class DesignInput
    {
        [Parameter, EditorRequired]
        public DesignModel Design { get; set; } = default!;
        [Inject]
        public ILogger<DesignInput> Logger { get; set; } = default!;

        private IBrowserFile? _selectedImage;

        private MudForm? _form;

        private bool _isSaving = false;

        private string SaveButtonText() => Design.Id == Guid.Empty ? "Create" : "Update";

        [Parameter]
        public EventCallback<DesignSumbission> OnValidSubmit { get; set; }

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


                await OnValidSubmit.InvokeAsync(new DesignSumbission() { Design = Design, SelectedImage = _selectedImage });
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
