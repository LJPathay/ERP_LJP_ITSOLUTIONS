using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly InMemoryStore _store;
        private readonly ljp_itsolutions.Data.ApplicationDbContext _db;
        private readonly IPasswordHasher<ljp_itsolutions.Models.User> _hasher;

        public AdminController(InMemoryStore store, ljp_itsolutions.Data.ApplicationDbContext db, IPasswordHasher<ljp_itsolutions.Models.User> hasher)
        {
            _store = store;
            _db = db;
            _hasher = hasher;
        }

        // GET: /Admin/Users
        public IActionResult Users()
        {
            var users = _db.Users
                .Include(u => u.Role)
                .OrderByDescending(u => u.CreatedAt)
                .ToList();
            
            ViewBag.Roles = _db.Roles.ToList();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User user, string Password)
        {
            if (ModelState.IsValid)
            {
                user.UserID = Guid.NewGuid();
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                user.Password = _hasher.HashPassword(user, Password);
                
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                TempData["Success"] = "User created successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to create user. Please check the inputs.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User updatedUser)
        {
            var user = await _db.Users.FindAsync(updatedUser.UserID);
            if (user == null) return NotFound();

            user.FullName = updatedUser.FullName;
            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            user.RoleID = updatedUser.RoleID;
            user.IsActive = updatedUser.IsActive;

            await _db.SaveChangesAsync();
            TempData["Success"] = "User updated successfully.";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ArchiveUser(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return RedirectToAction("Users");
            var user = await _db.Users.FindAsync(guid);
            if (user != null)
            {
                user.IsActive = false;
                await _db.SaveChangesAsync();
                TempData["Success"] = "User archived successfully.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return RedirectToAction("Users");
            var user = await _db.Users.FindAsync(guid);
            if (user != null)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                TempData["Success"] = "User deleted successfully.";
            }
            return RedirectToAction("Users");
        }

        [HttpGet]
        public async Task<IActionResult> ChangePassword(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return NotFound();
            var user = await _db.Users.FindAsync(guid);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string id, string newPassword)
        {
            if (!Guid.TryParse(id, out var guid)) return NotFound();
            var user = await _db.Users.FindAsync(guid);
            if (user == null) return NotFound();
            user.Password = _hasher.HashPassword(user, newPassword);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Password updated successfully.";
            return RedirectToAction("Users");
        }


        public IActionResult Audit()
        {
            var logs = _db.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .ToList();
            return View("AuditLogs", logs);
        }

        [HttpGet]
        public IActionResult AuditLogs()
        {
            return Audit();
        }
        
        [HttpGet]
        public IActionResult ManageUsers()
        {
            return RedirectToAction("Users");
        }

        // Default index should show the dashboard
        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public IActionResult Reports()
        {
            // Financial Data
            var totalRevenue = _db.Orders
                .Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid")
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            var totalOrders = _db.Orders.Count();
            var activePromotions = _db.Promotions.Count(p => p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);
            
            // Recent Transactions
            var recentTransactions = _db.Orders
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToList();
            
            var model = new
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                ActivePromotions = activePromotions,
                RecentTransactions = recentTransactions
            };
            
            return View(model);
        }

        public IActionResult Reports_Cashier()
        {
            var today = DateTime.Today;
            
            // Daily Sales
            var todaysSales = _db.Orders
                .Where(o => o.OrderDate >= today && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            var yesterdaysSales = _db.Orders
                .Where(o => o.OrderDate >= today.AddDays(-1) && o.OrderDate < today && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;

            // Transactions
            var todaysTransactions = _db.Orders.Count(o => o.OrderDate >= today);
            var avgTransactionValue = todaysTransactions > 0 ? todaysSales / todaysTransactions : 0;
            
            // Top Selling Item Today
            var topItem = _db.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.Order.OrderDate >= today)
                .AsEnumerable() // Client evaluation for grouping if needed, but SQL should handle it
                .GroupBy(od => od.ProductID)
                .Select(g => new { ProductName = g.First().Product.Name, Count = g.Sum(od => od.Quantity) })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            // Recent Cash Transactions
            var recentTransactions = _db.Orders
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .Where(o => o.PaymentMethod == "Cash")
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToList();
                
            // Low Stock Items
            var lowStockItems = _db.Products
                .Where(p => p.StockQuantity < 20)
                .OrderBy(p => p.StockQuantity)
                .Take(5)
                .ToList();

            var model = new
            {
                TodaysSales = todaysSales,
                YesterdaysSales = yesterdaysSales,
                TodaysTransactions = todaysTransactions,
                AvgTransactionValue = avgTransactionValue,
                TopItemName = topItem?.ProductName ?? "N/A",
                TopItemCount = topItem?.Count ?? 0,
                RecentTransactions = recentTransactions,
                LowStockItems = lowStockItems
            };

            return View("Reports_Cashier", model);
        }

        public IActionResult Reports_Marketing()
        {
            // Active Promotions
            var activePromotionsList = _db.Promotions
                .Where(p => p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                .OrderByDescending(p => p.StartDate)
                .ToList();
            
            var activePromotionsCount = activePromotionsList.Count;
            
            // Customer Reach (Total Customers)
            var totalCustomers = _db.Customers.Count();
            
            // New this month (based on first order date since we don't track CreatedAt)
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var newCustomersThisMonth = _db.Customers
                .Where(c => c.Orders.Any())
                .Select(c => new { FirstOrderDate = c.Orders.Min(o => o.OrderDate) })
                .Count(x => x.FirstOrderDate >= startOfMonth);

            var model = new
            {
                ActivePromotions = activePromotionsList,
                ActivePromotionsCount = activePromotionsCount,
                TotalCustomers = totalCustomers,
                NewCustomersThisMonth = newCustomersThisMonth
            };

            return View("Reports_Marketing", model);
        }

        public IActionResult Reports_Manager()
        {
             // Inventory Value
            var inventoryValue = _db.Products.Sum(p => p.Price * p.StockQuantity);
            var lowStockCount = _db.Products.Count(p => p.StockQuantity < 20);
            
            // Financials (Monthly)
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            
            var monthlyRevenue = _db.Orders
                .Where(o => o.OrderDate >= startOfMonth && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
                
            var lastMonthStart = startOfMonth.AddMonths(-1);
            var lastMonthEnd = startOfMonth.AddSeconds(-1);
            
            var lastMonthRevenue = _db.Orders
                .Where(o => o.OrderDate >= lastMonthStart && o.OrderDate <= lastMonthEnd && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            // Staff List
            var staff = _db.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive)
                .OrderBy(u => u.Role.RoleName)
                .Take(10)
                .ToList();

            var model = new
            {
                InventoryValue = inventoryValue,
                LowStockCount = lowStockCount,
                MonthlyRevenue = monthlyRevenue,
                LastMonthRevenue = lastMonthRevenue,
                Staff = staff
            };

            return View("Reports_Manager", model);
        }

        // Admin dashboard showing KPIs
        public IActionResult Dashboard()
        {
            var today = DateTime.Today;
            
            // User Statistics
            var totalUsers = _db.Users.Count();
            var activeUsers = _db.Users.Count(u => u.IsActive);
            
            // Audit Log Statistics
            var totalAuditLogs = _db.AuditLogs.Count();
            var failedLogins = _db.AuditLogs.Count(a => a.Action.Contains("Failed") || a.Action.Contains("failed"));
            
            // Role Distribution
            var roleDistribution = _db.Users
                .Include(u => u.Role)
                .GroupBy(u => u.Role.RoleName)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToList();
            
            // Recent Activity Logs (with full details)
            var recentActivityLogs = _db.AuditLogs
                .Include(a => a.User)
                .ThenInclude(u => u.Role)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToList();

            var model = new
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                TotalAuditLogs = totalAuditLogs,
                FailedLogins = failedLogins,
                RoleDistribution = roleDistribution,
                RecentActivityLogs = recentActivityLogs
            };

            return View(model);
        }

        public IActionResult Transactions()
        {
            var transactions = _db.Orders
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.OrderDate)
                .ToList();
            return View(transactions);
        }

        public IActionResult SystemSettings()
        {
            return View();
        }

        public IActionResult Backups()
        {
            return View();
        }
    }
}
