using Microsoft.AspNetCore.Components;
using MudBlazor;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Tags;

public partial class TagInput
{
    [Parameter, EditorRequired]
    public TagModel Tag { get; set; } = default!;

    [Parameter]
    public EventCallback<TagModel> OnValidSubmit { get; set; }

    private MudForm? _form;
    private bool _isSaving;

    private static string? ValidateName(string? name) =>
        name?.Trim().Length > 100 ? "Tag name must be 100 characters or fewer." : null;

    private async Task HandleSubmit()
    {
        if (_isSaving || _form is null)
            return;

        _isSaving = true;
        try
        {
            await _form.ValidateAsync();
            if (_form.IsValid)
                await OnValidSubmit.InvokeAsync(Tag);
        }
        finally
        {
            _isSaving = false;
        }
    }
}