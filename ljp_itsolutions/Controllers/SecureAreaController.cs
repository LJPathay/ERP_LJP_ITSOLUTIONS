using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ljp_itsolutions.Controllers
{
    
    [Authorize(Roles = "Admin,Manager,MarketingStaff,SuperAdmin")]
    public class SecureAreaController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult UserManagement()
        {
            return View();
        }
    }
}
