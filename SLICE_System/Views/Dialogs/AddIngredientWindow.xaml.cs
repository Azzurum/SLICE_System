using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views.Dialogs
{
    public partial class AddIngredientWindow : Window
    {
        private readonly DatabaseService _db = new DatabaseService();
        private MasterInventory _existingItem;

        // 1. CONSTRUCTOR FOR NEW ITEM
        public AddIngredientWindow()
        {
            InitializeComponent();
        }

        // 2. CONSTRUCTOR FOR EDITING EXISTING ITEM
        public AddIngredientWindow(MasterInventory itemToEdit) : this()
        {
            _existingItem = itemToEdit;

            // Populate UI with existing data
            txtName.Text = itemToEdit.ItemName;
            cmbCategory.Text = itemToEdit.Category;
            txtBulk.Text = itemToEdit.BulkUnit;

            // Set Base Unit ComboBox
            foreach (ComboBoxItem item in cmbBase.Items)
            {
                if (item.Content.ToString() == itemToEdit.BaseUnit)
                {
                    cmbBase.SelectedItem = item;
                    break;
                }
            }

            txtRatio.Text = itemToEdit.ConversionRatio.ToString();

            // Update UI Header
            txtHeaderTitle.Text = "Edit Ingredient";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Ingredient name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtRatio.Text, out decimal ratio))
            {
                MessageBox.Show("Conversion Ratio must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = _db.GetConnection())
                {
                    string category = (cmbCategory.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "General";
                    string baseUnit = (cmbBase.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "g";

                    if (_existingItem == null)
                    {
                        // --- LOGIC: INSERT NEW ---
                        string sql = @"INSERT INTO MasterInventory (ItemName, Category, BulkUnit, BaseUnit, ConversionRatio) 
                                       VALUES (@Name, @Cat, @Bulk, @Base, @Ratio)";

                        conn.Execute(sql, new
                        {
                            Name = txtName.Text,
                            Cat = category,
                            Bulk = txtBulk.Text,
                            Base = baseUnit,
                            Ratio = ratio
                        });

                        MessageBox.Show("New ingredient successfully added to the Warehouse.", "Success");
                    }
                    else
                    {
                        // --- LOGIC: UPDATE EXISTING ---
                        string sql = @"UPDATE MasterInventory 
                                       SET ItemName = @Name, Category = @Cat, BulkUnit = @Bulk, BaseUnit = @Base, ConversionRatio = @Ratio 
                                       WHERE ItemID = @ID";

                        conn.Execute(sql, new
                        {
                            Name = txtName.Text,
                            Cat = category,
                            Bulk = txtBulk.Text,
                            Base = baseUnit,
                            Ratio = ratio,
                            ID = _existingItem.ItemID
                        });
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Operation failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Allows dragging of the borderless window
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}