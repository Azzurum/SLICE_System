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
                            // A. Ensure Record Exists
                            conn.Execute(sqlEnsureStock, new { Bid = header.BranchID, ItemId = item.ItemID }, trans);

                            // B. Insert Detail
                            conn.Execute(sqlDetail, new { Pid = newPurchaseId, ItemId = item.ItemID, Qty = item.Quantity, Price = item.UnitPrice }, trans);

                            // C. Add Stock
                            conn.Execute(sqlUpdateStock, new { Qty = item.Quantity, Bid = header.BranchID, ItemId = item.ItemID }, trans);
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