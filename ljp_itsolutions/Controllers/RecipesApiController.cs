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

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// REST API that exposes the recipe template catalogue stored in the database.
    ///
    /// Endpoints:
    ///   GET  /api/recipes           → all templates
    ///   GET  /api/recipes/{id}      → single template by ID
    ///   GET  /api/recipes/lookup?name=Cappuccino → best-matching template
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Secured at the app level; internal service-to-service calls only
    public class RecipesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public RecipesController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ── GET /api/recipes ──────────────────────────────────────────────────
        /// <summary>Returns the full recipe template catalogue from the database.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var templates = await _db.RecipeTemplates
                .Include(t => t.Ingredients)
                .OrderBy(t => t.ProductName)
                .ToListAsync();

            return Ok(templates.Select(ToDto));
        }

        // ── GET /api/recipes/{id} ─────────────────────────────────────────────
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

        // ── GET /api/recipes/lookup?name=Vanilla+Latte ────────────────────────
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

        // ── Helpers ───────────────────────────────────────────────────────────

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
