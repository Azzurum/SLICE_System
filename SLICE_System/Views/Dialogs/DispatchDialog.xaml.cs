using System.Windows;
using System.Collections.Generic;
using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views.Dialogs
{
    public partial class DispatchDialog : Window
    {
        // FIX: Added Public Properties to access data from ViewModel
        public int SelectedBranchID { get; private set; }
        public decimal Quantity { get; private set; }

        // FIX: Added the specific Constructor that takes 2 arguments
        public DispatchDialog(string itemName, string unit)
        {
            InitializeComponent();

            // Set the text of the TextBlock named 'txtItemName'
            txtItemName.Text = $"Sending: {itemName} ({unit})";

            LoadBranches();
        }

        private void LoadBranches()
        {
            var db = new DatabaseService();
            using (var conn = db.GetConnection())
            {
                // Simple query to fill the dropdown
                var branches = conn.Query<Branch>("SELECT * FROM Branches WHERE BranchName != 'Head Office'").AsList();

                cmbBranches.ItemsSource = branches;
                cmbBranches.DisplayMemberPath = "BranchName";
                cmbBranches.SelectedValuePath = "BranchID";

                if (branches.Count > 0) cmbBranches.SelectedIndex = 0;
            }
        }

        private void Dispatch_Click(object sender, RoutedEventArgs e)
        {
            if (cmbBranches.SelectedValue == null)
            {
                MessageBox.Show("Please select a branch.");
                return;
            }

            if (!decimal.TryParse(txtQty.Text, out decimal qty) || qty <= 0)
            {
                MessageBox.Show("Please enter a valid quantity.");
                return;
            }

            // Save values to properties
            SelectedBranchID = (int)cmbBranches.SelectedValue;
            Quantity = qty;

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