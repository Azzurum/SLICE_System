using System;
using System.Windows;
using System.Windows.Input;
using SLICE_System.Data;
using SLICE_System.Services;

namespace SLICE_System.Views.Dialogs
{
    public partial class ForgotPasswordWindow : Window
    {
        private UserRepository _userRepo;
        private EmailService _emailService;
        private AuditRepository _auditRepo;
        private string _verifiedEmail = "";

        public ForgotPasswordWindow()
        {
            InitializeComponent();
            _userRepo = new UserRepository();
            _emailService = new EmailService();
            _auditRepo = new AuditRepository();
        }

        private void SendCode_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            if (string.IsNullOrEmpty(email)) return;

            var user = _userRepo.GetUserByEmail(email);
            if (user == null)
            {
                MessageBox.Show("Email not found in our system.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Generate 6-digit code
            Random rnd = new Random();
            string code = rnd.Next(100000, 999999).ToString();

            // Change this line in your SendCode_Click method:
            _userRepo.SaveResetCode(user.UserID, code, DateTime.UtcNow.AddMinutes(15));

            // Send Email
            bool emailSent = _emailService.SendPasswordResetEmail(email, code);

            if (emailSent)
            {
                _verifiedEmail = email;
                pnlStep1.Visibility = Visibility.Collapsed;
                pnlStep2.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Failed to send email. Check SMTP settings.", "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VerifyCode_Click(object sender, RoutedEventArgs e)
        {
            if (_userRepo.VerifyResetCode(_verifiedEmail, txtCode.Text.Trim()))
            {
                pnlStep2.Visibility = Visibility.Collapsed;
                pnlStep3.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Invalid or expired code.", "Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            string newPass = txtNewPassword.Password;
            if (newPass.Length < 4)
            {
                MessageBox.Show("Password must be at least 4 characters.", "Weak Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Get the user's ID before we close out
            var user = _userRepo.GetUserByEmail(_verifiedEmail);

            // 2. Update the password
            _userRepo.UpdatePassword(_verifiedEmail, newPass);

            // 3. LOG IT TO THE AUDIT TRAIL
            if (user != null)
            {
                // Note: Adjust the method name to whatever you named it in your AuditRepository.cs
                _auditRepo.LogAction(user.UserID, "Security", $"User reset their password via email verification.");
            }

            MessageBox.Show("Password successfully updated! You can now log in.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    }
}