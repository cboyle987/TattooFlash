using Microsoft.EntityFrameworkCore;

namespace TattooFlash.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Models.DesignModel> Designs { get; set; }
        public DbSet<Models.TagModel> Tags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Models.TagModel>()
                .HasIndex(t => t.NormalizedName)
                .IsUnique();
            modelBuilder.Entity<Models.DesignModel>()
                .HasMany(d => d.Tags)
                .WithMany(t => t.Designs)
                .UsingEntity(j => j.ToTable("DesignTags"));
        }
    }
}