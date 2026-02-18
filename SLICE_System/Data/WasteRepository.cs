using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    public class WasteRepository
    {
        private readonly DatabaseService _dbService;

        public WasteRepository()
        {
            _dbService = new DatabaseService();
        }

        // 1. RECORD WASTE
        public void RecordWaste(WasteRecord waste)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    INSERT INTO WasteTracker (BranchID, ItemID, QtyWasted, Reason, RecordedBy, DateRecorded)
                    VALUES (@BranchID, @ItemID, @QtyWasted, @Reason, @RecordedBy, GETDATE());

                    -- DEDUCT FROM INVENTORY IMMEDIATELY
                    UPDATE BranchInventory 
                    SET CurrentQuantity = CurrentQuantity - @QtyWasted
                    WHERE BranchID = @BranchID AND ItemID = @ItemID";

                connection.Execute(sql, waste);
            }
        }

        // 2. GET RECENT LOGS (For the UI List)
        public List<WasteRecord> GetRecentWaste(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT TOP 20 w.*, m.ItemName, u.FullName as RecordedByName
                    FROM WasteTracker w
                    INNER JOIN MasterInventory m ON w.ItemID = m.ItemID
                    LEFT JOIN Users u ON w.RecordedBy = u.UserID
                    WHERE w.BranchID = @BranchID
                    ORDER BY w.DateRecorded DESC";

                return connection.Query<WasteRecord>(sql, new { BranchID = branchId }).ToList();
            }
        }
    }
}