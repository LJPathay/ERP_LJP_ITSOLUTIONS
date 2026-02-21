using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    /// <summary>
    /// A default recipe template for a drink type (e.g. "Cappuccino").
    /// When a manager creates a new product, the system looks up the
    /// best-matching template and auto-populates the product's recipe.
    /// </summary>
    public class RecipeTemplate
    {
        [Key]
        public int RecipeTemplateID { get; set; }

        /// <summary>The canonical drink name used for matching (e.g. "Vanilla Latte").</summary>
        [Required]
        [StringLength(150)]
        public string ProductName { get; set; } = string.Empty;

        public virtual ICollection<RecipeTemplateIngredient> Ingredients { get; set; }
            = new List<RecipeTemplateIngredient>();
    }

    /// <summary>
    /// One ingredient line in a <see cref="RecipeTemplate"/>.
    /// </summary>
    public class RecipeTemplateIngredient
    {
        [Key]
        public int RecipeTemplateIngredientID { get; set; }

        public int RecipeTemplateID { get; set; }
        [ForeignKey(nameof(RecipeTemplateID))]
        public virtual RecipeTemplate RecipeTemplate { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string IngredientName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = string.Empty;
    }
}
