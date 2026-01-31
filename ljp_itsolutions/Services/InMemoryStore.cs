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
        public ConcurrentDictionary<Guid, Product> Products { get; } = new();
        public ConcurrentDictionary<Guid, Order> Orders { get; } = new();

        public InMemoryStore()
        {
            var hasher = new PasswordHasher<User>();

            // Seed users 
            var admin = new User { Username = "admin", Email = "admin@coffee.local", FullName = "System Admin", Role = Role.Admin };
            admin.Password = hasher.HashPassword(admin, "123");

            var manager = new User { Username = "manager", Email = "manager@coffee.local", FullName = "Store Manager", Role = Role.Manager };
            manager.Password = hasher.HashPassword(manager, "123");

            var cashier = new User { Username = "cashier", Email = "cashier@coffee.local", FullName = "Cashier", Role = Role.Cashier };
            cashier.Password = hasher.HashPassword(cashier, "123");

            var marketing = new User { Username = "marketing", Email = "marketing@coffee.local", FullName = "Marketing", Role = Role.MarketingStaff };
            marketing.Password = hasher.HashPassword(marketing, "123");

            Users[admin.Id] = admin;
            Users[manager.Id] = manager;
            Users[cashier.Id] = cashier;
            Users[marketing.Id] = marketing;

            // Seed products
            var p1 = new Product { Name = "Espresso", Description = "Strong coffee shot", Price = 2.5m, Stock = 100 };
            var p2 = new Product { Name = "Latte", Description = "Milk with espresso", Price = 3.5m, Stock = 80 };
            var p3 = new Product { Name = "Croissant", Description = "Buttery pastry", Price = 2.0m, Stock = 50 };

            Products[p1.Id] = p1;
            Products[p2.Id] = p2;
            Products[p3.Id] = p3;
        }
    }
}
