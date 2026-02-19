using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SLICE_System.ViewModels
{
    public class PurchaseViewModel : ViewModelBase
    {
        private readonly ProcurementRepository _procurementRepo;
        private readonly InventoryRepository _inventoryRepo;

        // --- Header Data ---
        public string SupplierName { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.Now;

        // --- Line Items ---
        public ObservableCollection<MasterInventory> AllIngredients { get; set; }
        public ObservableCollection<PurchaseDetail> CartItems { get; set; }

        // --- Computed Totals ---
        public decimal GrandTotal => CartItems.Sum(x => x.Subtotal);

        // --- Commands ---
        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }
        public ICommand SaveCommand { get; }

        public PurchaseViewModel()
        {
            _procurementRepo = new ProcurementRepository();
            _inventoryRepo = new InventoryRepository();

            CartItems = new ObservableCollection<PurchaseDetail>();
            AllIngredients = new ObservableCollection<MasterInventory>(_inventoryRepo.GetAllIngredients());

            AddRowCommand = new RelayCommand(AddRow);
            RemoveRowCommand = new RelayCommand<PurchaseDetail>(RemoveRow);
            SaveCommand = new RelayCommand(SavePurchase);

            AddRow(); // Start with one empty row
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
            // 1. Validation (Branch validation removed)
            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                MessageBox.Show("Please enter a Supplier Name.");
                return;
            }
            if (!CartItems.Any(x => x.ItemID > 0 && x.Quantity > 0))
            {
                MessageBox.Show("Please add at least one valid item.");
                return;
            }

            try
            {
                // 2. Build Header
                var header = new Purchase
                {
                    Supplier = SupplierName,
                    TotalAmount = GrandTotal,
                    BranchID = 1, // STRICT ENFORCEMENT: All purchases go to Head Office (Branch 1)
                    PurchaseDate = PurchaseDate,
                    PurchasedBy = 1 // TODO: Bind to CurrentUser
                };

                var validDetails = CartItems.Where(x => x.ItemID > 0 && x.Quantity > 0).ToList();

                // 3. Commit via Repository
                _procurementRepo.ProcessPurchase(header, validDetails);

                MessageBox.Show("Purchase Recorded Successfully!\nStock added to Head Office and Expense Logged.", "Success");

                // Clear UI
                CartItems.Clear();
                SupplierName = "";
                OnPropertyChanged(nameof(SupplierName));
                OnPropertyChanged(nameof(GrandTotal));
                AddRow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error recording purchase: {ex.Message}");
            }
        }
    }
}