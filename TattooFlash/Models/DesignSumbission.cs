using Microsoft.AspNetCore.Components.Forms;

namespace TattooFlash.Models
{
    public record DesignSumbission()
    {
        public required DesignModel Design { get; init; }
        public IBrowserFile? SelectedImage { get; init; }
    }
}
