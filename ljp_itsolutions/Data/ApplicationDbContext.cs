using Microsoft.EntityFrameworkCore;
using ljp_itsolutions.Models;

namespace ljp_itsolutions.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.Property(u => u.Username).IsRequired();
                // Password may be null for legacy rows; treat as optional in model
                b.Property(u => u.Password);
                b.Property(u => u.FullName).IsRequired();
                b.Property(u => u.Role).HasDefaultValue(Role.Admin);
                b.Property(u => u.IsArchived).HasDefaultValue(false);
            });

            builder.Entity<OrderLine>(b =>
            {
                b.HasKey(ol => ol.Id);
                b.HasOne(ol => ol.Order).WithMany(o => o.Lines).HasForeignKey(ol => ol.OrderId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
