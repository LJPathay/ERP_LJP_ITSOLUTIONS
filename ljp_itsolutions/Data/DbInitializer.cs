using Microsoft.EntityFrameworkCore;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Identity;

namespace ljp_itsolutions.Data
{
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var store = scope.ServiceProvider.GetRequiredService<InMemoryStore>();

            try
            {
                Console.WriteLine("Checking database connection...");
                
                // Ensure SystemSettings table exists (manual fix for migration issues)
                string createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SystemSettings]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[SystemSettings](
                            [SettingKey] [nvarchar](450) NOT NULL,
                            [SettingValue] [nvarchar](max) NOT NULL,
                            CONSTRAINT [PK_SystemSettings] PRIMARY KEY CLUSTERED ([SettingKey] ASC)
                        )
                    END";
                db.Database.ExecuteSqlRaw(createTableSql);

                if (!db.Roles.Any())
                {
                    Console.WriteLine("Seeding roles...");
                    db.Roles.Add(new Role { RoleName = Role.Admin });
                    db.Roles.Add(new Role { RoleName = Role.Manager });
                    db.Roles.Add(new Role { RoleName = Role.Cashier });
                    db.Roles.Add(new Role { RoleName = Role.MarketingStaff });
                    db.SaveChanges();
                }

                // Seed Categories
                if (!db.Categories.Any())
                {
                    Console.WriteLine("Seeding categories...");
                    db.Categories.Add(new Category { CategoryName = "Coffee" });
                    db.Categories.Add(new Category { CategoryName = "Tea" });
                    db.Categories.Add(new Category { CategoryName = "Pastry" });
                    db.SaveChanges();
                }

                // Seed Users
                var hasher = new PasswordHasher<User>();

                // Helper to seed a user if missing
                async Task SeedUser(string username, string fullName, string email, string roleName)
                {
                    if (!db.Users.Any(u => u.Username == username))
                    {
                        var role = db.Roles.FirstOrDefault(r => r.RoleName == roleName);
                        if (role != null)
                        {
                            Console.WriteLine($"Seeding user: {username}");
                            var user = new User
                            {
                                UserID = Guid.NewGuid(),
                                Username = username,
                                FullName = fullName,
                                Email = email,
                                RoleID = role.RoleID,
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            user.Password = hasher.HashPassword(user, "123");
                            db.Users.Add(user);
                        }
                    }
                }

                SeedUser("admin", "System Admin", "admin@coffee.local", Role.Admin).Wait();
                SeedUser("manager", "Store Manager", "manager@coffee.local", Role.Manager).Wait();
                SeedUser("cashier", "Store Cashier", "cashier@coffee.local", Role.Cashier).Wait();
                SeedUser("marketing", "Marketing Staff", "marketing@coffee.local", Role.MarketingStaff).Wait();

                db.SaveChanges();

                // Seed Products
                if (!db.Products.Any())
                {
                    Console.WriteLine("Seeding products...");
                    var coffeeCat = db.Categories.FirstOrDefault(c => c.CategoryName == "Coffee");
                    if (coffeeCat != null)
                    {
                        db.Products.Add(new Product { ProductName = "Espresso", Price = 2.5m, StockQuantity = 100, CategoryID = coffeeCat.CategoryID, IsAvailable = true });
                        db.Products.Add(new Product { ProductName = "Latte", Price = 3.5m, StockQuantity = 80, CategoryID = coffeeCat.CategoryID, IsAvailable = true });
                        db.SaveChanges();
                    }
                }

                Console.WriteLine("Seeding completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Seeding error: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
        }
    }
}
