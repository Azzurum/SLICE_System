using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class ReconciliationView : UserControl
    {
        private int _branchId;
        private int _userId;
        private InventoryRepository _repo;

        public ReconciliationView(int branchId, int userId)
        {
            InitializeComponent();
            _branchId = branchId;
            _userId = userId;
            _repo = new InventoryRepository();
            LoadData();
        }

        private void LoadData()
        {
            var rawData = _repo.GetReconciliationSheet(_branchId);

            // Convert to View Model for dynamic "IsMatched" property
            var viewModels = rawData.Select(x => new ReconItemVM
            {
                StockID = x.StockID,
                BranchID = x.BranchID,  // NEW: Needed for P&L tracking
                ItemID = x.ItemID,      // NEW: Needed for P&L tracking
                ItemName = x.ItemName,
                SystemQty = x.SystemQty,
                // Default to SystemQty so they only edit what is different
                PhysicalQty = x.SystemQty
            }).ToList();

            dgRecon.ItemsSource = viewModels;
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            var items = dgRecon.ItemsSource as List<ReconItemVM>;
            if (items == null || items.Count == 0) return;

            // Security/UX: Warn the manager that this impacts financials
            if (MessageBox.Show("Finalize this physical audit? Any missing stock will be permanently logged as a Leakage Expense on the Financial Ledger.",
                                "Confirm Audit", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                int variancesFound = 0;

                foreach (var item in items)
                {
                    // OPTIMIZATION: Only save to the database if there is an actual discrepancy!
                    if (item.PhysicalQty != item.SystemQty)
                    {
                        // Updated to match the new signature from Step 1
                        _repo.SaveAdjustment(item.StockID, item.BranchID, item.ItemID, item.SystemQty, item.PhysicalQty, _userId);
                        variancesFound++;
                    }
                }

                MessageBox.Show($"Inventory Audit Completed.\n{variancesFound} variances have been logged and synced with the financial ledger.",
                                "Audit Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadData(); // Refresh the grid to a clean state
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Audit Failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- VIEW MODEL HELPER (For Dynamic UI Feedback) ---
        public class ReconItemVM : INotifyPropertyChanged
        {
            public int StockID { get; set; }
            public int BranchID { get; set; } // Added
            public int ItemID { get; set; }   // Added
            public string ItemName { get; set; }
            public decimal SystemQty { get; set; }

            private decimal _physicalQty;
            public decimal PhysicalQty
            {
                get { return _physicalQty; }
                set
                {
                    if (_physicalQty != value)
                    {
                        _physicalQty = value;
                        OnPropertyChanged(nameof(PhysicalQty));
                        OnPropertyChanged(nameof(IsMatched)); // Triggers XAML UI to turn Green/Orange
                    }
                }
            }

            // Logic for the Green/Orange Badge in XAML
            public bool IsMatched => SystemQty == PhysicalQty;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}