using System;
using System.Windows;
using System.Windows.Controls;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class AddUserWindow : Window
    {
        private User _existingUser; // Stores the user if we are editing

        // 1. CONSTRUCTOR FOR NEW USER
        public AddUserWindow()
        {
            InitializeComponent();
            LoadBranches();
        }

        // 2. CONSTRUCTOR FOR EDITING EXISTING USER (Fixes the CS1729 Error)
        public AddUserWindow(User userToEdit) : this() // Calls the first constructor to load branches
        {
            _existingUser = userToEdit;

            // Populate the textboxes with the existing data so the owner can see/edit them
            txtName.Text = _existingUser.FullName;
            txtUser.Text = _existingUser.Username;
            txtPass.Text = _existingUser.PasswordHash; // The owner can see and edit the password

            // Match the Role Dropdown
            foreach (ComboBoxItem item in cmbRole.Items)
            {
                if (item.Content.ToString() == _existingUser.Role)
                {
                    cmbRole.SelectedItem = item;
                    break;
                }
            }

            // Match the Branch Dropdown
            if (_existingUser.BranchID.HasValue)
            {
                cmbBranch.SelectedValue = _existingUser.BranchID.Value;
            }

            // Update UI title
            this.Title = "Edit User";
        }

        private void LoadBranches()
        {
            try
            {
                InventoryRepository repo = new InventoryRepository();
                cmbBranch.ItemsSource = repo.GetAllBranches();

                // Select first branch by default if creating a new user
                if (cmbBranch.Items.Count > 0 && _existingUser == null)
                    cmbBranch.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load branches: " + ex.Message);
            }
        }

        // --- NEW: UX SAFEGUARD FOR LOGISTICS ADMIN ---
        private void cmbRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Safety check to ensure the UI is fully loaded
            if (cmbRole == null || cmbBranch == null) return;

            var selectedItem = cmbRole.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string selectedRole = selectedItem.Content.ToString();

            if (selectedRole == "Logistics Admin")
            {
                // Loop through your loaded branches to find the HQ
                foreach (Branch b in cmbBranch.Items)
                {
                    // Look for a branch that has "HQ", "Headquarters", or "Main" in the name
                    if (b.BranchName.Contains("Headquarters") || b.BranchName.Contains("HQ") || b.BranchName.Contains("Main"))
                    {
                        cmbBranch.SelectedValue = b.BranchID;
                        break;
                    }
                }

                // Lock the dropdown so the Owner cannot accidentally change it
                cmbBranch.IsEnabled = false;
            }
            else
            {
                // If it is a Clerk, Manager, or Super-Admin, unlock the dropdown
                cmbBranch.IsEnabled = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtUser.Text) || string.IsNullOrWhiteSpace(txtPass.Text))
            {
                MessageBox.Show("Please fill in all required fields (Name, Username, Password).", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbRole.SelectedItem == null)
            {
                MessageBox.Show("Please select a user role.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                UserRepository repo = new UserRepository();
                string selectedRole = (cmbRole.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Clerk";
                int? selectedBranch = (int?)cmbBranch.SelectedValue;

                if (_existingUser == null)
                {
                    // --- INSERT NEW USER ---
                    User newUser = new User
                    {
                        FullName = txtName.Text.Trim(),
                        Username = txtUser.Text.Trim(),
                        PasswordHash = txtPass.Text,
                        Role = selectedRole,
                        BranchID = selectedBranch,
                        IsActive = true
                    };
                    repo.AddUser(newUser);
                    MessageBox.Show("User account created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // --- UPDATE EXISTING USER ---
                    _existingUser.FullName = txtName.Text.Trim();
                    _existingUser.Username = txtUser.Text.Trim();
                    _existingUser.PasswordHash = txtPass.Text; // Update to the new password
                    _existingUser.Role = selectedRole;
                    _existingUser.BranchID = selectedBranch;

                    repo.UpdateUser(_existingUser);
                    MessageBox.Show("User account updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving user: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}