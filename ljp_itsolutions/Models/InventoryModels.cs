using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class Ingredient
    {
        [Key]
        public int IngredientID { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public decimal StockQuantity { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = "pcs"; // g, ml, pcs, kg, etc.

        public decimal LowStockThreshold { get; set; }

        public DateTime? ExpiryDate { get; set; }
        public DateTime? LastStockedDate { get; set; }

        public virtual ICollection<ProductRecipe> ProductRecipes { get; set; } = new List<ProductRecipe>();
    }

    public class ProductRecipe
    {
        [Key]
        public int RecipeID { get; set; }

        public int ProductID { get; set; }
        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; } = null!;

        public int IngredientID { get; set; }
        [ForeignKey("IngredientID")]
        public virtual Ingredient Ingredient { get; set; } = null!;

        public decimal QuantityRequired { get; set; }
    }
}
