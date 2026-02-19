using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using Microsoft.EntityFrameworkCore;

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
            var products = await _db.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .ToListAsync();
            return View(products);
        }

        public IActionResult CreateOrder()
        {
            return RedirectToAction("Index");
        }
    }
}
