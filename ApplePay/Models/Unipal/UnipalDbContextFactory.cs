using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApplePay.Models.Unipal
{
    public sealed class UnipalDbContextFactory : IDesignTimeDbContextFactory<UnipalDbContext>
    {
        public UnipalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UnipalDbContext>();
            optionsBuilder.UseSqlServer("Server=UTILITIES\\SQLEXPRESS;Database=Unipal;User Id=softsol1_Tap;password=775RAxUz[<B&;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Integrated Security=false");

            return new UnipalDbContext(optionsBuilder.Options);
        }
    }
}
