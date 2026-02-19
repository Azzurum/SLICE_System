namespace SLICE_System.Models
{
    /// <summary>
    /// Represents a global ingredient or raw material definition in the Master Inventory.
    /// This registry is shared across all branches to ensure consistent naming and measurement.
    /// </summary>
    public class MasterInventory
    {
        /// <summary>
        /// Unique identifier for the ingredient (Primary Key).
        /// </summary>
        public int ItemID { get; set; }

        /// <summary>
        /// The standard name of the ingredient (e.g., "High Gluten Flour").
        /// </summary>
        public string? ItemName { get; set; }

        /// <summary>
        /// The grouping category (e.g., "Dough & Flour", "Cheese & Dairy").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// The unit used for purchasing or shipping (e.g., "Sack", "Box", "Gallon").
        /// </summary>
        public string? BulkUnit { get; set; }

        /// <summary>
        /// The smallest unit used for recipe calculation (e.g., "g", "ml", "pcs").
        /// </summary>
        public string? BaseUnit { get; set; }

        /// <summary>
        /// The multiplier to convert 1 Bulk Unit into Base Units.
        /// Example: If 1 Sack = 25,000 grams, this value is 25000.
        /// </summary>
        public decimal ConversionRatio { get; set; }

        public decimal TotalStock { get; set; }

        // ---------------------------------------------------------
        // HELPER PROPERTIES (Read-Only)
        // ---------------------------------------------------------

        /// <summary>
        /// Returns a formatted string description of the item and its conversion logic.
        /// Handles null values gracefully to prevent UI crashes.
        /// </summary>
        public string FullDescription =>
            $"{ItemName ?? "Unknown Item"} ({ConversionRatio:N0} {BaseUnit ?? "-"} per {BulkUnit ?? "-"})";
    }
}