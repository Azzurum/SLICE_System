using System.Windows;
using System.Windows.Controls;
using SLICE_System.Data;

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

        // --- DEACTIVATE USER ---
        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            int userId = (int)button.Tag;

            var result = MessageBox.Show(
                "Are you sure you want to deactivate this user? They will lose access immediately.",
                "Confirm Deactivation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _repo.DeactivateUser(userId);
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