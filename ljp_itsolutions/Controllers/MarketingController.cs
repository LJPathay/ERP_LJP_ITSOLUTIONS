using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Authorization;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "MarketingStaff,Admin")]
    public class MarketingController : Controller
    {
        private readonly InMemoryStore _store;

        public MarketingController(InMemoryStore store)
        {
            _store = store;
        }

        public IActionResult Dashboard()
        {
            // stub KPIs
            var model = new { Promos = new[] { "Summer Sale", "Buy 1 Get 1" } };
            return View(model);
        }

        public IActionResult Promotions()
        {
            // stub promotions list
            var promos = new[] { new { Id = 1, Name = "Summer Sale", Active = true } };
            return View(promos);
        }

        [HttpPost]
        public IActionResult CreatePromotion(string name)
        {
            // stub: would persist promotion
            return RedirectToAction("Promotions");
        }
    }
}
