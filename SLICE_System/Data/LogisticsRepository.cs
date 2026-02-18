using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_System.Models;

namespace SLICE_System.Data
{
    /// <summary>
    /// Handles the "Mesh Logistics" module.
    /// Implements the Three-Way Handshake:
    /// 1. RequestStock (Status: Pending) - Created by Requester
    /// 2. ApproveRequest (Status: In-Transit) - Approved by Sender (Stock Deducted)
    /// 3. ReceiveShipment (Status: Completed) - Confirmed by Receiver (Stock Added)
    /// </summary>
    public class LogisticsRepository
    {
        private readonly DatabaseService _dbService;

        public LogisticsRepository()
        {
            _dbService = new DatabaseService();
        }

        // =========================================================
        // HANDSHAKE STEP 1: CREATE REQUEST
        // (A Branch asks another Branch for items)
        // =========================================================

        public void RequestStock(MeshLogistics header, List<WaybillDetail> items)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Create the Header (Status: Pending)
                        string sqlHeader = @"
                            INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, ReceiverID, SentDate)
                            VALUES (@FromBranchID, @ToBranchID, 'Pending', @ReceiverID, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int newTransferId = connection.ExecuteScalar<int>(sqlHeader, header, transaction);

                        // 2. Insert the Details (Items requested)
                        string sqlDetail = @"INSERT INTO WaybillDetails (TransferID, ItemID, Quantity) VALUES (@TransferID, @ItemID, @Quantity)";

                        foreach (var item in items)
                        {
                            item.TransferID = newTransferId;
                            connection.Execute(sqlDetail, item, transaction);
                        }

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

        // =========================================================
        // HANDSHAKE STEP 2: APPROVE & SHIP
        // (The Sender approves the request and ships the items)
        // =========================================================

        /// <summary>
        /// Gets requests that are either Pending Action or currently In-Transit.
        /// They only disappear when status is 'Completed'.
        /// </summary>
        public List<MeshLogistics> GetPendingRequests(int myBranchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT m.*, 
                           b.BranchName AS ToBranchName, 
                           (SELECT SUM(Quantity) FROM WaybillDetails WHERE TransferID = m.TransferID) AS TotalQuantity
                    FROM MeshLogistics m
                    INNER JOIN Branches b ON m.ToBranchID = b.BranchID
                    WHERE m.FromBranchID = @BranchID 
                      AND m.Status IN ('Pending', 'In-Transit') 
                    ORDER BY 
                        CASE WHEN m.Status = 'Pending' THEN 1 ELSE 2 END, -- Prioritize Pending items
                        m.SentDate";

                return connection.Query<MeshLogistics>(sql, new { BranchID = myBranchId }).ToList();
            }
        }

        public void ApproveRequest(int transferId, int managerId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Get Header Info
                        string sqlHeader = "SELECT * FROM MeshLogistics WHERE TransferID = @Id";
                        var header = connection.QuerySingleOrDefault<MeshLogistics>(sqlHeader, new { Id = transferId }, transaction);

                        if (header == null) throw new Exception("Transfer record not found.");
                        // Allow re-approving only if it was pending (safety check)
                        if (header.Status != "Pending") throw new Exception("This request has already been processed.");

                        // 2. Get Details (Items to ship)
                        string sqlDetails = "SELECT * FROM WaybillDetails WHERE TransferID = @Id";
                        var items = connection.Query<WaybillDetail>(sqlDetails, new { Id = transferId }, transaction).ToList();

                        // 3. Validation & Deduction Loop
                        string sqlCheckStock = "SELECT CurrentQuantity FROM BranchInventory WHERE BranchID = @BranchID AND ItemID = @ItemID";
                        string sqlDeductStock = "UPDATE BranchInventory SET CurrentQuantity = CurrentQuantity - @Qty WHERE BranchID = @BranchID AND ItemID = @ItemID";

                        foreach (var item in items)
                        {
                            // A. Check if Sender has enough stock
                            decimal currentStock = connection.ExecuteScalar<decimal>(sqlCheckStock,
                                new { BranchID = header.FromBranchID, ItemID = item.ItemID }, transaction);

                            if (currentStock < item.Quantity)
                            {
                                throw new Exception($"Insufficient stock for Item ID {item.ItemID}. Available: {currentStock}, Requested: {item.Quantity}");
                            }

                            // B. Deduct Stock (Inventory leaves the Sender)
                            connection.Execute(sqlDeductStock,
                                new { BranchID = header.FromBranchID, ItemID = item.ItemID, Qty = item.Quantity }, transaction);
                        }

                        // 4. Update Status to 'In-Transit'
                        string sqlUpdateStatus = @"
                            UPDATE MeshLogistics 
                            SET Status = 'In-Transit', 
                                SenderID = @SenderID, 
                                SentDate = GETDATE() 
                            WHERE TransferID = @Id";

                        connection.Execute(sqlUpdateStatus, new { SenderID = managerId, Id = transferId }, transaction);

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

        // =========================================================
        // HANDSHAKE STEP 3: RECEIVE SHIPMENT
        // (The Requester receives the items and adds them to stock)
        // =========================================================

        public List<MeshLogistics> GetIncomingShipments(int myBranchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // Receiver only sees items that are officially 'In-Transit'
                string sql = @"
                    SELECT m.*, 
                           b.BranchName AS FromBranchName,
                           ISNULL((SELECT SUM(Quantity) FROM WaybillDetails WHERE TransferID = m.TransferID), 0) AS TotalQuantity
                    FROM MeshLogistics m
                    INNER JOIN Branches b ON m.FromBranchID = b.BranchID
                    WHERE m.ToBranchID = @BranchID AND m.Status = 'In-Transit'
                    ORDER BY m.SentDate DESC";

                return connection.Query<MeshLogistics>(sql, new { BranchID = myBranchId }).ToList();
            }
        }

        public void ReceiveShipment(int transferId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Get Items
                        var items = connection.Query<WaybillDetail>("SELECT * FROM WaybillDetails WHERE TransferID = @Id", new { Id = transferId }, transaction).ToList();
                        var header = connection.QuerySingle<MeshLogistics>("SELECT * FROM MeshLogistics WHERE TransferID = @Id", new { Id = transferId }, transaction);

                        if (header.Status != "In-Transit") throw new Exception("Shipment is not in transit or already received.");

                        // 2. Add Stock to Receiver
                        string sqlAddStock = @"
                            UPDATE BranchInventory 
                            SET CurrentQuantity = CurrentQuantity + @Qty
                            WHERE BranchID = @BranchID AND ItemID = @ItemID";

                        foreach (var item in items)
                        {
                            connection.Execute(sqlAddStock, new { Qty = item.Quantity, BranchID = header.ToBranchID, ItemID = item.ItemID }, transaction);
                        }

                        // 3. Mark Completed (This is when it disappears from Pending Requests)
                        string sqlComplete = "UPDATE MeshLogistics SET Status = 'Completed', ReceivedDate = GETDATE() WHERE TransferID = @Id";
                        connection.Execute(sqlComplete, new { Id = transferId }, transaction);

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

        // =========================================================
        // UTILITY METHODS
        // =========================================================

        public List<WaybillDetail> GetTransferDetails(int transferId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT d.*, m.ItemName, m.BaseUnit 
                    FROM WaybillDetails d
                    INNER JOIN MasterInventory m ON d.ItemID = m.ItemID
                    WHERE d.TransferID = @TransferID";

                return connection.Query<WaybillDetail>(sql, new { TransferID = transferId }).ToList();
            }
        }
    }
}