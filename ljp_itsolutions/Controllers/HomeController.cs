using System.Diagnostics;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Mvc;

namespace ljp_itsolutions.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? HttpContext.Session.GetString("UserRole");
                if (string.Equals(role, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "SuperAdmin");
                if (string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "Admin");
                if (string.Equals(role, UserRoles.Manager, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "Manager");
                if (string.Equals(role, UserRoles.Cashier, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Index", "POS");
                if (string.Equals(role, UserRoles.MarketingStaff, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Dashboard", "Marketing");
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
