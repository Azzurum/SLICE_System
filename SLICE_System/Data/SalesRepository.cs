using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class SalesRepository
    {
        private readonly DatabaseService _dbService;

        public SalesRepository()
        {
            _dbService = new DatabaseService();
        }

        // 1. GET MENU
        public List<MenuProduct> GetMenu()
        {
            using (var connection = _dbService.GetConnection())
            {
                // FIX: Query 'MenuItems' table, not 'MasterInventory'
                // We default Category to 'General' since MenuItems doesn't have a Category column in your schema
                string sql = @"
                    SELECT ProductID, 
                           ProductName, 
                           BasePrice, 
                           'General' as Category 
                    FROM MenuItems 
                    WHERE IsAvailable = 1";

                return connection.Query<MenuProduct>(sql).ToList();
            }
        }

        // 2. PROCESS SALE (Transaction with Recipe Deduction)
        public void ProcessSale(int branchId, int productId, int quantitySold, int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();

                // Start a Transaction (All or Nothing)
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // --- STEP 1: CALCULATE INGREDIENTS (Bill of Materials) ---
                        // Get the recipe that links this Menu Item (ProductID) to Raw Stock (ItemID)
                        string sqlGetRecipe = "SELECT * FROM BillOfMaterials WHERE ProductID = @ProductID";
                        var ingredients = connection.Query<Recipe>(sqlGetRecipe, new { ProductID = productId }, transaction).AsList();

                        // --- STEP 2: DEDUCT STOCK ---
                        // Only run deduction if there is a recipe. 
                        // We cannot deduct the ProductID directly because MenuItems and MasterInventory are separate tables.
                        if (ingredients.Any())
                        {
                            string sqlDeduct = @"
                                UPDATE BranchInventory 
                                SET CurrentQuantity = CurrentQuantity - @AmountToDeduct
                                WHERE BranchID = @BranchID AND ItemID = @ItemID";

                            foreach (var ing in ingredients)
                            {
                                decimal totalNeeded = ing.RequiredQty * quantitySold;

                                connection.Execute(sqlDeduct, new
                                {
                                    AmountToDeduct = totalNeeded,
                                    BranchID = branchId,
                                    ItemID = ing.IngredientID // This is the Raw Material ID from MasterInventory
                                }, transaction);
                            }
                        }

                        // --- STEP 3: RECORD THE SALE ---
                        string sqlRecord = @"
                            INSERT INTO SalesTransactions (BranchID, ProductID, QuantitySold, TransactionDate)
                            VALUES (@BranchID, @ProductID, @Qty, GETDATE())";

                        connection.Execute(sqlRecord, new
                        {
                            BranchID = branchId,
                            ProductID = productId,
                            Qty = quantitySold
                        }, transaction);

                        // Save Changes
                        transaction.Commit();
                    }
                    catch
                    {
                        // Cancel if error
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}