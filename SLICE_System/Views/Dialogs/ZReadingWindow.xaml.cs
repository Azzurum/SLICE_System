using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using SLICE_System.Data;
using Dapper;

namespace SLICE_System.Views.Dialogs
{
    public partial class ZReadingWindow : Window
    {
        private int _userId;
        private decimal _expectedCash;

        public ZReadingWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;

            // Wire up the input restrictions ---
            txtActualCash.PreviewTextInput += TxtActualCash_PreviewTextInput;
            txtActualCash.PreviewKeyDown += TxtActualCash_PreviewKeyDown;

            // 1. Fetch expected cash from the database
            var repo = new SalesRepository();
            _expectedCash = repo.GetTodayExpectedCash(_userId);

            // Display it on the screen
            txtExpectedCash.Text = $"₱{_expectedCash:N2}";
        }

        // Blocks letters and multiple dots ---
        private void TxtActualCash_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;

            // If the user types a dot, check if one already exists
            if (e.Text == "." && textBox.Text.Contains("."))
            {
                e.Handled = true; // Reject the input
                return;
            }

            // Regex: Allow ONLY numbers (0-9) and dots (.)
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text); // If it's a letter or symbol, reject it
        }

        // Blocks the spacebar ---
        private void TxtActualCash_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true; // Reject the spacebar
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            // We still keep this check just in case they leave it completely blank
            if (!decimal.TryParse(txtActualCash.Text, out decimal actualCash))
            {
                MessageBox.Show("Please enter a valid monetary amount.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Calculate Variance
            decimal variance = actualCash - _expectedCash;
            string status = variance == 0 ? "Perfect Match" : (variance > 0 ? $"Overage of ₱{Math.Abs(variance):N2}" : $"Shortage of ₱{Math.Abs(variance):N2}");

            // 3. Log it strictly to the Audit Trail
            using (var db = new DatabaseService().GetConnection())
            {
                string sqlAudit = @"
                    INSERT INTO AuditLogs (UserID, ActionType, AffectedTable, NewValue, Timestamp, ReferenceNumber)
                    VALUES (@UserID, 'Z-READING', 'FinancialLedger', @Desc, GETDATE(), @RefNum)";

                db.Execute(sqlAudit, new
                {
                    UserID = _userId,
                    Desc = $"Shift Closed. Expected: ₱{_expectedCash:N2} | Actual: ₱{actualCash:N2} | Variance: {status}",
                    RefNum = $"ZREAD-{DateTime.Now:MMdd-HHmm}"
                });
            }

            // 4. Show result to the cashier
            MessageBox.Show($"Z-Reading Complete.\n\nVariance: {status}\n\nLogging out...", "Shift Closed", MessageBoxButton.OK, MessageBoxImage.Information);

            // 5. Close window (and handle your logout logic in the parent window)
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}