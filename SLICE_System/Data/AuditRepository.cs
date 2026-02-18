using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class AuditRepository
    {
        private readonly DatabaseService _dbService;

        public AuditRepository() => _dbService = new DatabaseService();

        // FIXED: Now accepts a search parameter (defaults to empty string)
        public List<AuditEntry> GetSystemHistory(string search = "")
        {
            using (var connection = _dbService.GetConnection())
            {
                // We wrap the UNION in a CTE (Common Table Expression) or Subquery
                // to allow filtering the combined results easily.
                string sql = @"
                    SELECT * FROM 
                    (
                        -- 1. SALES
                        SELECT 
                            s.TransactionDate as Timestamp, 
                            'SALE' as ActivityType, 
                            'Sold ' + CAST(ISNULL(s.QuantitySold, 1) AS VARCHAR) + 'x ' + ISNULL(m.ProductName, 'Unknown Item') as Description,
                            ISNULL(b.BranchName, 'Unknown Branch') as BranchName, 
                            'Store Staff' as PerformedBy
                        FROM SalesTransactions s
                        LEFT JOIN Branches b ON s.BranchID = b.BranchID
                        LEFT JOIN MenuItems m ON s.ProductID = m.ProductID
            
                        UNION ALL

                        -- 2. WASTE
                        SELECT 
                            DateRecorded as Timestamp, 
                            'WASTE' as ActivityType, 
                            'Reason: ' + Reason as Description, 
                            b.BranchName, 
                            u.FullName as PerformedBy
                        FROM WasteTracker w
                        JOIN Branches b ON w.BranchID = b.BranchID
                        JOIN Users u ON w.RecordedBy = u.UserID

                        UNION ALL

                        -- 3. LOGISTICS
                        SELECT 
                            SentDate as Timestamp, 
                            'SHIPMENT' as ActivityType, 
                            'Transfer #' + CAST(TransferID AS VARCHAR) as Description, 
                            b.BranchName, 
                            u.FullName as PerformedBy
                        FROM MeshLogistics m
                        JOIN Branches b ON m.FromBranchID = b.BranchID
                        JOIN Users u ON m.SenderID = u.UserID
                    ) AS AllActivity
                    WHERE (@SearchStr = '' 
                           OR Description LIKE @SearchStr 
                           OR BranchName LIKE @SearchStr 
                           OR PerformedBy LIKE @SearchStr)
                    ORDER BY Timestamp DESC";

                // Pass the search term with wildcards for SQL LIKE
                return connection.Query<AuditEntry>(sql, new { SearchStr = "%" + search + "%" }).AsList();
            }
        }
    }
}