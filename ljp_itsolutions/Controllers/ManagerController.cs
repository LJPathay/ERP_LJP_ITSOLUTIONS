using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Authorization;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IPhotoService _photoService;

        public ManagerController(ApplicationDbContext db, IPhotoService photoService)
        {
            _db = db;
            _photoService = photoService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var model = new
            {
                TotalProducts = await _db.Products.CountAsync(),
                TotalUsers = await _db.Users.CountAsync(),
                TotalOrders = await _db.Orders.CountAsync()
            };
            return View(model);
        }

        public async Task<IActionResult> Products()
        {
            var products = await _db.Products.Include(p => p.Category).ToListAsync();
            ViewBag.Categories = await _db.Categories.ToListAsync();
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile photo)
        {
            if (photo != null)
            {
                var result = await _photoService.AddPhotoAsync(photo);
                if (result.Error == null)
                {
                    product.ImageURL = result.SecureUrl.AbsoluteUri;
                }
            }

            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            return RedirectToAction("Products");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStock(int id, int stock)
        {
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                product.StockQuantity = stock;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Products");
        }

        public IActionResult Reports()
        {
            return View();
        }
    }
}
