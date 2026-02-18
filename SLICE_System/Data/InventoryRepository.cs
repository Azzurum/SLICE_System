using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class InventoryRepository
    {
        private readonly DatabaseService _dbService;

        public InventoryRepository()
        {
            _dbService = new DatabaseService();
        }

        // =========================================================
        // 1. MASTER INVENTORY (Global Ingredients Registry)
        // =========================================================

        public List<MasterInventory> GetAllIngredients(string search = "")
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT * FROM MasterInventory
                    WHERE (@Search = '' OR ItemName LIKE @Search OR Category LIKE @Search)
                    ORDER BY Category, ItemName";

                return connection.Query<MasterInventory>(sql, new { Search = "%" + search + "%" }).AsList();
            }
        }

        public void AddIngredient(MasterInventory item)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    INSERT INTO MasterInventory (ItemName, Category, BulkUnit, BaseUnit, ConversionRatio) 
                    VALUES (@ItemName, @Category, @BulkUnit, @BaseUnit, @ConversionRatio)";

                connection.Execute(sql, item);
            }
        }

        // =========================================================
        // 2. BRANCH INVENTORY (Live Stock Levels)
        // =========================================================

        public List<BranchInventoryItem> GetStockForBranch(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT 
                        bi.StockID, 
                        bi.BranchID, 
                        bi.ItemID,  
                        bi.CurrentQuantity, 
                        bi.LowStockThreshold, 
                        bi.ExpirationDate,
                        mi.ItemName, 
                        mi.BaseUnit, 
                        mi.Category
                    FROM BranchInventory bi
                    INNER JOIN MasterInventory mi ON bi.ItemID = mi.ItemID
                    WHERE bi.BranchID = @BranchID
                    ORDER BY mi.ItemName";

                return connection.Query<BranchInventoryItem>(sql, new { BranchID = branchId }).AsList();
            }
        }

        // =========================================================
        // 3. UTILITIES (Branches & Lookups)
        // =========================================================

        // Get All Branches (For Dropdowns/Admin)
        public List<Branch> GetAllBranches()
        {
            using (var connection = _dbService.GetConnection())
            {
                return connection.Query<Branch>("SELECT * FROM Branches ORDER BY BranchName").AsList();
            }
        }

        // Get Shipping Destinations (Exclude Current Branch)
        public List<Branch> GetShippingDestinations(int myBranchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "SELECT * FROM Branches WHERE BranchID != @MyId ORDER BY BranchName";
                return connection.Query<Branch>(sql, new { MyId = myBranchId }).AsList();
            }
        }

        // =========================================================
        // 4. RECONCILIATION & ADJUSTMENTS
        // =========================================================

        // Get Data for Reconciliation Sheet
        public List<ReconItem> GetReconciliationSheet(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT 
                        bi.StockID, 
                        mi.ItemName, 
                        bi.CurrentQuantity as SystemQty, 
                        0 as PhysicalQty -- Default to 0 for user input
                    FROM BranchInventory bi
                    JOIN MasterInventory mi ON bi.ItemID = mi.ItemID
                    WHERE bi.BranchID = @BranchID
                    ORDER BY mi.ItemName";

                return connection.Query<ReconItem>(sql, new { BranchID = branchId }).AsList();
            }
        }

        // Save Adjustment Transaction
        public void SaveAdjustment(int stockId, decimal systemQty, decimal physicalQty, int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // Calculate Variance
                decimal variance = physicalQty - systemQty;

                // A. Log the Adjustment History
                string sqlLog = @"
                    INSERT INTO InventoryAdjustments (StockID, SystemQty, PhysicalQty, Variance, AdjustedBy, AdjustmentDate)
                    VALUES (@StockID, @Sys, @Phys, @Var, @User, GETDATE())";

                connection.Execute(sqlLog, new { StockID = stockId, Sys = systemQty, Phys = physicalQty, Var = variance, User = userId });

                // B. Update the Real Stock to match Physical Count
                string sqlUpdate = "UPDATE BranchInventory SET CurrentQuantity = @Phys WHERE StockID = @StockID";
                connection.Execute(sqlUpdate, new { Phys = physicalQty, StockID = stockId });
            }
        }

        // Helper Class for Reconciliation Logic
        public class ReconItem
        {
            public int StockID { get; set; }
            public string ItemName { get; set; }
            public decimal SystemQty { get; set; }
            public decimal PhysicalQty { get; set; } // This property binds to the Editable Column
        }
    }
}