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
        public int IngredientID { get; set; }
        public decimal QuantityRequired { get; set; }
        public string? IngredientName { get; set; }
    }

    public class RecipeService : IRecipeService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _db;

        public RecipeService(HttpClient httpClient, ApplicationDbContext db)
        {
            _httpClient = httpClient;
            _db = db;
        }

        public async Task<List<RecipeItemDto>> GetDefaultRecipeFromApiAsync(string productName, int categoryId)
        {
            var results = new List<RecipeItemDto>();
            Console.WriteLine($"[RecipeService] Starting fetch for: {productName} (Cat: {categoryId})");

            // Step 1: Try External API
            try
            {
                if (categoryId == 1) // Coffee
                {
                    var response = await _httpClient.GetAsync("https://api.sampleapis.com/coffee/hot");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var coffeeData = JsonSerializer.Deserialize<List<CoffeeApiItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        // Use a more flexible name matching
                        var match = coffeeData?.FirstOrDefault(c => 
                            productName.Contains(c.Title, StringComparison.OrdinalIgnoreCase) || 
                            c.Title.Contains(productName, StringComparison.OrdinalIgnoreCase) ||
                            (productName.ToLower().Contains("cap") && c.Title.Contains("Cappuccino")));

                        if (match != null && match.Ingredients != null)
                        {
                            foreach (var ingName in match.Ingredients)
                            {
                                // Simplify DB matching to be SQL-translation friendly
                                var dbIngredient = await _db.Ingredients
                                    .FirstOrDefaultAsync(i => i.Name.Contains(ingName) || ingName.Contains(i.Name));

                                if (dbIngredient == null)
                                {
                                    dbIngredient = new Ingredient 
                                    { 
                                        Name = ingName, 
                                        Unit = GetDefaultUnit(ingName),
                                        StockQuantity = 0,
                                        LowStockThreshold = 1
                                    };
                                    _db.Ingredients.Add(dbIngredient);
                                    await _db.SaveChangesAsync();
                                }

                                results.Add(new RecipeItemDto 
                                { 
                                    IngredientID = dbIngredient.IngredientID,
                                    IngredientName = dbIngredient.Name,
                                    QuantityRequired = GetDefaultQuantity(dbIngredient.Unit) 
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RecipeService] API Fetch Failed: {ex.Message}");
            }

            // Step 2: Fallback Logic (Mandatory if results still empty)
            if (results.Count == 0)
            {
                try 
                {
                    if (categoryId == 1) // Coffee
                    {
                        var beans = await _db.Ingredients.FirstOrDefaultAsync(i => i.Name.Contains("Beans") || i.Name.Contains("Coffee"));
                        if (beans == null)
                        {
                            beans = new Ingredient { Name = "Espresso Beans", Unit = "kg", StockQuantity = 0, LowStockThreshold = 2 };
                            _db.Ingredients.Add(beans);
                            await _db.SaveChangesAsync();
                        }
                        results.Add(new RecipeItemDto { IngredientID = beans.IngredientID, QuantityRequired = 0.018m, IngredientName = beans.Name });
                    }
                    else if (categoryId == 3) // Pastries
                    {
                        var flour = await _db.Ingredients.FirstOrDefaultAsync(i => i.Name.Contains("Flour"));
                        if (flour == null)
                        {
                            flour = new Ingredient { Name = "Baking Flour", Unit = "kg", StockQuantity = 0, LowStockThreshold = 5 };
                            _db.Ingredients.Add(flour);
                            await _db.SaveChangesAsync();
                        }
                        results.Add(new RecipeItemDto { IngredientID = flour.IngredientID, QuantityRequired = 0.100m, IngredientName = flour.Name });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RecipeService] Fallback Failed: {ex.Message}");
                }
            }

            return results;
        }

        private string GetDefaultUnit(string ingredientName)
        {
            var name = ingredientName.ToLower();
            if (name.Contains("milk") || name.Contains("water") || name.Contains("syrup") || name.Contains("liquid")) return "ml";
            if (name.Contains("beans") || name.Contains("flour") || name.Contains("powder") || name.Contains("sugar")) return "kg";
            return "pcs";
        }

        private decimal GetDefaultQuantity(string unit)
        {
            return unit.ToLower() switch
            {
                "kg" => 0.018m,
                "l" => 0.250m,
                "ml" => 30m,
                _ => 1m
            };
        }

        private class CoffeeApiItem
        {
            public string Title { get; set; } = string.Empty;
            public List<string> Ingredients { get; set; } = new();
        }
    }
}
