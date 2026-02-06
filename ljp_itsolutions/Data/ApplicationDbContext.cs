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
        public DbSet<Role> Roles { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<InventoryLog> InventoryLogs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<User>(b =>
            {
                b.HasKey(u => u.UserID);
                b.Property(u => u.Username).IsRequired().HasMaxLength(50);
                b.Property(u => u.FullName).IsRequired().HasMaxLength(100);
                b.Property(u => u.IsActive).HasDefaultValue(true);
                b.Property(u => u.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            builder.Entity<Order>(b =>
            {
                b.HasKey(o => o.OrderID);
                b.Property(o => o.OrderDate).HasDefaultValueSql("GETDATE()");
                b.HasOne(o => o.Cashier).WithMany().HasForeignKey(o => o.CashierID).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<OrderDetail>(b =>
            {
                b.HasKey(od => od.OrderDetailID);
                b.HasOne(od => od.Order).WithMany(o => o.OrderDetails).HasForeignKey(od => od.OrderID);
                b.Property(od => od.UnitPrice).HasPrecision(18, 2);
                b.Property(od => od.Subtotal).HasPrecision(18, 2);
            });

            builder.Entity<Product>(b =>
            {
                b.Property(p => p.Price).HasPrecision(18, 2);
            });

            builder.Entity<Order>(b =>
            {
                b.Property(o => o.TotalAmount).HasPrecision(18, 2);
                b.Property(o => o.DiscountAmount).HasPrecision(18, 2);
                b.Property(o => o.FinalAmount).HasPrecision(18, 2);
            });

            builder.Entity<Payment>(b =>
            {
                b.Property(p => p.AmountPaid).HasPrecision(18, 2);
            });

            builder.Entity<Promotion>(b =>
            {
                b.Property(p => p.DiscountValue).HasPrecision(18, 2);
            });
        }
    }
}
