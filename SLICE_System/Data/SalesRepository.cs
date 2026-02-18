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

        // =========================================================
        // 1. GET MENU
        // =========================================================
        public List<MenuProduct> GetMenu()
        {
            using (var connection = _dbService.GetConnection())
            {
                // Query 'MenuItems' table. 
                // We default Category to 'General' since MenuItems doesn't have a Category column in the schema yet.
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

        // =========================================================
        // 2. PROCESS SALE (Transaction + P&L Integration)
        // =========================================================
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
                        // --- STEP 1: GET PRODUCT PRICE SNAPSHOT ---
                        // Crucial for P&L: We must capture the price *at the moment of sale*.
                        string sqlGetProduct = "SELECT ProductName, BasePrice FROM MenuItems WHERE ProductID = @Id";
                        var product = connection.QuerySingleOrDefault(sqlGetProduct, new { Id = productId }, transaction);

                        if (product == null) throw new Exception("Product not found or invalid.");

                        decimal unitPrice = product.BasePrice;
                        decimal totalRevenue = unitPrice * quantitySold;
                        string productName = product.ProductName;

                        // --- STEP 2: CALCULATE INGREDIENTS (Bill of Materials) ---
                        // Get the recipe that links this Menu Item (ProductID) to Raw Stock (ItemID)
                        string sqlGetRecipe = "SELECT * FROM BillOfMaterials WHERE ProductID = @ProductID";
                        var ingredients = connection.Query<Recipe>(sqlGetRecipe, new { ProductID = productId }, transaction).AsList();

                        // --- STEP 3: DEDUCT STOCK ---
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
                                    // Use the property name from your Recipe.cs model
                                    ItemID = ing.IngredientID
                                }, transaction);
                            }
                        }

                        // --- STEP 4: RECORD THE SALE (With Price) ---
                        // Updated to include UnitPrice so historical reports remain accurate even if menu prices change later.
                        string sqlRecord = @"
                            INSERT INTO SalesTransactions (BranchID, ProductID, QuantitySold, UnitPrice, TransactionDate)
                            VALUES (@BranchID, @ProductID, @Qty, @Price, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int newSaleId = connection.ExecuteScalar<int>(sqlRecord, new
                        {
                            BranchID = branchId,
                            ProductID = productId,
                            Qty = quantitySold,
                            Price = unitPrice
                        }, transaction);

                        // --- STEP 5: FINANCIAL LEDGER (The P&L Entry) ---
                        // This injects the "Income" directly into the central finance table.
                        string sqlLedger = @"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, ReferenceID)
                            VALUES (GETDATE(), @BranchID, 'Income', 'Sales', @Amount, @Desc, @RefID)";

                        connection.Execute(sqlLedger, new
                        {
                            BranchID = branchId,
                            Amount = totalRevenue,
                            Desc = $"Sale: {productName} (x{quantitySold})",
                            RefID = newSaleId
                        }, transaction);

                        // Complete the transaction
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}