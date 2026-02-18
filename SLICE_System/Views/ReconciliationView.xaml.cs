using System.Collections.Generic;
using System.Linq; // For conversion
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel; // For INotifyPropertyChanged
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
                ItemName = x.ItemName,
                SystemQty = x.SystemQty,
                PhysicalQty = x.PhysicalQty // Default usually same or 0
            }).ToList();

            dgRecon.ItemsSource = viewModels;
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            var items = dgRecon.ItemsSource as List<ReconItemVM>;
            if (items == null) return;

            foreach (var item in items)
            {
                // Logic: Always save the adjustment record
                _repo.SaveAdjustment(item.StockID, item.SystemQty, item.PhysicalQty, _userId);
            }

            MessageBox.Show("Inventory Audit Completed.\nVariances have been logged.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadData(); // Refresh to clean state
        }

        // --- VIEW MODEL HELPER (For Dynamic UI Feedback) ---
        public class ReconItemVM : INotifyPropertyChanged
        {
            public int StockID { get; set; }
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
                        OnPropertyChanged(nameof(IsMatched)); // Trigger UI update
                    }
                }
            }

            // Logic for the Green/Orange Badge
            public bool IsMatched => SystemQty == PhysicalQty;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}