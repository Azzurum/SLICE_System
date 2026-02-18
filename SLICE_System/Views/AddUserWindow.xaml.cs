using System;
using System.Windows;
using System.Windows.Controls;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class AddUserWindow : Window
    {
        public AddUserWindow()
        {
            InitializeComponent();
            LoadBranches();
        }

        private void LoadBranches()
        {
            try
            {
                // We reuse the InventoryRepository for branches as per your existing structure
                // Ideally, move 'GetAllBranches' to a common repository later.
                InventoryRepository repo = new InventoryRepository();
                cmbBranch.ItemsSource = repo.GetAllBranches();

                // Select first branch by default if available
                if (cmbBranch.Items.Count > 0) cmbBranch.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load branches: " + ex.Message);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
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
                // 2. Prepare Data
                UserRepository repo = new UserRepository();

                string selectedRole = (cmbRole.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Clerk";
                int? selectedBranch = (int?)cmbBranch.SelectedValue;

                // Super-Admins usually don't need a specific branch (or can see all), 
                // but we assign one if selected, or null if you prefer global access logic.
                // Keeping logic simple: always assign what is selected.

                User newUser = new User
                {
                    FullName = txtName.Text.Trim(),
                    Username = txtUser.Text.Trim(),
                    PasswordHash = txtPass.Text, // TODO: In production, Hash this password!
                    Role = selectedRole,
                    BranchID = selectedBranch,
                    IsActive = true
                };

                // 3. Save
                repo.AddUser(newUser);

                MessageBox.Show("User account created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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