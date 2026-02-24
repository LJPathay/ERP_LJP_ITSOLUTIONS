using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")] 
    public class AdminController : BaseController
    {
        private readonly InMemoryStore _store;
        private readonly IPasswordHasher<ljp_itsolutions.Models.User> _hasher;
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;

        public AdminController(InMemoryStore store, ljp_itsolutions.Data.ApplicationDbContext db, IPasswordHasher<ljp_itsolutions.Models.User> hasher, IReceiptService receiptService, IServiceScopeFactory scopeFactory)
            : base(db)
        {
            _store = store;
            _hasher = hasher;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
        }



        [HttpGet]
        public IActionResult Users(bool showArchived = false)
        {
            var query = _db.Users.AsQueryable();
            
            query = query.Where(u => u.Role != "SuperAdmin");

            if (showArchived)
                query = query.Where(u => !u.IsActive);
            else
                query = query.Where(u => u.IsActive);

            var users = query.OrderByDescending(u => u.CreatedAt).ToList();
            ViewBag.Roles = new List<string> { 
                UserRoles.Admin, 
                UserRoles.Manager, 
                UserRoles.Cashier, 
                UserRoles.MarketingStaff 
            };
            ViewBag.ShowArchived = showArchived;
            return View(users);
        }

        [HttpGet]
        public IActionResult ManageUsers() => RedirectToAction("Users");

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public IActionResult Reports()
        {
            var totalRevenue = _db.Orders
                .Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)")
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            var totalOrders = _db.Orders.Count();
            var activePromotions = _db.Promotions.Count(p => p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow);
            
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
                .Where(o => o.OrderDate >= today && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            var yesterdaysSales = _db.Orders
                .Where(o => o.OrderDate >= today.AddDays(-1) && o.OrderDate < today && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
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

            var dayStart = DateTime.Today;
            var hourlyData = _db.Orders
                .Where(o => o.OrderDate >= dayStart)
                .GroupBy(o => o.OrderDate.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToList();

            var hourlyLabels = new List<string>();
            var hourlyValues = new List<int>();

            for (int i = 0; i < 24; i++)
            {
                hourlyLabels.Add(i < 12 ? $"{i}am" : (i == 12 ? "12pm" : $"{i-12}pm"));
                var count = hourlyData.FirstOrDefault(h => h.Hour == i)?.Count ?? 0;
                hourlyValues.Add(count);
            }

            var model = new
            {
                TodaysSales = todaysSales,
                YesterdaysSales = yesterdaysSales,
                TodaysTransactions = todaysTransactions,
                AvgTransactionValue = avgTransactionValue,
                TopItemName = topItem?.ProductName ?? "N/A",
                TopItemCount = topItem?.Count ?? 0,
                RecentTransactions = recentTransactions,
                LowStockItems = lowStockItems,
                HourlyLabels = hourlyLabels,
                HourlyData = hourlyValues
            };

            return View("Reports_Cashier", model);
        }

        public IActionResult Reports_Marketing()
        {
            var activePromotionsList = _db.Promotions
                .Where(p => p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
                .OrderByDescending(p => p.StartDate)
                .ToList();
            
            var activePromotionsCount = activePromotionsList.Count;
            
            var totalCustomers = _db.Customers.Count();
            
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
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
            var inventoryValue = _db.Expenses
                .Where(e => e.Category == "Supplies")
                .Sum(e => e.Amount);
            var lowStockCount = _db.Products.Count(p => p.StockQuantity < 20);
            
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            
            var monthlyRevenue = _db.Orders
                .Where(o => o.OrderDate >= startOfMonth && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
                
            var lastMonthStart = startOfMonth.AddMonths(-1);
            var lastMonthEnd = startOfMonth.AddSeconds(-1);
            
            var lastMonthRevenue = _db.Orders
                .Where(o => o.OrderDate >= lastMonthStart && o.OrderDate <= lastMonthEnd && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .Sum(o => (decimal?)o.FinalAmount) ?? 0;
            
            var staff = _db.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Role)
                .Take(10)
                .ToList();

            var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-29);
            var dailyRevenue = _db.Orders
                .Where(o => o.OrderDate >= thirtyDaysAgo && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(o => o.FinalAmount) })
                .ToList();

            var revenueLabels = new List<string>();
            var revenueValues = new List<decimal>();

            for (int i = 0; i < 30; i++)
            {
                var date = thirtyDaysAgo.AddDays(i);
                revenueLabels.Add(date.ToString("MMM dd"));
                var total = dailyRevenue.FirstOrDefault(d => d.Date == date)?.Total ?? 0;
                revenueValues.Add(total);
            }

            var categoryData = _db.OrderDetails
                .Include(od => od.Product)
                .ThenInclude(p => p.Category)
                .Where(od => od.Order.OrderDate >= thirtyDaysAgo)
                .GroupBy(od => od.Product.Category.CategoryName)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();

            var model = new
            {
                InventoryValue = inventoryValue,
                LowStockCount = lowStockCount,
                MonthlyRevenue = monthlyRevenue,
                LastMonthRevenue = lastMonthRevenue,
                Staff = staff,
                RevenueLabels = revenueLabels,
                RevenueData = revenueValues,
                CategoryLabels = categoryData.Select(c => c.Category).ToList(),
                CategoryData = categoryData.Select(c => c.Count).ToList()
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
            // Sales trend logic
            var last7Days = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();
            var trendLabels = new List<string>();
            var trendData = new List<decimal>();
            
            var weekStart = today.AddDays(-6);
            var weekEnd = today.AddDays(1);
            
            var orders = _db.Orders
                .Where(o => o.OrderDate >= weekStart && o.OrderDate < weekEnd && (o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)"))
                .Select(o => new { o.OrderDate, o.FinalAmount, o.PaymentStatus })
                .ToList();

            foreach (var day in last7Days)
            {
                trendLabels.Add(day.ToString("MMM dd"));
                var dailySum = orders.Where(o => o.OrderDate.Date == day && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).Sum(o => o.FinalAmount);
                trendData.Add(dailySum);
            }
            
            var roleDistribution = _db.Users
                .GroupBy(u => u.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToList();
            
            var recentActivityLogs = _db.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToList();

            var productCount = _db.Products.Count();
            var todayTransactions = _db.Orders.Count(o => o.OrderDate.Date == today);

            var model = new
            {
                TotalProducts = productCount,
                TodayTransactions = todayTransactions,
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

        public IActionResult InventoryOverview()
        {
            var ingredients = _db.Ingredients
                .OrderBy(i => i.Name)
                .ToList();

            var lowStockCount = ingredients.Count(i => i.StockQuantity < i.LowStockThreshold && i.StockQuantity > 0);
            var outOfStockCount = ingredients.Count(i => i.StockQuantity <= 0);
    
            ViewBag.LowStockCount = lowStockCount;
            ViewBag.OutOfStockCount = outOfStockCount;
            ViewBag.TotalCount = ingredients.Count;

            return View(ingredients);
        }

        public IActionResult AuditLogs()
        {
            var logs = _db.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(100)
                .ToList();
            return View(logs);
        }

        // API Endpoints for Modals
        [HttpGet]
        public async Task<IActionResult> GetUser(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();
            
            var user = await _db.Users
                .Where(u => u.UserID.ToString() == id)
                .Select(u => new 
                {
                    u.UserID,
                    u.Username,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt,
                    u.ProfilePictureUrl
                })
                .FirstOrDefaultAsync();
                
            if (user == null) return NotFound();
            return Json(user);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(Guid id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .Where(o => o.OrderID == id)
                .Select(o => new
                {
                    o.OrderID,
                    OrderDate = o.OrderDate.ToString("MMM dd, yyyy HH:mm"),
                    CustomerName = o.Customer != null ? o.Customer.FullName : "Walk-in Customer",
                    CashierName = o.Cashier != null ? o.Cashier.FullName : "N/A",
                    o.PaymentMethod,
                    o.PaymentStatus,
                    TotalAmount = o.FinalAmount,
                    Items = o.OrderDetails.Select(od => new
                    {
                        ProductName = od.Product.Name,
                        od.Quantity,
                        od.UnitPrice,
                        Total = od.Quantity * od.UnitPrice
                    })
                })
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();
            return Json(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Username))
                return BadRequest("Username is required.");

            if (_db.Users.Any(u => u.Username == user.Username))
                return BadRequest("Username already exists.");

            // SECURITY: Prevent creating a SuperAdmin from this endpoint
            if (user.Role == UserRoles.SuperAdmin)
                return Forbid("Admin cannot create a SuperAdmin account.");

            user.UserID = Guid.NewGuid();
            
            var token = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(2);
            user.Password = "LOCKED_INITIALLY"; 

            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(user.Email))
            {
                var inviteLink = Url.Action("ResetPassword", "Account", new { userId = user.UserID, token = token }, protocol: Request.Scheme) ?? "";
                // Send welcome email invite in background
                _ = Task.Run(async () => {
                    try {
                        using (var scope = _scopeFactory.CreateScope()) {
                            var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                            await scopedReceiptService.SendStaffInviteAsync(user, inviteLink);
                        }
                    } catch (Exception ex) {
                        // Log locally but don't block
                        Console.WriteLine($"[Email Failure]: {ex.Message}");
                    }
                });
            }

            await LogAudit("Created User & Sent Invite: " + user.Username, null, user.UserID);

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser([FromBody] JsonElement data)
        {
            try
            {
                var jsonId = data.GetProperty("UserID").GetString();
                if (string.IsNullOrEmpty(jsonId)) return BadRequest("Invalid UserID");
                var id = Guid.Parse(jsonId);
                var existingUser = await _db.Users.FindAsync(id);
                if (existingUser == null) return NotFound();

                if (existingUser.Role == "SuperAdmin") return Forbid();

                existingUser.FullName = data.GetProperty("FullName").GetString() ?? existingUser.FullName;
                existingUser.Email = data.GetProperty("Email").GetString() ?? existingUser.Email;
                
                string newRole = data.GetProperty("Role").GetString() ?? existingUser.Role;
                // SECURITY: Prevent promotion to SuperAdmin
                if (newRole == UserRoles.SuperAdmin && existingUser.Role != UserRoles.SuperAdmin)
                {
                    return Forbid("Cannot promote a user to SuperAdmin.");
                }
                existingUser.Role = newRole;

                _db.Users.Update(existingUser);
                await _db.SaveChangesAsync();
                await LogAudit("Updated User: " + existingUser.Username, null, existingUser.UserID);

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(Guid id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role == "SuperAdmin") return Forbid();

            user.IsActive = !user.IsActive;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
            await LogAudit((user.IsActive ? "Restored" : "Archived") + " User: " + user.Username, null, user.UserID);

            return Ok();
        }
        // Inventory Actions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIngredient([FromBody] Ingredient ingredient)
        {
            if (string.IsNullOrEmpty(ingredient.Name)) return BadRequest("Name is required.");

            _db.Ingredients.Add(ingredient);
            await _db.SaveChangesAsync();

            if (ingredient.StockQuantity > 0)
            {
                ingredient.LastStockedDate = DateTime.UtcNow;
                _db.InventoryLogs.Add(new InventoryLog
                {
                    IngredientID = ingredient.IngredientID,
                    QuantityChange = ingredient.StockQuantity,
                    ChangeType = "Initial",
                    LogDate = DateTime.UtcNow,
                    Remarks = "Initial stock upon ingredient creation"
                });
                await _db.SaveChangesAsync();
            }

            await LogAudit("Added Ingredient: " + ingredient.Name);
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IntakeStock(int IngredientID, decimal Quantity, string Remarks, DateTime? IntakeDate, DateTime? ExpiryDate)
        {
            var ingredient = await _db.Ingredients.FindAsync(IngredientID);
            if (ingredient == null) return NotFound();

            if (Quantity <= 0)
            {
                TempData["ErrorMessage"] = "Intake quantity must be greater than zero.";
                return RedirectToAction("InventoryOverview");
            }

            var actualDate = IntakeDate ?? DateTime.UtcNow;

            ingredient.StockQuantity += Quantity;
            ingredient.LastStockedDate = actualDate;

            if (ExpiryDate.HasValue)
            {
                ingredient.ExpiryDate = ExpiryDate.Value;
            }

            string logRemarks = string.IsNullOrEmpty(Remarks) ? "Manual stock intake (Admin)" : Remarks;
            if (ExpiryDate.HasValue)
            {
                logRemarks += $" (Expiry: {ExpiryDate.Value:yyyy-MM-dd})";
            }

            _db.InventoryLogs.Add(new InventoryLog
            {
                IngredientID = IngredientID,
                QuantityChange = Quantity,
                ChangeType = "Intake",
                LogDate = actualDate,
                Remarks = logRemarks
            });

            await _db.SaveChangesAsync();
            await LogAudit($"Stock Intake (Admin): Added {Quantity} {ingredient.Unit} to {ingredient.Name} on {actualDate:yyyy-MM-dd}");
            
            TempData["SuccessMessage"] = $"Stock updated! Added {Quantity} {ingredient.Unit} to {ingredient.Name}.";
            return RedirectToAction("InventoryOverview");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(int id, decimal quantity, decimal? threshold, DateTime? expiryDate, string remarks)
        {
            var ingredient = await _db.Ingredients.FindAsync(id);
            if (ingredient == null) return NotFound();

            var diff = quantity - ingredient.StockQuantity;
            bool changed = false;

            if (Math.Abs(diff) > 0.0001m)
            {
                if (diff > 0) ingredient.LastStockedDate = DateTime.UtcNow;

                _db.InventoryLogs.Add(new InventoryLog
                {
                    IngredientID = ingredient.IngredientID,
                    QuantityChange = diff,
                    ChangeType = diff > 0 ? "Adjustment (Add)" : "Adjustment (Remove)",
                    LogDate = DateTime.UtcNow,
                    Remarks = string.IsNullOrEmpty(remarks) ? "Manual adjustment via Admin" : remarks
                });
                ingredient.StockQuantity = quantity;
                changed = true;
            }

            if (threshold.HasValue && ingredient.LowStockThreshold != threshold.Value)
            {
                ingredient.LowStockThreshold = threshold.Value;
                changed = true;
            }

            if (expiryDate != ingredient.ExpiryDate)
            {
                ingredient.ExpiryDate = expiryDate;
                changed = true;
            }

            if (changed)
            {
                _db.Ingredients.Update(ingredient);
                await _db.SaveChangesAsync();
                await LogAudit($"Stock Update (Admin): {ingredient.Name} updated. Qty: {quantity}, Threshold: {threshold}, Expiry: {expiryDate:yyyy-MM-dd}");
            }

            return Ok();
        }
    }
}
