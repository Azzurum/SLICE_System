using System.Collections.Generic;
using System.Linq; // Important for .ToList()
using System.Windows;
using Dapper; // Ensure Dapper is using
using SLICE_System.Data;
using SLICE_System.Models;
using SLICE_System.ViewModels; // To access DispatchItemModel

namespace SLICE_System.Views.Dialogs
{
    public partial class DispatchDialog : Window
    {
        public int SelectedBranchID { get; private set; }
        public List<DispatchItemModel> ItemsToDispatch { get; private set; }

        // Constructor now accepts a List
        public DispatchDialog(List<DispatchItemModel> items)
        {
            InitializeComponent();
            ItemsToDispatch = items;
            dgItems.ItemsSource = ItemsToDispatch; // Bind the grid

            // Just show count in title
            txtItemName.Text = $"Preparing to dispatch {items.Count} item(s)";
            LoadBranches();
        }

        private void LoadBranches()
        {
            var db = new DatabaseService();
            using (var conn = db.GetConnection())
            {
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
                MessageBox.Show("Please select a destination branch.");
                return;
            }

            // Validation: Ensure at least one item has quantity > 0
            if (!ItemsToDispatch.Any(i => i.Quantity > 0))
            {
                MessageBox.Show("Please enter a quantity for at least one item.");
                return;
            }

            SelectedBranchID = (int)cmbBranches.SelectedValue;
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