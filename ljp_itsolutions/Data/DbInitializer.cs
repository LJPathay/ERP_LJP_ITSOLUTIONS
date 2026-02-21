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
            var strategy = db.Database.CreateExecutionStrategy();

            strategy.Execute(() =>
            {
                try
                {
                    Console.WriteLine("Applying pending migrations...");
                    db.Database.Migrate();

                    Console.WriteLine("Normalizing database schema and records...");
                    
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

                    // Normalize roles
                    var usersToFix = db.Users.ToList();
                    foreach (var u in usersToFix)
                    {
                        var normalizedRole = u.Role?.Trim();
                        if (string.IsNullOrEmpty(normalizedRole)) continue;

                        string? targetRole = null;
                        if (string.Equals(normalizedRole, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.Admin;
                        else if (string.Equals(normalizedRole, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.SuperAdmin;
                        else if (string.Equals(normalizedRole, UserRoles.Manager, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.Manager;
                        else if (string.Equals(normalizedRole, UserRoles.Cashier, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.Cashier;
                        else if (string.Equals(normalizedRole, UserRoles.MarketingStaff, StringComparison.OrdinalIgnoreCase)) targetRole = UserRoles.MarketingStaff;

                        if (targetRole != null && u.Role != targetRole)
                        {
                            u.Role = targetRole;
                        }
                    }

                    // Seed Categories
                    if (!db.Categories.Any())
                    {
                        Console.WriteLine("Seeding categories...");
                        db.Categories.AddRange(
                            new Category { CategoryName = "Coffee" },
                            new Category { CategoryName = "Tea" },
                            new Category { CategoryName = "Pastry" }
                        );
                    }

                    // Seed Users
                    var hasher = new PasswordHasher<User>();
                    var rolesToSeed = new[] { 
                        (Username: "superadmin", FullName: "System SuperAdmin", Email: "superadmin@coffee.local", Role: UserRoles.SuperAdmin),
                        (Username: "admin", FullName: "System Admin", Email: "admin@coffee.local", Role: UserRoles.Admin),
                        (Username: "manager", FullName: "Store Manager", Email: "manager@coffee.local", Role: UserRoles.Manager),
                        (Username: "cashier", FullName: "Store Cashier", Email: "cashier@coffee.local", Role: UserRoles.Cashier),
                        (Username: "marketing", FullName: "Marketing Staff", Email: "marketing@coffee.local", Role: UserRoles.MarketingStaff)
                    };

                    foreach (var s in rolesToSeed)
                    {
                        var user = db.Users.FirstOrDefault(u => u.Username == s.Username);
                        if (user == null)
                        {
                            Console.WriteLine($"Seeding user: {s.Username}");
                            var newUser = new User
                            {
                                UserID = Guid.NewGuid(),
                                Username = s.Username,
                                FullName = s.FullName,
                                Email = s.Email,
                                Role = s.Role,
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            newUser.Password = hasher.HashPassword(newUser, "123");
                            db.Users.Add(newUser);
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(user.Role) || !string.Equals(user.Role, s.Role, StringComparison.OrdinalIgnoreCase))
                            {
                                user.Role = s.Role;
                            }
                            if (string.IsNullOrEmpty(user.Password))
                            {
                                user.Password = hasher.HashPassword(user, "123");
                            }
                        }
                    }

                    db.SaveChanges();

                    // Seed Products
                    if (!db.Products.Any())
                    {
                        Console.WriteLine("Seeding products...");
                        var coffeeCat = db.Categories.FirstOrDefault(c => c.CategoryName == "Coffee");
                        if (coffeeCat != null)
                        {
                            db.Products.AddRange(
                                new Product { ProductName = "Espresso", Price = 2.5m, StockQuantity = 100, CategoryID = coffeeCat.CategoryID, IsAvailable = true },
                                new Product { ProductName = "Latte", Price = 3.5m, StockQuantity = 80, CategoryID = coffeeCat.CategoryID, IsAvailable = true }
                            );
                            db.SaveChanges();
                        }
                    }

                    // Seed Recipe Templates (only once)
                    if (!db.RecipeTemplates.Any())
                    {
                        Console.WriteLine("Seeding recipe templates...");

                        var templates = new List<RecipeTemplate>
                        {
                            // ── COFFEE DRINKS ─────────────────────────────────
                            new() { ProductName = "Espresso", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 18m,  Unit = "g" }
                            }},
                            new() { ProductName = "Americano", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Hot Water",    Quantity = 150m, Unit = "ml" }
                            }},
                            new() { ProductName = "Cappuccino", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",   Quantity = 150m, Unit = "ml" },
                                new() { IngredientName = "Milk Foam",    Quantity = 30m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",   Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Milk Foam",    Quantity = 20m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Caramel Macchiato", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans",  Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",    Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Caramel Syrup", Quantity = 20m,  Unit = "ml" },
                                new() { IngredientName = "Vanilla Syrup", Quantity = 10m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Mocha", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans",    Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",      Quantity = 180m, Unit = "ml" },
                                new() { IngredientName = "Chocolate Syrup", Quantity = 25m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Flat White", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",   Quantity = 160m, Unit = "ml" }
                            }},
                            new() { ProductName = "Vanilla Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans",  Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",    Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Vanilla Syrup", Quantity = 20m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Hazelnut Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans",   Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",     Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Hazelnut Syrup", Quantity = 20m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Spanish Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans",   Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",     Quantity = 180m, Unit = "ml" },
                                new() { IngredientName = "Condensed Milk", Quantity = 30m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Iced Americano", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Water",        Quantity = 120m, Unit = "ml" },
                                new() { IngredientName = "Ice",          Quantity = 100m, Unit = "g"  }
                            }},
                            new() { ProductName = "Iced Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",   Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Ice",          Quantity = 120m, Unit = "g"  }
                            }},
                            new() { ProductName = "Iced Caramel Macchiato", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans",  Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",    Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Caramel Syrup", Quantity = 20m,  Unit = "ml" },
                                new() { IngredientName = "Ice",           Quantity = 120m, Unit = "g"  }
                            }},
                            new() { ProductName = "Iced Mocha", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans",    Quantity = 18m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",      Quantity = 180m, Unit = "ml" },
                                new() { IngredientName = "Chocolate Syrup", Quantity = 25m,  Unit = "ml" },
                                new() { IngredientName = "Ice",             Quantity = 120m, Unit = "g"  }
                            }},
                            new() { ProductName = "Cold Brew Coffee", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Coffee Beans", Quantity = 25m,  Unit = "g"  },
                                new() { IngredientName = "Water",        Quantity = 250m, Unit = "ml" }
                            }},
                            // ── NON-COFFEE DRINKS ─────────────────────────────
                            new() { ProductName = "Hot Chocolate", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Cocoa Powder", Quantity = 25m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",   Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Sugar",        Quantity = 15m,  Unit = "g"  }
                            }},
                            new() { ProductName = "Matcha Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Matcha Powder", Quantity = 15m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",    Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Sugar",         Quantity = 10m,  Unit = "g"  }
                            }},
                            new() { ProductName = "Iced Matcha Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Matcha Powder", Quantity = 15m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",    Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Ice",           Quantity = 120m, Unit = "g"  },
                                new() { IngredientName = "Sugar",         Quantity = 10m,  Unit = "g"  }
                            }},
                            new() { ProductName = "Chai Latte", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Chai Powder", Quantity = 20m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",  Quantity = 200m, Unit = "ml" }
                            }},
                            new() { ProductName = "Iced Chocolate", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Cocoa Powder", Quantity = 25m,  Unit = "g"  },
                                new() { IngredientName = "Fresh Milk",   Quantity = 200m, Unit = "ml" },
                                new() { IngredientName = "Ice",          Quantity = 120m, Unit = "g"  }
                            }},
                            new() { ProductName = "Strawberry Milk", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Strawberry Syrup", Quantity = 30m,  Unit = "ml" },
                                new() { IngredientName = "Fresh Milk",       Quantity = 200m, Unit = "ml" }
                            }},
                            new() { ProductName = "Vanilla Milkshake", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Fresh Milk",    Quantity = 200m, Unit = "ml"    },
                                new() { IngredientName = "Vanilla Syrup", Quantity = 20m,  Unit = "ml"    },
                                new() { IngredientName = "Ice Cream",     Quantity = 1m,   Unit = "scoop" }
                            }},
                            new() { ProductName = "Cookies and Cream Milkshake", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Fresh Milk",      Quantity = 200m, Unit = "ml"    },
                                new() { IngredientName = "Ice Cream",       Quantity = 1m,   Unit = "scoop" },
                                new() { IngredientName = "Crushed Cookies", Quantity = 30m,  Unit = "g"     }
                            }},
                            new() { ProductName = "Mango Smoothie", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Mango Puree",  Quantity = 150m, Unit = "ml" },
                                new() { IngredientName = "Ice",          Quantity = 120m, Unit = "g"  },
                                new() { IngredientName = "Sugar Syrup",  Quantity = 15m,  Unit = "ml" }
                            }},
                            new() { ProductName = "Strawberry Smoothie", Ingredients = new List<RecipeTemplateIngredient> {
                                new() { IngredientName = "Strawberry Puree", Quantity = 150m, Unit = "ml" },
                                new() { IngredientName = "Ice",              Quantity = 120m, Unit = "g"  },
                                new() { IngredientName = "Sugar Syrup",      Quantity = 15m,  Unit = "ml" }
                            }},
                        };

                        db.RecipeTemplates.AddRange(templates);
                        db.SaveChanges();
                        Console.WriteLine($"Seeded {templates.Count} recipe templates.");
                    }

                    Console.WriteLine("Database initialization completed.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Seeding error: {ex.Message}");
                    if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
                    throw; // Rethrow to let the strategy handle retries
                }
            });
        }
    }
}
