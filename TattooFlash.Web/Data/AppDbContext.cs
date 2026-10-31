using Microsoft.EntityFrameworkCore;

namespace TattooFlash.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Models.DesignModel> Designs { get; set; }
    }
}
