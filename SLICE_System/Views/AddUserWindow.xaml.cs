using System;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions; // Required for Enterprise Password Regex
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

        // 2. CONSTRUCTOR FOR EDITING EXISTING USER
        public AddUserWindow(User userToEdit) : this()
        {
            _existingUser = userToEdit;

            // Populate the textboxes with the existing data
            txtName.Text = _existingUser.FullName;
            txtEmail.Text = _existingUser.Email; // NEW: Bind Email
            txtUser.Text = _existingUser.Username;
            txtPass.Text = _existingUser.PasswordHash;

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

        // UX SAFEGUARD FOR LOGISTICS ADMIN
        private void cmbRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbRole == null || cmbBranch == null) return;

            var selectedItem = cmbRole.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string selectedRole = selectedItem.Content.ToString();

            if (selectedRole == "Logistics Admin")
            {
                foreach (Branch b in cmbBranch.Items)
                {
                    if (b.BranchName.Contains("Headquarters") || b.BranchName.Contains("HQ") || b.BranchName.Contains("Main"))
                    {
                        cmbBranch.SelectedValue = b.BranchID;
                        break;
                    }
                }
                cmbBranch.IsEnabled = false;
            }
            else
            {
                cmbBranch.IsEnabled = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate all fields including Email
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtUser.Text) ||
                string.IsNullOrWhiteSpace(txtPass.Text) || string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                MessageBox.Show("Please fill in all required fields (Name, Email, Username, Password).", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Enterprise Password Complexity Check
            // We only check complexity if it's a new user, OR if the admin is actively typing a new password for an existing user
            bool isPasswordChanged = _existingUser == null || txtPass.Text != _existingUser.PasswordHash;

            if (isPasswordChanged)
            {
                var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
                if (!passwordRegex.IsMatch(txtPass.Text))
                {
                    MessageBox.Show("Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, and one number.",
                                    "Weak Password Requirement", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
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
                        Email = txtEmail.Text.Trim(), // Include Email
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
                    _existingUser.Email = txtEmail.Text.Trim(); // Include Email
                    _existingUser.Username = txtUser.Text.Trim();
                    _existingUser.PasswordHash = txtPass.Text;
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