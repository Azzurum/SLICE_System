using System.Windows;
using System.Windows.Input;
using Dapper;
using SLICE_System.Data;

namespace SLICE_System.Views.Dialogs
{
    public partial class ManagerApprovalWindow : Window
    {
        public int ApprovedManagerID { get; private set; }

        public ManagerApprovalWindow()
        {
            InitializeComponent();

            // Auto-focus the username box so the manager can start typing immediately
            txtUsername.Focus();
        }

        private void Authorize_Click(object sender, RoutedEventArgs e)
        {
            Authenticate();
        }

        // Allow the manager to press the 'Enter' key inside the password box to submit
        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Authenticate();
            }
        }

        private void Authenticate()
        {
            using (var db = new DatabaseService().GetConnection())
            {
                // FIX: Column is "PasswordHash" and checks if the user IsActive = 1
                string sql = @"
                    SELECT UserID FROM Users 
                    WHERE Username = @User 
                    AND PasswordHash = @Pass 
                    AND Role IN ('Manager', 'Super-Admin', 'Owner') 
                    AND IsActive = 1";

                var managerId = db.QuerySingleOrDefault<int?>(sql, new { User = txtUsername.Text, Pass = txtPassword.Password });

                if (managerId.HasValue)
                {
                    ApprovedManagerID = managerId.Value;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid credentials or insufficient permissions.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtPassword.Clear();
                    txtPassword.Focus(); // Reset focus so they can try again quickly
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Allows dragging the borderless window if the user clicks on the transparent background
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}