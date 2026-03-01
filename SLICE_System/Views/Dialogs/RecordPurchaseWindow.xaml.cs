using System.Windows;
using SLICE_System.ViewModels;

namespace SLICE_System.Views.Dialogs
{
    public partial class RecordPurchaseWindow : Window
    {
        public RecordPurchaseWindow()
        {
            InitializeComponent();

            // Bind the window to the ViewModel
            var vm = new PurchaseViewModel();
            this.DataContext = vm;
        }

        // Cleanly close the window when the user hits the Confirm button
        // Note: The actual database saving is handled safely by the ViewModel's SaveCommand
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var vm = (PurchaseViewModel)this.DataContext;

            // Only close if the cart has items and a supplier is named
            if (vm.CartItems.Count > 0 && !string.IsNullOrWhiteSpace(vm.SupplierName))
            {
                MessageBox.Show("Procurement successfully recorded and stock updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please provide a Supplier Name and add at least one item.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}