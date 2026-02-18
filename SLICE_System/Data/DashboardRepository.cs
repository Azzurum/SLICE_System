using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class DashboardRepository
    {
        private readonly DatabaseService _dbService;

        public DashboardRepository() => _dbService = new DatabaseService();

        // 1. GET BRANCHES FOR DROPDOWN
        public List<Branch> GetAllBranches()
        {
            using (var connection = _dbService.GetConnection())
            {
                return connection.Query<Branch>("SELECT BranchID, BranchName FROM Branches").ToList();
            }
        }

        // 2. GET METRICS (With Filters)
        public DashboardMetrics GetMetrics(DateTime start, DateTime end, int? branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                var metrics = new DashboardMetrics();

                // Parameters for Dapper
                var p = new { Start = start, End = end, BranchID = branchId };

                // A. KPI: Sales (Filtered by Time & Branch)
                string sqlSales = @"
                    SELECT 
                        ISNULL(COUNT(s.SaleID), 0) as Count, 
                        ISNULL(SUM(s.QuantitySold * m.BasePrice), 0) as Value
                    FROM SalesTransactions s
                    JOIN MenuItems m ON s.ProductID = m.ProductID
                    WHERE (s.TransactionDate BETWEEN @Start AND @End)
                    AND (@BranchID IS NULL OR s.BranchID = @BranchID)";

                var salesData = connection.QueryFirstOrDefault(sqlSales, p);
                if (salesData != null)
                {
                    metrics.TotalSalesCount = (int)salesData.Count;
                    metrics.TotalSalesValue = (decimal)salesData.Value;
                }

                // B. KPI: Inventory & Logistics (Snapshot - Filter by Branch Only)
                // Note: Inventory is current state, so date filter doesn't apply to stock levels
                string sqlStock = @"
                    SELECT COUNT(*) FROM BranchInventory 
                    WHERE CurrentQuantity <= LowStockThreshold 
                    AND (@BranchID IS NULL OR BranchID = @BranchID)";
                metrics.LowStockCount = connection.ExecuteScalar<int>(sqlStock, p);

                string sqlShip = @"
                    SELECT COUNT(*) FROM MeshLogistics 
                    WHERE (Status = 'In-Transit' OR Status = 'Pending') 
                    AND (@BranchID IS NULL OR ToBranchID = @BranchID)";
                metrics.PendingShipments = connection.ExecuteScalar<int>(sqlShip, p);

                // C. CHART: Branch Revenue (Time & Branch Filter)
                string sqlBranch = @"
                    SELECT TOP 5 b.BranchName, ISNULL(SUM(s.QuantitySold * m.BasePrice), 0) as TotalRevenue
                    FROM Branches b
                    LEFT JOIN SalesTransactions s ON b.BranchID = s.BranchID 
                        AND (s.TransactionDate BETWEEN @Start AND @End)
                    LEFT JOIN MenuItems m ON s.ProductID = m.ProductID
                    WHERE (@BranchID IS NULL OR b.BranchID = @BranchID)
                    GROUP BY b.BranchName
                    ORDER BY TotalRevenue DESC";
                metrics.BranchRanking = connection.Query<SLICE_System.Models.BranchPerformance>(sqlBranch, p).AsList();

                // D. CHART: Stock Health (Snapshot - Branch Filter)
                string sqlHealth = @"
                    SELECT b.BranchName,
                        COUNT(CASE WHEN bi.CurrentQuantity > bi.LowStockThreshold THEN 1 END) as GoodStock,
                        COUNT(CASE WHEN bi.CurrentQuantity <= bi.LowStockThreshold AND bi.CurrentQuantity > 0 THEN 1 END) as LowStock,
                        COUNT(CASE WHEN bi.CurrentQuantity <= 0 THEN 1 END) as CriticalStock
                    FROM Branches b
                    LEFT JOIN BranchInventory bi ON b.BranchID = bi.BranchID
                    WHERE (@BranchID IS NULL OR b.BranchID = @BranchID)
                    GROUP BY b.BranchName";
                metrics.BranchStockHealth = connection.Query<InventoryHealth>(sqlHealth, p).AsList();

                // E. CHART: Product Mix (Time & Branch Filter)
                string sqlProducts = @"
                    SELECT TOP 5 m.ProductName, ISNULL(SUM(s.QuantitySold), 0) as QuantitySold
                    FROM SalesTransactions s
                    JOIN MenuItems m ON s.ProductID = m.ProductID
                    WHERE (s.TransactionDate BETWEEN @Start AND @End)
                    AND (@BranchID IS NULL OR s.BranchID = @BranchID)
                    GROUP BY m.ProductName
                    ORDER BY QuantitySold DESC";
                metrics.TopProducts = connection.Query<ProductMix>(sqlProducts, p).AsList();

                // F. ALERT LIST (Snapshot - Branch Filter)
                string sqlAlerts = @"
                    SELECT TOP 20 b.BranchName, m.ProductName as ItemName, bi.CurrentQuantity as CurrentQty, bi.LowStockThreshold as Threshold
                    FROM BranchInventory bi
                    JOIN Branches b ON bi.BranchID = b.BranchID
                    JOIN MenuItems m ON bi.ItemID = m.ProductID -- Assuming ItemID maps to MenuItems for simplicity, check schema if MasterInventory used
                    WHERE bi.CurrentQuantity <= bi.LowStockThreshold
                    AND (@BranchID IS NULL OR b.BranchID = @BranchID)
                    ORDER BY bi.CurrentQuantity ASC";

                // NOTE: If using MasterInventory, change JOIN above to: JOIN MasterInventory m ON bi.ItemID = m.ItemID
                // Adjusted below to match your provided schema using MasterInventory:
                sqlAlerts = @"
                    SELECT TOP 20 b.BranchName, mi.ItemName, bi.CurrentQuantity as CurrentQty, bi.LowStockThreshold as Threshold
                    FROM BranchInventory bi
                    JOIN Branches b ON bi.BranchID = b.BranchID
                    JOIN MasterInventory mi ON bi.ItemID = mi.ItemID
                    WHERE bi.CurrentQuantity <= bi.LowStockThreshold
                    AND (@BranchID IS NULL OR b.BranchID = @BranchID)
                    ORDER BY bi.CurrentQuantity ASC";

                metrics.Alerts = connection.Query<LowStockAlert>(sqlAlerts, p).AsList();

                return metrics;
            }
        }

        // 3. RECENT ACTIVITY (Filtered)
        public List<RecentTransaction> GetRecentActivity(DateTime start, DateTime end, int? branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                var p = new { Start = start, End = end, BranchID = branchId };
                string sql = @"
                    SELECT TOP 10 
                        s.SaleID,
                        b.BranchName,
                        m.ProductName, 
                        s.QuantitySold,
                        s.TransactionDate,
                        (s.QuantitySold * m.BasePrice) as TotalAmount
                    FROM SalesTransactions s
                    JOIN Branches b ON s.BranchID = b.BranchID
                    JOIN MenuItems m ON s.ProductID = m.ProductID
                    WHERE (s.TransactionDate BETWEEN @Start AND @End)
                    AND (@BranchID IS NULL OR s.BranchID = @BranchID)
                    ORDER BY s.TransactionDate DESC";

                return connection.Query<RecentTransaction>(sql, p).AsList();
            }
        }
    }
}