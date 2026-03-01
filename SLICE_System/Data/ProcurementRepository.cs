using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class ProcurementRepository
    {
        private readonly DatabaseService _dbService;

        public ProcurementRepository()
        {
            _dbService = new DatabaseService();
        }

        public void ProcessPurchase(Purchase header, List<PurchaseDetail> details)
        {
            using (var conn = _dbService.GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert Header
                        string sqlHeader = @"
                            INSERT INTO Purchases (Supplier, TotalAmount, PurchasedBy, BranchID, PurchaseDate)
                            VALUES (@Supplier, @TotalAmount, @PurchasedBy, @BranchID, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int newPurchaseId = conn.ExecuteScalar<int>(sqlHeader, header, trans);

                        // 2. Insert Details & Update Inventory
                        // We must fetch the conversion ratio to turn Bulk Units (Sacks) into Base Units (Grams)
                        string sqlConversion = "SELECT ISNULL(ConversionRatio, 1) FROM MasterInventory WHERE ItemID = @ItemId";

                        string sqlDetail = @"
                            INSERT INTO PurchaseDetails (PurchaseID, ItemID, Quantity, UnitPrice)
                            VALUES (@Pid, @ItemId, @Qty, @Price)";

                        string sqlUpdateStock = @"
                            UPDATE BranchInventory 
                            SET CurrentQuantity = CurrentQuantity + @Qty, LastUpdated = GETDATE()
                            WHERE BranchID = @Bid AND ItemID = @ItemId";

                        // Ensure stock record exists before updating
                        string sqlEnsureStock = @"
                            IF NOT EXISTS (SELECT 1 FROM BranchInventory WHERE BranchID = @Bid AND ItemID = @ItemId)
                            BEGIN
                                INSERT INTO BranchInventory (BranchID, ItemID, CurrentQuantity, LowStockThreshold)
                                VALUES (@Bid, @ItemId, 0, 10) -- Default threshold
                            END";

                        foreach (var item in details)
                        {
                            // A. Fetch Conversion Ratio (e.g., 1 Sack = 25000 grams)
                            decimal ratio = conn.ExecuteScalar<decimal>(sqlConversion, new { ItemId = item.ItemID }, trans);
                            if (ratio <= 0) ratio = 1; // Safety fallback to prevent divide-by-zero

                            // B. Perform Bulk-to-Base Conversion Math
                            decimal baseQty = item.Quantity * ratio;           // e.g., 1 Sack * 25000 = 25000
                            decimal baseUnitPrice = item.UnitPrice / ratio;    // e.g., ₱1000 / 25000 = ₱0.04 per gram

                            // C. Ensure Record Exists
                            conn.Execute(sqlEnsureStock, new { Bid = header.BranchID, ItemId = item.ItemID }, trans);

                            // D. Insert Detail (Saved in Base Units to fix P&L Waste & Reconciliation Calculations)
                            conn.Execute(sqlDetail, new { Pid = newPurchaseId, ItemId = item.ItemID, Qty = baseQty, Price = baseUnitPrice }, trans);

                            // E. Add Stock (In Base Units)
                            conn.Execute(sqlUpdateStock, new { Qty = baseQty, Bid = header.BranchID, ItemId = item.ItemID }, trans);
                        }

                        // 3. LOG TO FINANCIAL LEDGER (The Critical P&L Step)
                        string sqlLedger = @"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, ReferenceID)
                            VALUES (GETDATE(), @BranchID, 'Expense', 'Ingredients', @Amount, @Desc, @RefID)";

                        conn.Execute(sqlLedger, new
                        {
                            BranchID = header.BranchID,
                            Amount = header.TotalAmount,
                            Desc = $"Purchase from {header.Supplier}",
                            RefID = newPurchaseId
                        }, trans);

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}