using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SLICE_System.ViewModels
{
    public class PurchaseViewModel : ViewModelBase
    {
        private const int HEADQUARTERS_BRANCH_ID = 4;

        private readonly ProcurementRepository _procurementRepo;
        private readonly InventoryRepository _inventoryRepo;

        public string SupplierName { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.Now;

        public ObservableCollection<MasterInventory> AllIngredients { get; set; }
        public ObservableCollection<PurchaseDetail> CartItems { get; set; }

        public decimal GrandTotal => CartItems.Sum(x => x.Subtotal);

        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }
        public ICommand SaveCommand { get; }

        // NEW: Constructor now accepts the pre-selected list
        public PurchaseViewModel(List<MasterInventory> preSelectedItems = null)
        {
            _procurementRepo = new ProcurementRepository();
            _inventoryRepo = new InventoryRepository();

            CartItems = new ObservableCollection<PurchaseDetail>();
            AllIngredients = new ObservableCollection<MasterInventory>(_inventoryRepo.GetAllIngredients());

            AddRowCommand = new RelayCommand(AddRow);
            RemoveRowCommand = new RelayCommand<PurchaseDetail>(RemoveRow);
            SaveCommand = new RelayCommand(SavePurchase);

            // PRE-FILL CART LOGIC
            if (preSelectedItems != null && preSelectedItems.Count > 0)
            {
                foreach (var item in preSelectedItems)
                {
                    CartItems.Add(new PurchaseDetail
                    {
                        ItemID = item.ItemID,
                        Quantity = 1,
                        UnitPrice = 0
                    });
                }
                OnPropertyChanged(nameof(GrandTotal));
            }
            else
            {
                AddRow(); // Default behavior: 1 empty row if nothing was selected
            }
        }

        private void AddRow()
        {
            var newItem = new PurchaseDetail { Quantity = 1, UnitPrice = 0 };
            CartItems.Add(newItem);
            OnPropertyChanged(nameof(GrandTotal));
        }

        private void RemoveRow(PurchaseDetail item)
        {
            if (CartItems.Contains(item))
            {
                CartItems.Remove(item);
                OnPropertyChanged(nameof(GrandTotal));
            }
        }

        private void SavePurchase()
        {
            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                MessageBox.Show("Please enter a Supplier Name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!CartItems.Any(x => x.ItemID > 0 && x.Quantity > 0))
            {
                MessageBox.Show("Please add at least one valid item.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var header = new Purchase
                {
                    Supplier = SupplierName,
                    TotalAmount = GrandTotal,
                    BranchID = HEADQUARTERS_BRANCH_ID,
                    PurchaseDate = PurchaseDate,
                    PurchasedBy = 1
                };

                var validDetails = CartItems.Where(x => x.ItemID > 0 && x.Quantity > 0).ToList();

                _procurementRepo.ProcessPurchase(header, validDetails);

                MessageBox.Show("Purchase Recorded Successfully!\nStock added to Central Warehouse.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                CartItems.Clear();
                SupplierName = "";
                OnPropertyChanged(nameof(SupplierName));
                OnPropertyChanged(nameof(GrandTotal));
                AddRow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error recording purchase: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}