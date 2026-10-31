namespace TattooFlash.Web.Models
{
    public class DesignModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public int Price { get; set; }
        public string ImageName { get; set; } = string.Empty;

        public bool IsAvailable { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }


    }
}
