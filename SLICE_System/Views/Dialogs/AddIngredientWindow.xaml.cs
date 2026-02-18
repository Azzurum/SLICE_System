using System.Windows;
using System.Windows.Controls;
using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views.Dialogs
{
    public partial class AddIngredientWindow : Window
    {
        public AddIngredientWindow()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtBulk.Text))
            {
                MessageBox.Show("Please fill in all fields.");
                return;
            }

            if (!decimal.TryParse(txtRatio.Text, out decimal ratio) || ratio <= 0)
            {
                MessageBox.Show("Invalid Conversion Ratio.");
                return;
            }

            // 2. Prepare Data
            var newItem = new MasterInventory
            {
                ItemName = txtName.Text.Trim(),
                Category = (cmbCategory.SelectedItem as ComboBoxItem)?.Content.ToString(),
                BulkUnit = txtBulk.Text.Trim(),
                BaseUnit = (cmbBase.SelectedItem as ComboBoxItem)?.Content.ToString(),
                ConversionRatio = ratio
            };

            // 3. Save to Database
            var db = new DatabaseService();
            using (var conn = db.GetConnection())
            {
                string sql = @"
                    INSERT INTO MasterInventory (ItemName, Category, BulkUnit, BaseUnit, ConversionRatio) 
                    VALUES (@ItemName, @Category, @BulkUnit, @BaseUnit, @ConversionRatio)";

                conn.Execute(sql, newItem);
            }

            // 4. Close with Success
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}