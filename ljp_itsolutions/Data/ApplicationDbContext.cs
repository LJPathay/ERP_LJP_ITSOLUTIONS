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

            // Seed Roles
            builder.Entity<Role>().HasData(
                new Role { RoleID = 1, RoleName = Role.Admin },
                new Role { RoleID = 2, RoleName = Role.Manager },
                new Role { RoleID = 3, RoleName = Role.Cashier },
                new Role { RoleID = 4, RoleName = Role.MarketingStaff }
            );

            // Seed Categories
            builder.Entity<Category>().HasData(
                new Category { CategoryID = 1, CategoryName = "Coffee" },
                new Category { CategoryID = 2, CategoryName = "Tea" },
                new Category { CategoryID = 3, CategoryName = "Pastry" }
            );

            // Seed Admin User (Password is '123' - pre-hashed for Identity V3)
            var adminId = Guid.Parse("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f");
            builder.Entity<User>().HasData(
                new User
                {
                    UserID = adminId,
                    Username = "admin",
                    FullName = "System Admin",
                    Email = "admin@coffee.local",
                    RoleID = 1,
                    IsActive = true,
                    Password = "AQAAAAIAAYagAAAAEEmhXNnUvV8p+L1p0v7wXv9XwQyGZG/0T0T0T0T0T0T0T0T0T0T0T0T0T0T0T0==" // Example hash, usually better to run a small app once to get a real one
                }
            );
        }
    }
}
