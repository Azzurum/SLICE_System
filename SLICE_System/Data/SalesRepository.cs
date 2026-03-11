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
                    m.ImagePath,
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
                GROUP BY m.ProductID, m.ProductName, m.BasePrice, m.ImagePath";

                return connection.Query<MenuProduct>(sql, new { BranchID = branchId }).ToList();
            }
        }

        // =========================================================
        // 2. COMPLETE SALE (Entire Cart + Audit/Payment Integration)
        // =========================================================
        public bool CompleteSale(int branchId, int userId, List<CartItem> cart, string paymentMethod, string referenceNumber, out string errorMessage)
        {
            errorMessage = string.Empty;

            using (var connection = _dbService.GetConnection())
            {
                connection.Open();

                // Start a transaction so the whole cart succeeds or fails together (Atomic Transaction)
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        decimal totalCartRevenue = 0;

                        // Loop through every item in the shopping cart
                        foreach (var item in cart)
                        {
                            // --- STEP 1: GET PRODUCT PRICE SNAPSHOT ---
                            string sqlGetProduct = "SELECT ProductID, ProductName, BasePrice FROM MenuItems WHERE ProductID = @Id";
                            var product = connection.QuerySingleOrDefault<MenuItem>(sqlGetProduct, new { Id = item.ProductID }, transaction);

                            if (product == null) throw new Exception($"Product '{item.ProductName}' not found or invalid.");

                            decimal unitPrice = product.BasePrice;
                            decimal itemTotalRevenue = unitPrice * item.Qty;
                            totalCartRevenue += itemTotalRevenue;

                            // --- STEP 2: CALCULATE INGREDIENTS (Bill of Materials) ---
                            string sqlGetRecipe = "SELECT ProductID, ItemID as IngredientID, RequiredQty FROM BillOfMaterials WHERE ProductID = @ProductID";
                            var ingredients = connection.Query<Recipe>(sqlGetRecipe, new { ProductID = item.ProductID }, transaction).AsList();

                            // --- STEP 3: DEDUCT STOCK (With Negative Stock Prevention) ---
                            if (ingredients.Any())
                            {
                                string sqlDeduct = @"
                                    UPDATE BranchInventory 
                                    SET CurrentQuantity = CurrentQuantity - @AmountToDeduct
                                    WHERE BranchID = @BranchID AND ItemID = @ItemID 
                                    AND CurrentQuantity >= @AmountToDeduct"; // Safety check

                                foreach (var ing in ingredients)
                                {
                                    decimal totalNeeded = ing.RequiredQty * item.Qty;

                                    int rowsAffected = connection.Execute(sqlDeduct, new
                                    {
                                        AmountToDeduct = totalNeeded,
                                        BranchID = branchId,
                                        ItemID = ing.IngredientID
                                    }, transaction);

                                    // If 0 rows were updated, it means stock was insufficient
                                    if (rowsAffected == 0)
                                    {
                                        throw new Exception($"Transaction blocked: Insufficient stock for an ingredient required to make {product.ProductName}.");
                                    }
                                }
                            }

                            // --- STEP 4: RECORD THE SALE (Updated for Audit Traceability) ---
                            string sqlRecord = @"
                                INSERT INTO SalesTransactions 
                                (BranchID, UserID, ProductID, QuantitySold, UnitPrice, TransactionDate, PaymentMethod, ReferenceNumber, TransactionStatus)
                                VALUES 
                                (@BranchID, @UserID, @ProductID, @Qty, @Price, GETDATE(), @PayMethod, @RefNum, 'Completed')";

                            connection.Execute(sqlRecord, new
                            {
                                BranchID = branchId,
                                UserID = userId,
                                ProductID = item.ProductID,
                                Qty = item.Qty,
                                Price = unitPrice,
                                PayMethod = paymentMethod,
                                RefNum = referenceNumber,
                                Status = "Completed"
                            }, transaction);
                        }

                        // --- STEP 5: FINANCIAL LEDGER (Centralized Income Tracking) ---
                        string sqlLedger = @"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, PaymentMethod, ReferenceNumber)
                            VALUES (GETDATE(), @BranchID, 'Income', 'Sales', @Amount, @Desc, @PayMethod, @RefNum)";

                        connection.Execute(sqlLedger, new
                        {
                            BranchID = branchId,
                            Amount = totalCartRevenue,
                            Desc = $"POS Sale ({cart.Count} items)",
                            PayMethod = paymentMethod,
                            RefNum = referenceNumber
                        }, transaction);

                        // --- STEP 6: WRITE DIRECTLY TO AUDIT LOG ---
                        // This fixes the "SYSTEM_LOG" issue by capturing the Reference Number immediately.
                        string sqlAudit = @"
                            INSERT INTO AuditLogs (UserID, ActionType, AffectedTable, NewValue, Timestamp, ReferenceNumber)
                            VALUES (@UserID, 'Sale Completed', 'SalesTransactions', @Desc, GETDATE(), @RefNum)";

                        connection.Execute(sqlAudit, new
                        {
                            UserID = userId,
                            Desc = $"Processed sale for {cart.Count} items. Gateway: {paymentMethod.ToUpper()} - Total: ₱{totalCartRevenue:N2}",
                            RefNum = referenceNumber
                        }, transaction);

                        // Success! Everything commits to the DB at once.
                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // Rollback ensures that if one item fails, NO ingredients are deducted and NO sale is recorded.
                        transaction.Rollback();
                        errorMessage = ex.Message;
                        return false;
                    }
                }
            }
        }
    }
}