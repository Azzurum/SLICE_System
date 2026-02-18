using System;
using System.Windows;
using System.Windows.Controls;
using SLICE_System.Data;

namespace SLICE_System.Views
{
    public partial class BranchStockView : UserControl
    {
        private int _branchId;
        private InventoryRepository _repo;

        public BranchStockView(int branchId)
        {
            InitializeComponent();
            _branchId = branchId;
            _repo = new InventoryRepository();
            LoadStock();
        }

        private void LoadStock()
        {
            try
            {
                // Fetch the live inventory list for this branch
                var stockList = _repo.GetStockForBranch(_branchId);

                // Bind it to the DataGrid
                dgStock.ItemsSource = stockList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory: {ex.Message}", "System Error");
            }
        }

        // Allow manual refresh if the user wants to check for new updates
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStock();
        }
    }
}