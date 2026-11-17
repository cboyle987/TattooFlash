using System.ComponentModel.DataAnnotations;
using TattooFlash.Web.Helpers;

namespace TattooFlash.Web.Models
{
    public class TagModel
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        [MinLength(1)]
        public string NormalizedName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [MinLength(1)]
        public string DisplayName { get; set; } = string.Empty;

        public List<DesignModel> Designs { get; set; } = [];

    }
}