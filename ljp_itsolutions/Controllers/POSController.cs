using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Cashier,Admin,Manager,SuperAdmin")]
    public class POSController : Controller
    {
        private readonly ljp_itsolutions.Data.ApplicationDbContext _db;

        public POSController(ljp_itsolutions.Data.ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Challenge();

            var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == userId && !s.IsClosed);
            if (currentShift == null)
            {
                TempData["ErrorMessage"] = "No open shift found. Please start a shift first.";
                return RedirectToAction("ShiftManagement", "Cashier");
            }

            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                .Where(p => p.IsAvailable)
                .ToListAsync();

            var availableProducts = products.Where(p => {
                if (p.ProductRecipes != null && p.ProductRecipes.Any())
                {
                    return p.ProductRecipes.All(pr => pr.Ingredient.StockQuantity >= pr.QuantityRequired);
                }
                return p.StockQuantity > 0;
            }).ToList();

            return View(availableProducts);
        }

        public IActionResult CreateOrder()
        {
            return RedirectToAction("Index");
        }
    }
}
