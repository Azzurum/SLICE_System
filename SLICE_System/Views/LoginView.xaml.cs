using SLICE_System.Data;
using SLICE_System.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SLICE_System.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
            txtUsername.Focus();
        }

        // Exit Button Logic
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                // Playful Error Message
                MessageBox.Show("Hey! We need your ID and Password to get cooking!", "Missing Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                UserRepository repo = new UserRepository();
                User user = repo.Login(username, password);

                if (user != null)
                {
                    MainWindow dashboard = new MainWindow(user);
                    dashboard.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Oops! Those credentials don't match our recipe.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Server Error: {ex.Message}", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ForgotPassword_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var forgotPassWindow = new SLICE_System.Views.Dialogs.ForgotPasswordWindow();
            forgotPassWindow.Owner = System.Windows.Application.Current.MainWindow;
            forgotPassWindow.ShowDialog();
        }

        // Flag to prevent infinite looping between Text and Password updates
        private bool _isPasswordSyncing = false;

        private void btnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (btnTogglePassword.IsChecked == true)
            {
                // Show Plain Text
                txtPasswordVisible.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPasswordVisible.Focus();
                txtPasswordVisible.CaretIndex = txtPasswordVisible.Text.Length;
            }
            else
            {
                // Hide Plain Text (Show Dots)
                txtPassword.Password = txtPasswordVisible.Text;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;
                txtPassword.Focus();
            }
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordSyncing)
            {
                _isPasswordSyncing = true;
                txtPasswordVisible.Text = txtPassword.Password;
                _isPasswordSyncing = false;
            }
        }

        private void txtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isPasswordSyncing)
            {
                _isPasswordSyncing = true;
                txtPassword.Password = txtPasswordVisible.Text;
                _isPasswordSyncing = false;
            }
        }
    }
}