using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;
using SLICE_System.Views;
using SLICE_System.Views.Dialogs;
using System;
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

        // --- CONSTRUCTOR ---
        public InventoryViewModel()
        {
            _repo = new InventoryRepository();
            _db = new DatabaseService();
            Ingredients = new ObservableCollection<MasterInventory>();

            RefreshCommand = new RelayCommand(LoadData);

            // RESTORED: Logic to open the Add Window
            AddItemCommand = new RelayCommand(AddItem);

            DispatchCommand = new RelayCommand(DispatchStock, () => SelectedIngredient != null);

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

        // --- RESTORED FEATURE ---
        private void AddItem()
        {
            // FIX: Use the specific namespace 'Views.Dialogs' to avoid confusion
            var popup = new SLICE_System.Views.Dialogs.AddIngredientWindow();

            if (popup.ShowDialog() == true)
            {
                LoadData();
                MessageBox.Show("New Ingredient Added Successfully!");
            }
        }

        private void DispatchStock()
        {
            if (SelectedIngredient == null) return;

            var dialog = new DispatchDialog(SelectedIngredient.ItemName, SelectedIngredient.BulkUnit);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = _db.GetConnection())
                    {
                        string sql = @"
                            INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, SenderID, SentDate)
                            VALUES (1, @TargetBranchID, 'In-Transit', 1, GETDATE());
                            
                            DECLARE @NewTransferID INT = SCOPE_IDENTITY();

                            INSERT INTO WaybillDetails (TransferID, ItemID, Quantity)
                            VALUES (@NewTransferID, @ItemID, @Qty);";

                        conn.Execute(sql, new
                        {
                            TargetBranchID = dialog.SelectedBranchID,
                            ItemID = SelectedIngredient.ItemID,
                            Qty = dialog.Quantity
                        });
                    }
                    MessageBox.Show($"Successfully dispatched {dialog.Quantity} {SelectedIngredient.BulkUnit} of {SelectedIngredient.ItemName}!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dispatch failed: {ex.Message}");
                }
            }
        }
    }
}