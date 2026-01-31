using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ljp_itsolutions.Controllers
{
    public class AccountController : Controller
    {
        private readonly ljp_itsolutions.Data.ApplicationDbContext _db;
        private readonly ljp_itsolutions.Services.IEmailSender _emailSender;
        private readonly Microsoft.AspNetCore.Identity.IPasswordHasher<ljp_itsolutions.Models.User> _hasher;

        public AccountController(ljp_itsolutions.Data.ApplicationDbContext db, ljp_itsolutions.Services.IEmailSender emailSender, Microsoft.AspNetCore.Identity.IPasswordHasher<ljp_itsolutions.Models.User> hasher)
        {
            _db = db;
            _emailSender = emailSender;
            _hasher = hasher;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            ljp_itsolutions.Models.User? user = null;
            if (!string.IsNullOrWhiteSpace(model.UsernameOrEmail))
            {
                user = await _db.Users.FirstOrDefaultAsync(u => u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View(model);
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View(model);
            }

            var verify = _hasher.VerifyHashedPassword(user, user.Password, model.Password);
            if (verify == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, (user.Role ?? Role.Admin).ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            var primaryRole = (user.Role ?? Role.Admin).ToString();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return primaryRole switch
            {
                nameof(Role.Admin) => RedirectToAction("Dashboard", "Admin"),
                nameof(Role.Manager) => RedirectToAction("Dashboard", "Manager"),
                nameof(Role.Cashier) => RedirectToAction("Index", "POS"),
                nameof(Role.MarketingStaff) => RedirectToAction("Dashboard", "Marketing"),
                _ => RedirectToAction("Index", "Home"),
            };
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPasswordSubmit(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                TempData["Message"] = "If the account exists, password reset instructions were sent.";
                return RedirectToAction("Login");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username || u.Email == username);
            if (user == null)
            {
                TempData["Message"] = "If the account exists, password reset instructions were sent.";
                return RedirectToAction("Login");
            }

            // Simple password reset placeholder - in a real app generate a token and flow
            var callback = Url.Action("ResetPassword", "Account", new { userId = user.Id, token = "placeholder" }, protocol: Request.Scheme);
            await _emailSender.SendEmailAsync(user.Email, "Password Reset", $"Reset your password using this link: {callback}");
            TempData["Message"] = "Password reset instructions were sent to the registered email.";
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            var model = new ljp_itsolutions.Models.ResetPasswordViewModel { UserId = userId, Token = token };
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ljp_itsolutions.Models.ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!Guid.TryParse(model.UserId, out var guid))
            {
                ModelState.AddModelError(string.Empty, "Invalid password reset request.");
                return View(model);
            }

            var user = await _db.Users.FindAsync(guid);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid password reset request.");
                return View(model);
            }

            // Simple reset: replace password hash directly (token omitted in this simplified flow)
            user.Password = _hasher.HashPassword(user, model.NewPassword);
            await _db.SaveChangesAsync();

            TempData["Message"] = "Password has been reset.";
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}
