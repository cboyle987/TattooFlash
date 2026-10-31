namespace TattooFlash.Models
{
    public record PaginatedDesignResults
    {
        public int TotalItems { get; set; }
        public List<DesignModel> Designs { get; set; } = [];

        public int PageNumber { get; set; }

        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);

        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
