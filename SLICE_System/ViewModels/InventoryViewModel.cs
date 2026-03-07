using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;
using SLICE_System.Views.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SLICE_System.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        // Central Warehouse Database ID
        private const int HEADQUARTERS_BRANCH_ID = 4;

        private readonly InventoryRepository _repo;
        private readonly DatabaseService _db;
        private MasterInventory _selectedItem;
        private string _searchText;

        public ObservableCollection<MasterInventory> Ingredients { get; set; }

        public MasterInventory SelectedIngredient
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    LoadData();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand DispatchCommand { get; }
        public ICommand RecordPurchaseCommand { get; }
        public ICommand EditItemCommand { get; }
        public ICommand DeleteItemCommand { get; }

        public InventoryViewModel()
        {
            _repo = new InventoryRepository();
            _db = new DatabaseService();
            Ingredients = new ObservableCollection<MasterInventory>();

            RefreshCommand = new RelayCommand(LoadData);
            AddItemCommand = new RelayCommand(AddItem);

            // Commands now expect an IList of highlighted DataGrid items
            DispatchCommand = new RelayCommand<IList>(DispatchStock);
            RecordPurchaseCommand = new RelayCommand<IList>(OpenPurchaseWindow);

            EditItemCommand = new RelayCommand<MasterInventory>(EditItem);
            DeleteItemCommand = new RelayCommand<MasterInventory>(DeleteItem);

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var list = _repo.GetAllIngredients(SearchText ?? "");
                Ingredients.Clear();
                foreach (var item in list) Ingredients.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
            }
        }

        private void AddItem()
        {
            var popup = new AddIngredientWindow();
            if (popup.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void EditItem(MasterInventory item)
        {
            if (item == null) return;
            var popup = new SLICE_System.Views.Dialogs.AddIngredientWindow(item);
            if (popup.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void DeleteItem(MasterInventory item)
        {
            if (item == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to remove '{item.ItemName}'? This will permanently delete the item definition.",
                "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = _db.GetConnection())
                {
                    // Block deletion if the item is linked to recipes or still exists in physical stock
                    string sqlCheck = @"
                SELECT 
                    (SELECT COUNT(*) FROM BillOfMaterials WHERE ItemID = @Id) +
                    (SELECT COUNT(*) FROM BranchInventory WHERE ItemID = @Id AND CurrentQuantity > 0) 
                AS UsageCount";

                    int usage = conn.ExecuteScalar<int>(sqlCheck, new { Id = item.ItemID });

                    if (usage > 0)
                    {
                        MessageBox.Show(
                            $"Cannot delete '{item.ItemName}' because it is currently used in a Menu Recipe or still has remaining stock in one or more branches.\n\nPlease remove it from all recipes and empty the stock before deleting.",
                            "Item In Use", MessageBoxButton.OK, MessageBoxImage.Stop);
                        return;
                    }

                    conn.Execute("DELETE FROM BranchInventory WHERE ItemID = @Id", new { Id = item.ItemID });
                    conn.Execute("DELETE FROM MasterInventory WHERE ItemID = @Id", new { Id = item.ItemID });

                    LoadData();
                    MessageBox.Show("Ingredient successfully removed from the Central Warehouse.", "Success");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deletion failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Opens the Procurement window and pre-fills selected items
        private void OpenPurchaseWindow(IList selectedItems)
        {
            var preSelected = new List<MasterInventory>();

            if (selectedItems != null)
            {
                foreach (var item in selectedItems)
                {
                    if (item is MasterInventory mi) preSelected.Add(mi);
                }
            }

            var win = new RecordPurchaseWindow(preSelected);
            win.ShowDialog();
            LoadData();
        }

        // Handles sending bulk stock to one or more branches
        private void DispatchStock(IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one item to dispatch.");
                return;
            }

            var dispatchList = new List<DispatchItemModel>();
            foreach (var item in selectedItems)
            {
                if (item is MasterInventory inventoryItem)
                {
                    dispatchList.Add(new DispatchItemModel
                    {
                        ItemID = inventoryItem.ItemID,
                        ItemName = inventoryItem.ItemName,
                        Unit = inventoryItem.BulkUnit,
                        Quantity = 0
                    });
                }
            }

            var dialog = new DispatchDialog(dispatchList);

            if (dialog.ShowDialog() == true)
            {
                var itemsToSend = dialog.ItemsToDispatch.Where(x => x.Quantity > 0).ToList();
                var targetBranches = dialog.SelectedBranchIDs; // Fetches multiple selected branches

                if (itemsToSend.Count == 0 || targetBranches.Count == 0) return;

                try
                {
                    using (var conn = _db.GetConnection())
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                // STEP 1: Verify warehouse has enough total stock for all combined branches
                                foreach (var item in itemsToSend)
                                {
                                    string sqlCheckStock = "SELECT ISNULL(CurrentQuantity, 0) FROM BranchInventory WHERE BranchID = @HQ AND ItemID = @ItemID";
                                    decimal currentStock = conn.ExecuteScalar<decimal>(sqlCheckStock, new { HQ = HEADQUARTERS_BRANCH_ID, ItemID = item.ItemID }, transaction);

                                    decimal totalRequired = item.Quantity * targetBranches.Count;

                                    if (currentStock < totalRequired)
                                    {
                                        throw new Exception($"You cannot dispatch {totalRequired} units of {item.ItemName} ({item.Quantity} x {targetBranches.Count} branches). The warehouse only has {currentStock} units available!");
                                    }
                                }

                                // STEP 2: Create separate logistics records for each selected branch
                                foreach (int targetBranchId in targetBranches)
                                {
                                    string sqlHeader = @"
                                        INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, SenderID, SentDate)
                                        VALUES (@HQ, @TargetBranchID, 'In-Transit', 1, GETDATE());
                                        SELECT SCOPE_IDENTITY();";

                                    int transferId = conn.ExecuteScalar<int>(sqlHeader, new { HQ = HEADQUARTERS_BRANCH_ID, TargetBranchID = targetBranchId }, transaction);

                                    foreach (var item in itemsToSend)
                                    {
                                        // Deduct specific branch quantity from Headquarters
                                        string sqlDeduct = "UPDATE BranchInventory SET CurrentQuantity = CurrentQuantity - @Qty WHERE BranchID = @HQ AND ItemID = @ItemID";
                                        conn.Execute(sqlDeduct, new { HQ = HEADQUARTERS_BRANCH_ID, ItemID = item.ItemID, Qty = item.Quantity }, transaction);

                                        // Attach item to this specific branch's waybill
                                        string sqlDetail = "INSERT INTO WaybillDetails (TransferID, ItemID, Quantity) VALUES (@TransferID, @ItemID, @Qty);";
                                        conn.Execute(sqlDetail, new { TransferID = transferId, ItemID = item.ItemID, Qty = item.Quantity }, transaction);
                                    }
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
                    MessageBox.Show($"Successfully dispatched items to {targetBranches.Count} branch(es). Stock has been deducted from the warehouse.", "Logistics Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dispatch failed: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    public class DispatchItemModel
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
    }
}