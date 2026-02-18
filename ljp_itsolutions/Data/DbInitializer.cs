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
                Console.WriteLine("Applying pending migrations...");
                db.Database.Migrate();

                Console.WriteLine("Checking database connection...");
                
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

                var usersToFix = db.Users.ToList();
                bool fixedAny = false;
                foreach (var u in usersToFix)
                {
                    var normalizedRole = u.Role?.Trim();
                    if (string.IsNullOrEmpty(normalizedRole)) continue;

                    if (string.Equals(normalizedRole, UserRoles.Admin, StringComparison.OrdinalIgnoreCase) && normalizedRole != UserRoles.Admin)
                    {
                        u.Role = UserRoles.Admin;
                        fixedAny = true;
                    }
                    else if (string.Equals(normalizedRole, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) && normalizedRole != UserRoles.SuperAdmin)
                    {
                        u.Role = UserRoles.SuperAdmin;
                        fixedAny = true;
                    }
                    else if (string.Equals(normalizedRole, UserRoles.Manager, StringComparison.OrdinalIgnoreCase) && normalizedRole != UserRoles.Manager)
                    {
                        u.Role = UserRoles.Manager;
                        fixedAny = true;
                    }
                    else if (string.Equals(normalizedRole, UserRoles.Cashier, StringComparison.OrdinalIgnoreCase) && normalizedRole != UserRoles.Cashier)
                    {
                        u.Role = UserRoles.Cashier;
                        fixedAny = true;
                    }
                    else if (string.Equals(normalizedRole, UserRoles.MarketingStaff, StringComparison.OrdinalIgnoreCase) && normalizedRole != UserRoles.MarketingStaff)
                    {
                        u.Role = UserRoles.MarketingStaff;
                        fixedAny = true;
                    }
                }
                if (fixedAny)
                {
                    Console.WriteLine("Normalizing user roles to standard casing...");
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

                // Helper to seed or update a user
                void SeedOrUpdateUser(string username, string fullName, string email, string roleName)
                {
                    var user = db.Users.FirstOrDefault(u => u.Username == username);
                    if (user == null)
                    {
                        Console.WriteLine($"Seeding user: {username}");
                        var newUser = new User
                        {
                            UserID = Guid.NewGuid(),
                            Username = username,
                            FullName = fullName,
                            Email = email,
                            Role = roleName,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        newUser.Password = hasher.HashPassword(newUser, "123");
                        db.Users.Add(newUser);
                    }
                    else
                    {
                        // Ensure role and other basic info is correct
                        bool modified = false;
                        if (string.IsNullOrWhiteSpace(user.Role) || !string.Equals(user.Role, roleName, StringComparison.OrdinalIgnoreCase))
                        {
                            user.Role = roleName;
                            modified = true;
                        }

                        // Reset password to '123' ONLY if it's currently null or empty
                        if (string.IsNullOrEmpty(user.Password))
                        {
                            Console.WriteLine($"Setting initial password for: {username}");
                            user.Password = hasher.HashPassword(user, "123");
                            modified = true;
                        }
                        
                        if (modified)
                        {
                            Console.WriteLine($"Updating user data for: {username}");
                            db.Users.Update(user);
                        }
                    }
                }

                SeedOrUpdateUser("superadmin", "System SuperAdmin", "superadmin@coffee.local", UserRoles.SuperAdmin);
                SeedOrUpdateUser("admin", "System Admin", "admin@coffee.local", UserRoles.Admin);
                SeedOrUpdateUser("manager", "Store Manager", "manager@coffee.local", UserRoles.Manager);
                SeedOrUpdateUser("cashier", "Store Cashier", "cashier@coffee.local", UserRoles.Cashier);
                SeedOrUpdateUser("marketing", "Marketing Staff", "marketing@coffee.local", UserRoles.MarketingStaff);

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
