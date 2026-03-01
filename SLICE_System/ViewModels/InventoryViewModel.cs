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
        private readonly InventoryRepository _repo;
        private readonly DatabaseService _db;
        private MasterInventory _selectedItem;
        private string _searchText;

        // --- PROPERTIES ---
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

        // --- COMMANDS ---
        public ICommand RefreshCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand DispatchCommand { get; }
        public ICommand RecordPurchaseCommand { get; }

        // NEW: Action Commands for the DataGrid Rows
        public ICommand EditItemCommand { get; }
        public ICommand DeleteItemCommand { get; }

        // --- CONSTRUCTOR ---
        public InventoryViewModel()
        {
            _repo = new InventoryRepository();
            _db = new DatabaseService();
            Ingredients = new ObservableCollection<MasterInventory>();

            // Initialize Commands
            RefreshCommand = new RelayCommand(LoadData);
            AddItemCommand = new RelayCommand(AddItem);
            DispatchCommand = new RelayCommand<IList>(DispatchStock);
            RecordPurchaseCommand = new RelayCommand(OpenPurchaseWindow);

            // NEW: Row Actions
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
            var popup = new AddIngredientWindow(); // Standard empty window
            if (popup.ShowDialog() == true)
            {
                LoadData();
            }
        }

        // --- NEW: EDIT LOGIC ---
        private void EditItem(MasterInventory item)
        {
            if (item == null) return;

            // Use the full namespace to ensure it hits the Dialogs version with the custom constructor
            var popup = new SLICE_System.Views.Dialogs.AddIngredientWindow(item);

            if (popup.ShowDialog() == true)
            {
                LoadData();
            }
        }

        // --- NEW: DELETE LOGIC ---
        private void DeleteItem(MasterInventory item)
        {
            if (item == null) return;

            // 1. Confirm with the user
            var result = MessageBox.Show(
                $"Are you sure you want to remove '{item.ItemName}'? This will permanently delete the item definition.",
                "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = _db.GetConnection())
                {
                    // 2. SAFETY CHECK: Check if this item is used in Recipes or has Stock in Branches
                    string sqlCheck = @"
                SELECT 
                    (SELECT COUNT(*) FROM BillOfMaterials WHERE ItemID = @Id) +
                    (SELECT COUNT(*) FROM BranchInventory WHERE ItemID = @Id AND CurrentQuantity > 0) 
                AS UsageCount";

                    int usage = conn.ExecuteScalar<int>(sqlCheck, new { Id = item.ItemID });

                    if (usage > 0)
                    {
                        // BLOCK THE DELETE: Explain that it's in use
                        MessageBox.Show(
                            $"Cannot delete '{item.ItemName}' because it is currently used in a Menu Recipe or still has remaining stock in one or more branches.\n\nPlease remove it from all recipes and empty the stock before deleting.",
                            "Item In Use", MessageBoxButton.OK, MessageBoxImage.Stop);
                        return;
                    }

                    // 3. IF SAFE: Proceed with Deletion
                    // We also delete empty stock records (0 qty) to clean up the DB
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

        private void OpenPurchaseWindow()
        {
            var win = new RecordPurchaseWindow();
            win.ShowDialog();
            LoadData();
        }

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
                if (itemsToSend.Count == 0) return;

                try
                {
                    using (var conn = _db.GetConnection())
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                string sqlHeader = @"
                                    INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, SenderID, SentDate)
                                    VALUES (1, @TargetBranchID, 'In-Transit', 1, GETDATE());
                                    SELECT SCOPE_IDENTITY();";

                                int transferId = conn.ExecuteScalar<int>(sqlHeader, new { TargetBranchID = dialog.SelectedBranchID }, transaction);

                                string sqlDetail = @"
                                    INSERT INTO WaybillDetails (TransferID, ItemID, Quantity)
                                    VALUES (@TransferID, @ItemID, @Qty);";

                                foreach (var item in itemsToSend)
                                {
                                    conn.Execute(sqlDetail, new
                                    {
                                        TransferID = transferId,
                                        ItemID = item.ItemID,
                                        Qty = item.Quantity
                                    }, transaction);
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
                    MessageBox.Show($"Successfully dispatched {itemsToSend.Count} items.", "Logistics Success");
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dispatch failed: {ex.Message}");
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