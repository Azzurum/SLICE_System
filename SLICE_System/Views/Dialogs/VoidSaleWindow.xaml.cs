using System.Windows;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views.Dialogs
{
    public partial class VoidSaleWindow : Window
    {
        private int _branchId;
        private SalesRepository _repo;

        public VoidSaleWindow(int branchId)
        {
            InitializeComponent();
            _branchId = branchId;
            _repo = new SalesRepository();
            LoadSales();
        }

        private void LoadSales()
        {
            dgSales.ItemsSource = _repo.GetTodaySales(_branchId);
        }

        private void Void_Click(object sender, RoutedEventArgs e)
        {
            if (dgSales.SelectedItem is SaleRecord selectedSale)
            {
                if (string.IsNullOrWhiteSpace(txtReason.Text))
                {
                    MessageBox.Show("You must provide a reason for voiding this transaction.", "Missing Reason", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1. Prompt for Manager Override
                var authWindow = new ManagerApprovalWindow { Owner = this };
                if (authWindow.ShowDialog() == true)
                {
                    // 2. Process Void
                    string error;
                    bool success = _repo.VoidSale(selectedSale.SaleID, authWindow.ApprovedManagerID, txtReason.Text, out error);

                    if (success)
                    {
                        MessageBox.Show("Transaction successfully voided. Inventory and financial ledgers have been updated.", "Void Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadSales(); // Refresh the list
                    }
                    else
                    {
                        MessageBox.Show($"Failed to void transaction: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a transaction to void.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}