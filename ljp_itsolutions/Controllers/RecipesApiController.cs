using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Controllers
{
    // ── Response DTOs ─────────────────────────────────────────────────────────

    public class RecipeIngredientDto
    {
        public string  Name     { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string  Unit     { get; set; } = string.Empty;
    }

    public class RecipeDto
    {
        public int    RecipeTemplateID { get; set; }
        public string ProductName      { get; set; } = string.Empty;
        public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    }


    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class RecipesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public RecipesController(ApplicationDbContext db)
        {
            _db = db;
        }


        /// Returns the full recipe template catalogue from the database.
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var templates = await _db.RecipeTemplates
                .Include(t => t.Ingredients)
                .OrderBy(t => t.ProductName)
                .ToListAsync();

            return Ok(templates.Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var template = await _db.RecipeTemplates
                .Include(t => t.Ingredients)
                .FirstOrDefaultAsync(t => t.RecipeTemplateID == id);

            if (template == null)
                return NotFound(new { error = $"Recipe template with ID {id} not found." });

            return Ok(ToDto(template));
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> Lookup([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { error = "name query parameter is required." });

            var all = await _db.RecipeTemplates
                .Include(t => t.Ingredients)
                .ToListAsync();

            var match = all
                .OrderByDescending(t => t.ProductName.Length)
                .FirstOrDefault(t =>
                    name.Contains(t.ProductName, StringComparison.OrdinalIgnoreCase) ||
                    t.ProductName.Contains(name, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return NotFound(new { error = $"No recipe template found for '{name}'." });

            return Ok(ToDto(match));
        }


        // helpers that when a new product is added , we don't have to update the API code to include it in the response
        private static RecipeDto ToDto(RecipeTemplate t) => new()
        {
            RecipeTemplateID = t.RecipeTemplateID,
            ProductName      = t.ProductName,
            Ingredients      = t.Ingredients.Select(i => new RecipeIngredientDto
            {
                Name     = i.IngredientName,
                Quantity = i.Quantity,
                Unit     = i.Unit
            }).ToList()
        };
    }
}
