using Microsoft.EntityFrameworkCore;

namespace ApplePay.Models.Tamara
{
    public sealed class TamaraDbContext : DbContext
    {
        public TamaraDbContext(DbContextOptions<TamaraDbContext> options) : base(options)
        {
        }

        public DbSet<TamaraOrderRecord> TamaraOrders => Set<TamaraOrderRecord>();
        public DbSet<TamaraWebhookEventRecord> TamaraWebhookEvents => Set<TamaraWebhookEventRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TamaraOrderRecord>(entity =>
            {
                entity.ToTable("TamaraOrders", "dbo");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.OrderId)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => x.OrderId)
                    .IsUnique();

                entity.Property(x => x.OrderReferenceId)
                    .HasMaxLength(200);

                entity.Property(x => x.Status)
                    .HasMaxLength(50);

                entity.Property(x => x.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(x => x.Currency)
                    .HasMaxLength(10);

                entity.Property(x => x.RawJson)
                    .HasColumnType("nvarchar(max)");

                entity.Property(x => x.CreatedAt)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(x => x.UpdatedAt)
                    .HasDefaultValueSql("SYSUTCDATETIME()");
            });

            modelBuilder.Entity<TamaraWebhookEventRecord>(entity =>
            {
                entity.ToTable("TamaraWebhookEvents", "dbo");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.OrderId)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.EventType)
                    .HasMaxLength(100);

                entity.Property(x => x.Status)
                    .HasMaxLength(50);

                entity.Property(x => x.RawJson)
                    .HasColumnType("nvarchar(max)");

                entity.Property(x => x.CreatedAt)
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasIndex(x => new { x.OrderId, x.CreatedAt });
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
