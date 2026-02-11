using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ljp_itsolutions.Controllers
{
    
    [Authorize(Roles = "Admin,Manager,MarketingStaff")]
    public class SecureAreaController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult UserManagement()
        {
            return View();
        }
    }
}
