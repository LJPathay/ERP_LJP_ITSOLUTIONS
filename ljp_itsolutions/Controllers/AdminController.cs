using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        private async Task LogAudit(string action, Guid? userId = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Action = action,
                    Timestamp = DateTime.Now,
                    UserID = userId ?? (User.Identity.IsAuthenticated ? GetCurrentUserId() : null)
                };
                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Fail silently for audit logs if something goes wrong
            }
        }

        private Guid? GetCurrentUserId()
        {
            var username = User.Identity.Name;
            if (string.IsNullOrEmpty(username)) return null;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            return user?.UserID;
        }

        // GET: /Admin/Users
        public IActionResult Users(bool showArchived = false)
        {
            var query = _db.Users.AsQueryable();

            if (showArchived)
            {
                query = query.Where(u => !u.IsActive);
            }
            else
            {
                query = query.Where(u => u.IsActive);
            }

            var users = query
                .Include(u => u.Role)
                .OrderByDescending(u => u.CreatedAt)
                .ToList();
            
            ViewBag.Roles = _db.Roles.ToList();
            ViewBag.ShowArchived = showArchived;
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User user, string Password)
        {
            if (ModelState.IsValid)
            {
                if (_db.Users.Any(u => u.Username == user.Username))
                {
                    TempData["Error"] = "Username already exists.";
                    return RedirectToAction("Users");
                }

                user.UserID = Guid.NewGuid();
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                user.Password = _hasher.HashPassword(user, Password);
                
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                
                await LogAudit($"Created user: {user.Username}", user.UserID);

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

            // Only update password if provided? The original code didn't hold password update here.
            
            await _db.SaveChangesAsync();
            await LogAudit($"Updated user: {user.Username}", user.UserID);

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
                await LogAudit($"Archived user: {user.Username}", user.UserID);
                TempData["Success"] = "User archived successfully.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> RestoreUser(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return RedirectToAction("Users");
            var user = await _db.Users.FindAsync(guid);
            if (user != null)
            {
                user.IsActive = true;
                await _db.SaveChangesAsync();
                await LogAudit($"Restored user: {user.Username}", user.UserID);
                TempData["Success"] = "User restored successfully.";
            }
            return RedirectToAction("Users", new { showArchived = true }); // Keep them on archived view to see result
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
                await LogAudit($"Deleted user: {user.Username}");
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
            await LogAudit($"Changed password for user: {user.Username}", user.UserID);
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

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public IActionResult Reports()
        {
            var totalRevenue = _db.Orders
                .Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid")
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            var totalOrders = _db.Orders.Count();
            var activePromotions = _db.Promotions.Count(p => p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);
            
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
            
            var todaysSales = _db.Orders
                .Where(o => o.OrderDate >= today && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            var yesterdaysSales = _db.Orders
                .Where(o => o.OrderDate >= today.AddDays(-1) && o.OrderDate < today && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;

            var todaysTransactions = _db.Orders.Count(o => o.OrderDate >= today);
            var avgTransactionValue = todaysTransactions > 0 ? todaysSales / todaysTransactions : 0;
            
            var topItem = _db.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.Order.OrderDate >= today)
                .AsEnumerable()
                .GroupBy(od => od.ProductID)
                .Select(g => new { ProductName = g.First().Product.Name, Count = g.Sum(od => od.Quantity) })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            var recentTransactions = _db.Orders
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .Where(o => o.PaymentMethod == "Cash")
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToList();
                
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
            var activePromotionsList = _db.Promotions
                .Where(p => p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                .OrderByDescending(p => p.StartDate)
                .ToList();
            
            var activePromotionsCount = activePromotionsList.Count;
            
            var totalCustomers = _db.Customers.Count();
            
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
            var inventoryValue = _db.Products.Sum(p => p.Price * p.StockQuantity);
            var lowStockCount = _db.Products.Count(p => p.StockQuantity < 20);
            
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

        public IActionResult Dashboard()
        {
            var today = DateTime.Today;
            
            var totalUsers = _db.Users.Count();
            var activeUsers = _db.Users.Count(u => u.IsActive);
            
            var totalAuditLogs = _db.AuditLogs.Count();
            var failedLogins = _db.AuditLogs.Count(a => a.Action.Contains("Failed") || a.Action.Contains("failed"));
            
            // Sales Trend Logic
            var last7Days = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();
            var trendLabels = new List<string>();
            var trendData = new List<decimal>();
            
            var weekStart = today.AddDays(-6);
            var weekEnd = today.AddDays(1);
            
            var orders = _db.Orders
                .Where(o => o.OrderDate >= weekStart && o.OrderDate < weekEnd && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid"))
                .Select(o => new { o.OrderDate, o.FinalAmount })
                .ToList();

            foreach (var day in last7Days)
            {
                trendLabels.Add(day.ToString("MMM dd"));
                var dailySum = orders.Where(o => o.OrderDate.Date == day).Sum(o => o.FinalAmount);
                trendData.Add(dailySum);
            }
            
            var roleDistribution = _db.Users
                .Include(u => u.Role)
                .GroupBy(u => u.Role.RoleName)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToList();
            
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
                SalesTrendLabels = trendLabels,
                SalesTrendData = trendData,
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
            var settings = _db.SystemSettings.ToDictionary(s => s.SettingKey, s => s.SettingValue);
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(IFormCollection form)
        {
            try 
            {
                var keys = new[] { 
                    "SystemName", "Timezone", "Currency", "DateFormat", 
                    "SessionTimeout", "PasswordMinLength", "RequireSpecialChars", "RequireNumbers", "TwoFactorAuth",
                    "CompanyName", "TaxRate", "LowStockThreshold", "BusinessHourStart", "BusinessHourEnd",
                    "SmtpServer", "SmtpPort", "EmailNotifications", "LowStockAlerts", "DailyReports"
                };

                foreach (var key in keys)
                {
                    string value = form.ContainsKey(key) ? form[key].ToString() : "";
                    
                    // Handle unchecked checkboxes
                    bool isCheckbox = key.Contains("Require") || key.Contains("TwoFactor") || key.Contains("Notifications") || key.Contains("Alerts") || key.Contains("DailyReports");
                    if (isCheckbox)
                    {
                        value = (!string.IsNullOrEmpty(value) && (value.ToLower() == "true" || value.ToLower() == "on")) ? "true" : "false";
                    }

                    var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                    if (setting == null)
                    {
                        setting = new SystemSetting { SettingKey = key, SettingValue = value };
                        _db.SystemSettings.Add(setting);
                    }
                    else
                    {
                        setting.SettingValue = value;
                    }
                }
                
                await _db.SaveChangesAsync();
                await LogAudit("Updated system settings");
                TempData["Success"] = "System settings updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating settings: {ex.Message}";
            }
            return RedirectToAction("SystemSettings");
        }

        public IActionResult Backups()
        {
            var backupPath = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
            if (!Directory.Exists(backupPath)) Directory.CreateDirectory(backupPath);
            
            var files = Directory.GetFiles(backupPath, "*.json")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.CreationTime)
                                 .ToList();
                                 
            return View(files);
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateBackup()
        {
            try
            {
                var backupPath = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
                if (!Directory.Exists(backupPath)) Directory.CreateDirectory(backupPath);
                
                var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var fullPath = Path.Combine(backupPath, fileName);
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles 
                };

                // Fetch data - using AsNoTracking to avoid tracking overhead
                // Warning: fetching all data might be heavy for large DBs, but okay for this scope
                var users = await _db.Users.AsNoTracking().Include(u => u.Role).ToListAsync();
                var products = await _db.Products.AsNoTracking().ToListAsync();
                var orders = await _db.Orders.AsNoTracking().Include(o => o.OrderDetails).ToListAsync();
                var settings = await _db.SystemSettings.AsNoTracking().ToListAsync();
                
                var backupData = new {
                    GeneratedAt = DateTime.Now,
                    Users = users,
                    Products = products,
                    Orders = orders,
                    Settings = settings
                };
                
                var json = JsonSerializer.Serialize(backupData, options);
                
                await System.IO.File.WriteAllTextAsync(fullPath, json);
                await LogAudit("Created system backup");
                
                TempData["Success"] = "Backup created successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Backup failed: {ex.Message}";
            }
            return RedirectToAction("Backups");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBackup(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return RedirectToAction("Backups");
            
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups", fileName);
            if (System.IO.File.Exists(path))
            {
                try 
                {
                    System.IO.File.Delete(path);
                    await LogAudit($"Deleted backup: {fileName}");
                    TempData["Success"] = "Backup deleted successfully.";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error deleting backup: {ex.Message}";
                }
            }
            else
            {
                TempData["Error"] = "Backup file not found.";
            }
            return RedirectToAction("Backups");
        }
    }
}
