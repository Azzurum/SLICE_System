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
        // 1. GET MENU (With Recipe-Driven Depletion Engine)
        // =========================================================
        public List<MenuProduct> GetMenu(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // Calculates the maximum portions that can be made based on the limiting ingredient.
                string sql = @"
                    SELECT 
                        m.ProductID, 
                        m.ProductName, 
                        m.BasePrice, 
                        'General' as Category,
                        CAST(ISNULL(
                            MIN(
                                FLOOR(ISNULL(bi.CurrentQuantity, 0) / NULLIF(bom.RequiredQty, 0))
                            ), 999
                        ) AS INT) AS MaxCookable
                    FROM MenuItems m
                    LEFT JOIN BillOfMaterials bom ON m.ProductID = bom.ProductID
                    LEFT JOIN BranchInventory bi ON bom.ItemID = bi.ItemID AND bi.BranchID = @BranchID
                    WHERE m.IsAvailable = 1
                    GROUP BY m.ProductID, m.ProductName, m.BasePrice";

                return connection.Query<MenuProduct>(sql, new { BranchID = branchId }).ToList();
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

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // --- STEP 1: GET PRODUCT PRICE SNAPSHOT ---
                        string sqlGetProduct = "SELECT ProductID, ProductName, BasePrice FROM MenuItems WHERE ProductID = @Id";
                        // FIX: Strongly typed to MenuItem to prevent dynamic runtime crashes
                        var product = connection.QuerySingleOrDefault<MenuItem>(sqlGetProduct, new { Id = productId }, transaction);

                        if (product == null) throw new Exception("Product not found or invalid.");

                        decimal unitPrice = product.BasePrice;
                        decimal totalRevenue = unitPrice * quantitySold;
                        string productName = product.ProductName;

                        // --- STEP 2: CALCULATE INGREDIENTS (Bill of Materials) ---
                        string sqlGetRecipe = "SELECT ProductID, ItemID as IngredientID, RequiredQty FROM BillOfMaterials WHERE ProductID = @ProductID";
                        var ingredients = connection.Query<Recipe>(sqlGetRecipe, new { ProductID = productId }, transaction).AsList();

                        // --- STEP 3: DEDUCT STOCK (With Negative Stock Prevention) ---
                        if (ingredients.Any())
                        {
                            string sqlDeduct = @"
                                UPDATE BranchInventory 
                                SET CurrentQuantity = CurrentQuantity - @AmountToDeduct
                                WHERE BranchID = @BranchID AND ItemID = @ItemID 
                                AND CurrentQuantity >= @AmountToDeduct"; // Safety check to prevent negative inventory

                            foreach (var ing in ingredients)
                            {
                                decimal totalNeeded = ing.RequiredQty * quantitySold;

                                int rowsAffected = connection.Execute(sqlDeduct, new
                                {
                                    AmountToDeduct = totalNeeded,
                                    BranchID = branchId,
                                    ItemID = ing.IngredientID
                                }, transaction);

                                // If 0 rows were updated, it means stock was insufficient
                                if (rowsAffected == 0)
                                {
                                    throw new Exception($"Transaction blocked: Insufficient stock for an ingredient required to make {productName}.");
                                }
                            }
                        }

                        // --- STEP 4: RECORD THE SALE (With Price) ---
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