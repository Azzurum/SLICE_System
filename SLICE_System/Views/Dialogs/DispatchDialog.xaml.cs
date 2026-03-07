using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dapper;
using SLICE_System.Data;
using SLICE_System.Models;
using SLICE_System.ViewModels;

namespace SLICE_System.Views.Dialogs
{
    // Model for tracking multi-branch checkbox states
    public class BranchSelectionModel
    {
        public int BranchID { get; set; }
        public string BranchName { get; set; }
        public bool IsSelected { get; set; }
    }

    public partial class DispatchDialog : Window
    {
        // Returns a list of all BranchIDs the user highlighted
        public List<int> SelectedBranchIDs => BranchSelections.Where(b => b.IsSelected).Select(b => b.BranchID).ToList();

        public List<DispatchItemModel> ItemsToDispatch { get; private set; }
        public List<BranchSelectionModel> BranchSelections { get; set; }

        public DispatchDialog(List<DispatchItemModel> items)
        {
            InitializeComponent();
            ItemsToDispatch = items;
            dgItems.ItemsSource = ItemsToDispatch;

            txtItemName.Text = $"Preparing {items.Count} item(s) for deployment";
            LoadBranches();
        }

        private void LoadBranches()
        {
            var db = new DatabaseService();
            using (var conn = db.GetConnection())
            {
                // Fetch active branches (Exclude Headquarters ID 4)
                var branches = conn.Query<Branch>("SELECT * FROM Branches WHERE BranchID != 4 ORDER BY BranchName").AsList();

                // Convert to checkable models
                BranchSelections = branches.Select(b => new BranchSelectionModel
                {
                    BranchID = b.BranchID,
                    BranchName = b.BranchName,
                    IsSelected = false
                }).ToList();

                // Bind to the WrapPanel CheckBoxes
                icBranches.ItemsSource = BranchSelections;
            }
        }

        private void Dispatch_Click(object sender, RoutedEventArgs e)
        {
            // Validate at least one branch is selected
            if (SelectedBranchIDs.Count == 0)
            {
                MessageBox.Show("Please select at least one destination branch.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate at least one item has a quantity > 0
            if (!ItemsToDispatch.Any(i => i.Quantity > 0))
            {
                MessageBox.Show("Please enter a dispatch quantity for at least one item.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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