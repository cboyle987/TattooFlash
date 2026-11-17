using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TattooFlash.Web.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var cofigurationBuilder = new ConfigurationBuilder()
                .AddUserSecrets<AppDbContextFactory>()
                .AddEnvironmentVariables();
            var config = cofigurationBuilder.Build();

            var connectionString = config.GetConnectionString("DefaultConnection") ?? "Host=localhost;Port=5432;Database=tattooflash_design_time;Username=design_time;Password=not-a-real-password";

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}