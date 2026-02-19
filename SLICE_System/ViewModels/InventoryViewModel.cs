using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;
using SLICE_System.Views.Dialogs;
using System;
using System.Collections; // Required for IList
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

        public ICommand RefreshCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand DispatchCommand { get; }

        // NEW: Command to open the purchase window
        public ICommand RecordPurchaseCommand { get; }

        // --- CONSTRUCTOR ---
        public InventoryViewModel()
        {
            _repo = new InventoryRepository();
            _db = new DatabaseService();
            Ingredients = new ObservableCollection<MasterInventory>();

            RefreshCommand = new RelayCommand(LoadData);

            AddItemCommand = new RelayCommand(AddItem);

            // UPDATED: DispatchCommand now accepts a parameter (the list of selected items)
            DispatchCommand = new RelayCommand<IList>(DispatchStock);

            // NEW: Initialize the purchase command
            RecordPurchaseCommand = new RelayCommand(OpenPurchaseWindow);

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
            var popup = new SLICE_System.Views.Dialogs.AddIngredientWindow();

            if (popup.ShowDialog() == true)
            {
                LoadData();
                MessageBox.Show("New Ingredient Added Successfully!");
            }
        }

        // --- NEW PURCHASE LOGIC ---
        private void OpenPurchaseWindow()
        {
            var win = new SLICE_System.Views.Dialogs.RecordPurchaseWindow();
            win.ShowDialog();

            // Refresh inventory after the purchase window is closed
            // to reflect newly added stock in the UI
            LoadData();
        }

        // --- UPDATED DISPATCH LOGIC ---
        private void DispatchStock(IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one item to dispatch.");
                return;
            }

            // 1. Prepare the list for the Dialog
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
                        Quantity = 0 // User must enter this
                    });
                }
            }

            // 2. Open the Multi-Item Dispatch Dialog
            var dialog = new DispatchDialog(dispatchList);

            if (dialog.ShowDialog() == true)
            {
                // Filter out items with 0 quantity to avoid cluttering the database
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
                                // A. Create the Transfer Header (The "Box")
                                string sqlHeader = @"
                                    INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, SenderID, SentDate)
                                    VALUES (1, @TargetBranchID, 'In-Transit', 1, GETDATE());
                                    SELECT SCOPE_IDENTITY();";

                                int transferId = conn.ExecuteScalar<int>(sqlHeader, new { TargetBranchID = dialog.SelectedBranchID }, transaction);

                                // B. Insert Details (The "Contents")
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
                    MessageBox.Show($"Successfully dispatched {itemsToSend.Count} distinct items to Branch #{dialog.SelectedBranchID}!", "Success");

                    // Optional: Deselect or Refresh
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dispatch failed: {ex.Message}");
                }
            }
        }
    }

    // --- HELPER CLASS ---
    public class DispatchItemModel
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
    }
}