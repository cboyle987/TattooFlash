using System.ComponentModel.DataAnnotations;

namespace TattooFlash.Web.Configuration;

public class PaginationConfiguration
{
    [Range(1, int.MaxValue, ErrorMessage = "DefaultPageSize must be a positive integer (at least 1).")]
    public int DefaultPageSize { get; set; }
}