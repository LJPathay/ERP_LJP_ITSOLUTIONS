using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;

using Microsoft.AspNetCore.Authorization;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerController : Controller
    {
        private readonly InMemoryStore _store;

        public ManagerController(InMemoryStore store)
        {
            _store = store;
        }

        public IActionResult Dashboard()
        {
            // Basic KPIs
            var model = new
            {
                TotalProducts = _store.Products.Count,
                TotalUsers = _store.Users.Count,
                TotalOrders = _store.Orders.Count
            };
            return View(model);
        }

        public IActionResult Products()
        {
            return View(_store.Products.Values);
        }

        [HttpPost]
        public IActionResult UpdateStock(int id, int stock)
        {
            if (_store.Products.TryGetValue(id, out var p))
            {
                p.StockQuantity = stock;
            }
            return RedirectToAction("Products");
        }

        public IActionResult Reports()
        {
            return View();
        }
    }
}
