namespace TattooFlash.Web.Models
{
    public record ProcessedImage
    {
        public required Stream Content { get; init; }
        public required string ContentType { get; init; }
        public required string Extension { get; init; }
    }
}