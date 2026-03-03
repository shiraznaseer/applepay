using Microsoft.EntityFrameworkCore;

namespace ApplePay.Models.Unipal
{
    public sealed class UnipalDbContext : DbContext
    {
        public UnipalDbContext(DbContextOptions<UnipalDbContext> options) : base(options)
        {
        }

        public DbSet<UnipalPayment> Payments { get; set; }
        public DbSet<UnipalWebhookEvent> WebhookEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UnipalPayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PaymentId).IsUnique();
                entity.HasIndex(e => e.OrderReferenceId);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<UnipalWebhookEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PaymentId);
                entity.Property(e => e.RawBody).HasColumnType("nvarchar(max)");
            });
        }
    }
}
