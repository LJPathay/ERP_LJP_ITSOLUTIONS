using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

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

        // GET: /Admin/Users (lists Identity users)
        public IActionResult Users()
        {
            var users = _db.Users.ToList();
            return View(users);
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
            TempData["Message"] = "Password updated";
            return RedirectToAction("Users");
        }

        // Simple audit log placeholder
        public IActionResult Audit()
        {
            // stubbed data for audit
            var logs = new[] { "System started", "User admin created" };
            return View("AuditLogs", logs);
        }

        // Backward-compatible route: /Admin/AuditLogs -> Audit()
        [HttpGet]
        public IActionResult AuditLogs()
        {
            return Audit();
        }

        // Backward-compatible route: /Admin/ManageUsers -> Users()
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
            return View();
        }

        public IActionResult Reports_Cashier()
        {
            return View("Reports_Cashier");
        }

        public IActionResult Reports_Marketing()
        {
            return View("Reports_Marketing");
        }

        public IActionResult Reports_Manager()
        {
            return View("Reports_Manager");
        }

        // Admin dashboard showing KPIs
        public IActionResult Dashboard()
        {
            var totalRevenue = _store.Orders.Values.Sum(o => o.FinalAmount);
            var ordersToday = _store.Orders.Values.Count(o => o.OrderDate.Date == DateTime.Today);
            var activeCustomers = _store.Users.Values.Count(u => u.IsActive);
            var lowStockItems = _store.Products.Values.Count(p => p.StockQuantity < 10);

            var model = new
            {
                TotalRevenue = totalRevenue,
                OrdersToday = ordersToday,
                ActiveCustomers = activeCustomers,
                LowStockItems = lowStockItems,
                RecentActivity = new[]
                {
                    "New order #1234 placed",
                    "Customer feedback received - 5 stars",
                    "Inventory alert: Coffee beans running low",
                    "New customer registered: John Doe"
                }
            };

            return View(model);
        }

        public IActionResult Transactions()
        {
            return View();
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
