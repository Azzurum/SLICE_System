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

        // --- INLINE EDITING LOGIC ---
        private void dgStock_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Check the underlying data property (LowStockThreshold) instead of the visual Header text
            if (e.EditAction == DataGridEditAction.Commit && e.Column.SortMemberPath == "LowStockThreshold")
            {
                var textBox = e.EditingElement as TextBox;
                var selectedItem = e.Row.Item as SLICE_System.Models.BranchInventoryItem;

                if (textBox != null && selectedItem != null)
                {
                    // Allow parsing of numbers even if the user types commas (e.g., 1,500)
                    if (decimal.TryParse(textBox.Text, System.Globalization.NumberStyles.Any, null, out decimal newThreshold))
                    {
                        try
                        {
                            // 1. Save to Database
                            _repo.UpdateLowStockThreshold(selectedItem.StockID, newThreshold);

                            // 2. Update the visual UI object
                            selectedItem.LowStockThreshold = newThreshold;

                            // 3. Tell WPF to put this refresh task at the "Background" priority, 
                            // meaning it will wait for the DataGrid to completely finish its edit transaction first!
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                dgStock.Items.Refresh();
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to update threshold: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
    }
}