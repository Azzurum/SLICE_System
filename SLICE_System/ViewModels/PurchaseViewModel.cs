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
        public ObservableCollection<Branch> Branches { get; set; }
        public Branch SelectedBranch { get; set; }

        // --- Line Items ---
        public ObservableCollection<MasterInventory> AllIngredients { get; set; } // For Dropdown
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
            LoadData();

            AddRowCommand = new RelayCommand(AddRow);
            RemoveRowCommand = new RelayCommand<PurchaseDetail>(RemoveRow);
            SaveCommand = new RelayCommand(SavePurchase);

            // Start with one empty row
            AddRow();
        }

        private void LoadData()
        {
            Branches = new ObservableCollection<Branch>(_inventoryRepo.GetAllBranches());
            // Default to Head Office or first branch
            SelectedBranch = Branches.FirstOrDefault();

            AllIngredients = new ObservableCollection<MasterInventory>(_inventoryRepo.GetAllIngredients());
        }

        private void AddRow()
        {
            var newItem = new PurchaseDetail { Quantity = 1, UnitPrice = 0 };
            // Hook up property change to update GrandTotal when user types
            // Note: In a full MVVM framework like PRISM, this is automatic. 
            // Here we rely on the DataGrid committing edits.
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

        public void RecalculateTotal()
        {
            OnPropertyChanged(nameof(GrandTotal));
        }

        private void SavePurchase()
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                MessageBox.Show("Please enter a Supplier Name.");
                return;
            }
            if (SelectedBranch == null)
            {
                MessageBox.Show("Please select a target Branch.");
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
                    BranchID = SelectedBranch.BranchID,
                    PurchaseDate = PurchaseDate,
                    PurchasedBy = 1 // TODO: Replace with CurrentUser.UserID
                };

                // 3. Filter Valid Rows
                var validDetails = CartItems.Where(x => x.ItemID > 0 && x.Quantity > 0).ToList();

                // 4. Commit via Repository
                _procurementRepo.ProcessPurchase(header, validDetails);

                MessageBox.Show("Purchase Recorded Successfully!\nInventory Updated & Expense Logged.", "Success");

                // Close Window (Handled by View Code-Behind usually, or messaging)
                // For simplicity, we just clear the form here
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