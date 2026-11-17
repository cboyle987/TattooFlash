namespace TattooFlash.Web.Models
{
    public class TagSaveResult
    {
        public TagModel? Tag { get; set; }
        public bool IsSuccess => Tag is not null;
        public string TagSaveError { get; set; } = string.Empty;

        private TagSaveResult(TagModel? tag, string tagSaveError)
        {
            Tag = tag;
            TagSaveError = tagSaveError;
        }


        public static TagSaveResult Success(TagModel tag)
        {
            ArgumentNullException.ThrowIfNull(tag);
            return new TagSaveResult(tag, string.Empty);
        }

        public static TagSaveResult Failure(string tagSaveError)
        {
            return new TagSaveResult(null, tagSaveError);
        }

    }
}