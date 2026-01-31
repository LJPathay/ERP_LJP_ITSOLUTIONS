using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ljp_itsolutions.Controllers
{
    // This controller represents a secure area that only certain roles can access
    [Authorize(Roles = "Admin,Manager,MarketingStaff")]
    public class SecureAreaController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // Only Admin can access user management
        [Authorize(Roles = "Admin")]
        public IActionResult UserManagement()
        {
            return View();
        }
    }
}
