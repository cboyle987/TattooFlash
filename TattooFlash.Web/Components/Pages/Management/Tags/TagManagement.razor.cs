using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.Web.Components.Pages.Management.Tags
{
    public partial class TagManagement
    {
        public bool isLoading = false;

        public string loadingError = string.Empty;

        public List<TagModel> Tags = [];

        [Inject]
        public ITagService TagService { get; set; } = default!;
        [Inject]
        public ILogger<TagManagement> Logger { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            isLoading = true;
            loadingError = string.Empty;

            try
            {
                Tags = await TagService.GetAllTagsAsync();
            } catch(Exception ex)
            {
                Logger.LogError(ex, "Error loading tags");
                loadingError = "There has been an error loading tags please try again later.";
            } finally
            {
                isLoading = false;
            }
        }

        public void OnTagDeleted(Guid id)
        {
            Tags.RemoveAll(x => x.Id == id);
        }
    }
}
