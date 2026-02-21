using System.Text.Json;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Services
{
    public interface IRecipeService
    {
        Task<List<RecipeItemDto>> GetDefaultRecipeFromApiAsync(string productName, int categoryId);
    }

    public class RecipeItemDto
    {
        public int     IngredientID     { get; set; }
        public decimal QuantityRequired { get; set; }
        public string? IngredientName   { get; set; }
    }

    public class RecipeService : IRecipeService
    {
        private readonly HttpClient              _httpClient;
        private readonly ApplicationDbContext    _db;
        private readonly IHttpContextAccessor    _httpContextAccessor;

        public RecipeService(HttpClient httpClient, ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient          = httpClient;
            _db                  = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<RecipeItemDto>> GetDefaultRecipeFromApiAsync(string productName, int categoryId)
        {
            var results = new List<RecipeItemDto>();
            Console.WriteLine($"[RecipeService] Calling internal recipe API for: '{productName}' (Cat: {categoryId})");

            try
            {
                // Build the base URL from the current request so the service works in
                // both dev (http://localhost:xxxx) and production (https://...) without
                // any hard-coded URLs.
                var request    = _httpContextAccessor.HttpContext?.Request;
                var baseUrl    = request != null
                    ? $"{request.Scheme}://{request.Host}"
                    : "http://localhost:5000";

                var encodedName = Uri.EscapeDataString(productName);
                var apiUrl      = $"{baseUrl}/api/recipes/lookup?name={encodedName}";

                Console.WriteLine($"[RecipeService] GET {apiUrl}");

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[RecipeService] API returned {response.StatusCode} for '{productName}'. No recipe assigned.");
                    return results;
                }

                var json = await response.Content.ReadAsStringAsync();

                // Deserialise the RecipeDto returned by RecipesController
                var apiRecipe = JsonSerializer.Deserialize<ApiRecipeResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apiRecipe?.Ingredients == null || !apiRecipe.Ingredients.Any())
                    return results;

                Console.WriteLine($"[RecipeService] Matched recipe '{apiRecipe.ProductName}' with {apiRecipe.Ingredients.Count} ingredient(s).");

                // Resolve / create each ingredient in the DB
                foreach (var ing in apiRecipe.Ingredients)
                {
                    var dbIngredient = await _db.Ingredients
                        .FirstOrDefaultAsync(i =>
                            i.Name.ToLower() == ing.Name.ToLower() ||
                            i.Name.ToLower().Contains(ing.Name.ToLower()) ||
                            ing.Name.ToLower().Contains(i.Name.ToLower()));

                    if (dbIngredient == null)
                    {
                        dbIngredient = new Ingredient
                        {
                            Name              = ing.Name,
                            Unit              = ing.Unit,
                            StockQuantity     = 0,
                            LowStockThreshold = GetDefaultThreshold(ing.Unit)
                        };
                        _db.Ingredients.Add(dbIngredient);
                        await _db.SaveChangesAsync();
                        Console.WriteLine($"[RecipeService] Auto-created ingredient: {dbIngredient.Name} ({ing.Unit})");
                    }

                    results.Add(new RecipeItemDto
                    {
                        IngredientID     = dbIngredient.IngredientID,
                        IngredientName   = dbIngredient.Name,
                        QuantityRequired = ing.Quantity
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RecipeService] Failed to fetch or process recipe: {ex.Message}");
            }

            return results;
        }

        private static decimal GetDefaultThreshold(string unit) => unit.ToLower() switch
        {
            "g"     => 500m,
            "ml"    => 500m,
            "kg"    => 1m,
            "l"     => 1m,
            "scoop" => 5m,
            _       => 10m
        };

        // ── Internal DTOs matching RecipesApiController response ──────────────

        private class ApiRecipeResponse
        {
            public string                    ProductName { get; set; } = string.Empty;
            public List<ApiIngredientItem>   Ingredients { get; set; } = new();
        }

        private class ApiIngredientItem
        {
            public string  Name     { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public string  Unit     { get; set; } = string.Empty;
        }
    }
}
