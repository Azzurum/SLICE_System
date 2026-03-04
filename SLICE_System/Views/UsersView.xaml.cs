using System.Windows;
using System.Windows.Controls;
using SLICE_System.Data;
using SLICE_System.Models; // Required to recognize the User object

namespace SLICE_System.Views
{
    public partial class UsersView : UserControl
    {
        private UserRepository _repo;

        public UsersView()
        {
            InitializeComponent();
            _repo = new UserRepository();
            LoadUsers();
        }

        private void LoadUsers(string search = "")
        {
            dgUsers.ItemsSource = _repo.GetAllUsers(search);
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            AddUserWindow win = new AddUserWindow();
            win.ShowDialog();
            LoadUsers(txtSearch.Text);
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadUsers(txtSearch.Text);
        }

        // --- NEW: EDIT USER ---
        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            // Extract the entire User object we bound to the button's Tag
            var userToEdit = button.Tag as User;
            if (userToEdit == null) return;

            // Open the AddUserWindow, but use the Edit constructor
            AddUserWindow win = new AddUserWindow(userToEdit);
            win.ShowDialog();

            // Refresh the grid to show any updated names or roles
            LoadUsers(txtSearch.Text);
        }

        // --- DEACTIVATE USER ---
        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            // Get the full user object
            var userToDelete = button.Tag as User;
            if (userToDelete == null) return;

            // SAFEGUARD: Prevent deactivating the Owner
            if (userToDelete.Role == "Super-Admin")
            {
                MessageBox.Show("Security Alert: You cannot deactivate an Owner account. Please change their role first if you must remove their access.",
                                "Action Denied",
                                MessageBoxButton.OK,
                                MessageBoxImage.Stop);
                return; // Stops the code here, preventing deactivation
            }

            var result = MessageBox.Show(
                $"Are you sure you want to deactivate {userToDelete.FullName}? They will lose access immediately.",
                "Confirm Deactivation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _repo.DeactivateUser(userToDelete.UserID);
                LoadUsers(txtSearch.Text);
            }
        }

        // --- RESTORE USER ---
        private void RestoreUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            int userId = (int)button.Tag;

            var result = MessageBox.Show(
                "Do you want to reactivate this user account?",
                "Confirm Restoration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _repo.ReactivateUser(userId);
                LoadUsers(txtSearch.Text);
            }
        }
    }
}