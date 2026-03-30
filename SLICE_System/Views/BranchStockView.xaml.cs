using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SLICE_System.Data;
using SLICE_System.Models; // Ensure Models are imported for BranchInventoryItem

namespace SLICE_System.Views
{
    public partial class BranchStockView : UserControl
    {
        private int _branchId;
        private InventoryRepository _repo;

        // ADDED: Store the full original list so we can filter locally without hitting the DB over and over
        private List<BranchInventoryItem> _allStock;

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
                // Fetch the live inventory list for this branch and save it to the master list
                var stockList = _repo.GetStockForBranch(_branchId);
                _allStock = stockList.Cast<BranchInventoryItem>().ToList();

                // Apply any existing search text to the new data
                FilterStock();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory: {ex.Message}", "System Error");
            }
        }

        // --- ADDED SEARCH LOGIC ---
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterStock();
        }

        private void FilterStock()
        {
            if (_allStock == null) return;

            string searchText = txtSearch.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                // Reset to full list
                dgStock.ItemsSource = _allStock;
            }
            else
            {
                // Filter locally by ItemName or Category
                dgStock.ItemsSource = _allStock.Where(i =>
                    (i.ItemName != null && i.ItemName.ToLower().Contains(searchText)) ||
                    (i.Category != null && i.Category.ToLower().Contains(searchText))
                ).ToList();
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