using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.ComponentModel.DataAnnotations;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Designs
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
        public List<TagModel> AvailableTags { get; set; } = new List<TagModel>();

        public IReadOnlyCollection<Guid> SelectedTags { get; set; } = new List<Guid>();

        [Parameter]
        public EventCallback<DesignSumbission> OnValidSubmit { get; set; }

        protected override Task OnParametersSetAsync()
        {
            SelectedTags = Design.Tags.Select(t => t.Id).ToList();
            return Task.CompletedTask;
        }

        private string ToTagDisplayName(Guid id)
        {
            var tag = AvailableTags.FirstOrDefault(t => t.Id == id);
            return tag?.DisplayName ?? "Unknown Tag";
        }

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


                await OnValidSubmit.InvokeAsync(new DesignSumbission() { Design = Design, SelectedImage = _selectedImage, SelectedTagIds = SelectedTags.ToList() });
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