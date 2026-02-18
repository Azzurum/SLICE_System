using System;
using System.Windows;
using System.Windows.Controls;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class AddIngredientWindow : Window
    {
        public AddIngredientWindow()
        {
            InitializeComponent();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validate Inputs
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtRatio.Text))
            {
                MessageBox.Show("Please provide at least an Item Name and Conversion Ratio.", "Missing Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtRatio.Text, out decimal ratio))
            {
                MessageBox.Show("Conversion Ratio must be a valid number.", "Invalid Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 2. Create the Model
                MasterInventory newItem = new MasterInventory
                {
                    ItemName = txtName.Text,
                    // Safe way to get ComboBox text content
                    Category = (cmbCategory.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "General",
                    BulkUnit = txtBulk.Text,
                    BaseUnit = txtBase.Text,
                    ConversionRatio = ratio
                };

                // 3. Save to Database
                InventoryRepository repo = new InventoryRepository();
                repo.AddIngredient(newItem);

                MessageBox.Show("Ingredient added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // 4. Close
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}