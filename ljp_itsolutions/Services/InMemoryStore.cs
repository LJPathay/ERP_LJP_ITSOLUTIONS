using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;

namespace ljp_itsolutions.Services
{
    public class InMemoryStore
    {
        public ConcurrentDictionary<Guid, User> Users { get; } = new();
        public ConcurrentDictionary<int, Category> Categories { get; } = new();
        public ConcurrentDictionary<int, Product> Products { get; } = new();
        public ConcurrentDictionary<Guid, Order> Orders { get; } = new();

        public InMemoryStore()
        {
            var hasher = new PasswordHasher<User>();

            // Seed users 
            var admin = new User { UserID = Guid.NewGuid(), Username = "admin", Email = "admin@coffee.local", FullName = "System Admin", Role = UserRoles.Admin };
            admin.Password = hasher.HashPassword(admin, "123");

            var manager = new User { UserID = Guid.NewGuid(), Username = "manager", Email = "manager@coffee.local", FullName = "Store Manager", Role = UserRoles.Manager };
            manager.Password = hasher.HashPassword(manager, "123");

            var cashier = new User { UserID = Guid.NewGuid(), Username = "cashier", Email = "cashier@coffee.local", FullName = "Cashier", Role = UserRoles.Cashier };
            cashier.Password = hasher.HashPassword(cashier, "123");

            var marketing = new User { UserID = Guid.NewGuid(), Username = "marketing", Email = "marketing@coffee.local", FullName = "Marketing", Role = UserRoles.MarketingStaff };
            marketing.Password = hasher.HashPassword(marketing, "123");

            Users[admin.UserID] = admin;
            Users[manager.UserID] = manager;
            Users[cashier.UserID] = cashier;
            Users[marketing.UserID] = marketing;

            // Seed categories
            var catCoffee = new Category { CategoryID = 1, CategoryName = "Coffee" };
            var catTea = new Category { CategoryID = 2, CategoryName = "Tea" };
            var catPastry = new Category { CategoryID = 3, CategoryName = "Pastry" };

            Categories[catCoffee.CategoryID] = catCoffee;
            Categories[catTea.CategoryID] = catTea;
            Categories[catPastry.CategoryID] = catPastry;

            // Seed products
            var p1 = new Product { ProductID = 1, ProductName = "Espresso", Price = 2.5m, StockQuantity = 100, CategoryID = catCoffee.CategoryID, Category = catCoffee };
            var p2 = new Product { ProductID = 2, ProductName = "Latte", Price = 3.5m, StockQuantity = 80, CategoryID = catCoffee.CategoryID, Category = catCoffee };
            var p3 = new Product { ProductID = 3, ProductName = "Cappuccino", Price = 3.75m, StockQuantity = 60, CategoryID = catCoffee.CategoryID, Category = catCoffee };
            var p4 = new Product { ProductID = 4, ProductName = "Green Tea", Price = 3.0m, StockQuantity = 40, CategoryID = catTea.CategoryID, Category = catTea };
            var p5 = new Product { ProductID = 5, ProductName = "Croissant", Price = 2.0m, StockQuantity = 50, CategoryID = catPastry.CategoryID, Category = catPastry };
            var p6 = new Product { ProductID = 6, ProductName = "Muffin", Price = 2.5m, StockQuantity = 45, CategoryID = catPastry.CategoryID, Category = catPastry };
            var p7 = new Product { ProductID = 7, ProductName = "Cheesecake", Price = 4.5m, StockQuantity = 20, CategoryID = catPastry.CategoryID, Category = catPastry };

            Products[p1.ProductID] = p1;
            Products[p2.ProductID] = p2;
            Products[p3.ProductID] = p3;
            Products[p4.ProductID] = p4;
            Products[p5.ProductID] = p5;
            Products[p6.ProductID] = p6;
            Products[p7.ProductID] = p7;
        }
    }
}
