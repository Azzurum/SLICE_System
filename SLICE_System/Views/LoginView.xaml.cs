using System.Windows;
using System.Windows.Input;
using SLICE_System.Data;
using SLICE_System.Models;

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
    }
}