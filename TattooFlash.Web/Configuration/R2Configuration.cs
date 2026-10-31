using System.ComponentModel.DataAnnotations;

namespace TattooFlash.Web.Configuration
{
    public class R2Configuration
    {
        [Required]
        public string AccessKeyId { get; set; } = string.Empty; 
        [Required]
        public string SecretAccessKey { get; set; } = string.Empty;
        [Required]
        public string BucketName { get; set; } = string.Empty;
        [Required]
        public string Prefix { get; set; } = string.Empty;
        [Required]  
        public string Endpoint { get; set; } = string.Empty;
        [Required]
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}
