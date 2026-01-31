using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Cashier,Admin")]
    public class POSController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult CreateOrder()
        {
            return View();
        }
    }
}
