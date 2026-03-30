using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Dapper;

namespace SLICE_System.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly DatabaseService _db = new DatabaseService();
        private Branch _selectedBranch;

        public ObservableCollection<Branch> Branches { get; set; }

        // ADDED: For the WOW-factor dashboard metric
        public int TotalBranches => Branches.Count;

        // Form Fields
        private string _branchName;
        public string BranchName { get => _branchName; set => SetProperty(ref _branchName, value); }

        private string _location;
        public string Location { get => _location; set => SetProperty(ref _location, value); }

        private string _contactNumber;
        public string ContactNumber { get => _contactNumber; set => SetProperty(ref _contactNumber, value); }

        // Triggered when a user clicks a branch in the data grid
        public Branch SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (SetProperty(ref _selectedBranch, value) && value != null)
                {
                    // Pre-fill the form for editing
                    BranchName = value.BranchName;
                    Location = value.Location;
                    ContactNumber = value.ContactNumber;
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }

        public SettingsViewModel()
        {
            Branches = new ObservableCollection<Branch>();

            SaveCommand = new RelayCommand(SaveBranch);
            DeleteCommand = new RelayCommand(DeleteBranch, () => SelectedBranch != null);
            ClearCommand = new RelayCommand(ClearForm);

            LoadBranches();
        }

        private void LoadBranches()
        {
            try
            {
                using (var conn = _db.GetConnection())
                {
                    var list = conn.Query<Branch>("SELECT * FROM Branches ORDER BY BranchID ASC");
                    Branches.Clear();
                    foreach (var b in list) Branches.Add(b);

                    // Tell the UI to update the Live Stat card!
                    OnPropertyChanged(nameof(TotalBranches));
                }
            }
            catch (Exception ex) { MessageBox.Show("Error loading branches: " + ex.Message); }
        }

        private void SaveBranch()
        {
            if (string.IsNullOrWhiteSpace(BranchName) || string.IsNullOrWhiteSpace(Location))
            {
                MessageBox.Show("Branch Name and Location are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = _db.GetConnection())
                {
                    if (SelectedBranch == null)
                    {
                        // INSERT NEW
                        string sql = "INSERT INTO Branches (BranchName, Location, ContactNumber) VALUES (@Name, @Loc, @Contact)";
                        conn.Execute(sql, new { Name = BranchName, Loc = Location, Contact = ContactNumber });
                        MessageBox.Show("Branch added successfully!", "Success");
                    }
                    else
                    {
                        // UPDATE EXISTING
                        string sql = "UPDATE Branches SET BranchName = @Name, Location = @Loc, ContactNumber = @Contact WHERE BranchID = @Id";
                        conn.Execute(sql, new { Name = BranchName, Loc = Location, Contact = ContactNumber, Id = SelectedBranch.BranchID });
                        MessageBox.Show("Branch updated successfully!", "Success");
                    }
                }
                LoadBranches();
                ClearForm();
            }
            catch (Exception ex) { MessageBox.Show("Error saving: " + ex.Message); }
        }

        private void DeleteBranch()
        {
            if (SelectedBranch == null) return;

            // Block deleting the Central Warehouse (ID 4) or standard defaults
            if (SelectedBranch.BranchID == 4)
            {
                MessageBox.Show("You cannot delete the Central Warehouse.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete '{SelectedBranch.BranchName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = _db.GetConnection())
                    {
                        // Note: If a branch has inventory or sales, SQL Server will block deletion due to Foreign Keys. 
                        // This acts as a natural safety mechanism so you don't break financial records!
                        conn.Execute("DELETE FROM Branches WHERE BranchID = @Id", new { Id = SelectedBranch.BranchID });
                        LoadBranches();
                        ClearForm();
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Cannot delete this branch because it currently has active inventory or sales records linked to it.", "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearForm()
        {
            SelectedBranch = null;
            BranchName = "";
            Location = "";
            ContactNumber = "";
        }
    }
}