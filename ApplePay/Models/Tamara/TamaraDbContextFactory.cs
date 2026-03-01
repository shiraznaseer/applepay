using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ApplePay.Models.Tamara
{
    public sealed class TamaraDbContextFactory : IDesignTimeDbContextFactory<TamaraDbContext>
    {
        public TamaraDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            if (!File.Exists(Path.Combine(basePath, "appsettings.json"))
                && Directory.Exists(Path.Combine(basePath, "ApplePay"))
                && File.Exists(Path.Combine(basePath, "ApplePay", "appsettings.json")))
            {
                basePath = Path.Combine(basePath, "ApplePay");
            }

            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var configured = config["Tamara:DbConnectionString"];
            var connectionString =
                !string.IsNullOrWhiteSpace(configured)
                    ? configured
                    : config.GetConnectionString("Tamara")
                      ?? config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = "Server=(localdb)\\mssqllocaldb;Database=TamaraHistory;Trusted_Connection=True;TrustServerCertificate=True";
            }

            var optionsBuilder = new DbContextOptionsBuilder<TamaraDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new TamaraDbContext(optionsBuilder.Options);
        }
    }
}
