namespace TattooFlash.Models
{
    public class DesignModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public int Price { get; set; }
        public string ImageName { get; set; }

        public bool IsAvailable { get; set; }


    }
}
