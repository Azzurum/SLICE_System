using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class MenuRepository
    {
        private readonly DatabaseService _dbService;

        public MenuRepository() => _dbService = new DatabaseService();

        // 1. GET ALL MENU ITEMS
        public List<MenuItem> GetAllMenuItems()
        {
            using (var connection = _dbService.GetConnection())
            {
                // Order by Availability first, then Name
                string sql = "SELECT * FROM MenuItems ORDER BY IsAvailable DESC, ProductName";
                return connection.Query<MenuItem>(sql).AsList();
            }
        }

        // --- NEW: FETCH RECIPE FOR A SPECIFIC ITEM ---
        public List<RecipeItemVM> GetRecipeForProduct(int productId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // Join BOM with MasterInventory to get the Name and BaseUnit (e.g., grams, ml)
                string sql = @"
                    SELECT 
                        b.ItemID AS IngredientID, 
                        m.ItemName, 
                        m.BaseUnit, 
                        b.RequiredQty
                    FROM BillOfMaterials b
                    INNER JOIN MasterInventory m ON b.ItemID = m.ItemID
                    WHERE b.ProductID = @ProductID";

                return connection.Query<RecipeItemVM>(sql, new { ProductID = productId }).AsList();
            }
        }

        // 2. ADD NEW ITEM (Upgraded with Recipe support)
        public void AddMenuItem(MenuItem item)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var trans = connection.BeginTransaction()) // Start Transaction
                {
                    try
                    {
                        // A. Insert the Menu Item
                        string sqlMenu = @"
                            INSERT INTO MenuItems (ProductName, BasePrice, IsAvailable)
                            VALUES (@ProductName, @BasePrice, @IsAvailable);
                            SELECT SCOPE_IDENTITY();";

                        // Get the new auto-generated ProductID
                        int newId = connection.ExecuteScalar<int>(sqlMenu, item, trans);

                        // B. Insert the Recipe Ingredients
                        if (item.Recipe != null && item.Recipe.Any())
                        {
                            string sqlBOM = "INSERT INTO BillOfMaterials (ProductID, ItemID, RequiredQty) VALUES (@ProductID, @ItemID, @RequiredQty)";
                            foreach (var ing in item.Recipe)
                            {
                                connection.Execute(sqlBOM, new { ProductID = newId, ItemID = ing.IngredientID, RequiredQty = ing.RequiredQty }, trans);
                            }
                        }

                        trans.Commit(); // Save everything
                    }
                    catch
                    {
                        trans.Rollback(); // Cancel everything if it fails
                        throw;
                    }
                }
            }
        }

        // 3. UPDATE ITEM (Wipe and Replace Strategy)
        public void UpdateMenuItem(MenuItem item)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var trans = connection.BeginTransaction()) // Start Transaction
                {
                    try
                    {
                        // A. Update main details
                        string sqlUpdate = @"
                            UPDATE MenuItems 
                            SET ProductName = @ProductName, 
                                BasePrice = @BasePrice, 
                                IsAvailable = @IsAvailable
                            WHERE ProductID = @ProductID";
                        connection.Execute(sqlUpdate, item, trans);

                        // B. WIPE existing recipe for this product
                        string sqlDeleteBOM = "DELETE FROM BillOfMaterials WHERE ProductID = @ProductID";
                        connection.Execute(sqlDeleteBOM, new { ProductID = item.ProductID }, trans);

                        // C. REPLACE with new recipe
                        if (item.Recipe != null && item.Recipe.Any())
                        {
                            string sqlInsertBOM = "INSERT INTO BillOfMaterials (ProductID, ItemID, RequiredQty) VALUES (@ProductID, @ItemID, @RequiredQty)";
                            foreach (var ing in item.Recipe)
                            {
                                connection.Execute(sqlInsertBOM, new { ProductID = item.ProductID, ItemID = ing.IngredientID, RequiredQty = ing.RequiredQty }, trans);
                            }
                        }

                        trans.Commit(); // Save everything
                    }
                    catch
                    {
                        trans.Rollback(); // Cancel everything if it fails
                        throw;
                    }
                }
            }
        }

        // 4. TOGGLE STATUS (Quick Action)
        public void ToggleStatus(int id, bool newStatus)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE MenuItems SET IsAvailable = @Status WHERE ProductID = @ID";
                connection.Execute(sql, new { Status = newStatus, ID = id });
            }
        }
    }
}