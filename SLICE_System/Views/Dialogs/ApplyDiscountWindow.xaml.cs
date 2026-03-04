using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SLICE_System.Models;

namespace SLICE_System.Views.Dialogs
{
    public partial class ApplyDiscountWindow : Window
    {
        public Discount SelectedDiscount { get; private set; }

        public ApplyDiscountWindow(System.Collections.Generic.List<Discount> availableDiscounts)
        {
            InitializeComponent();
            lstDiscounts.ItemsSource = availableDiscounts;
        }

        private void LstDiscounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstDiscounts.SelectedItem is Discount selected)
            {
                pnlInputs.Visibility = Visibility.Visible;
                pnlID.Visibility = selected.DiscountType == "Government" ? Visibility.Visible : Visibility.Collapsed;
                pnlManual.Visibility = selected.DiscountType == "Manual" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // --- input validation to ONLY allow Letters, Numbers, and Dashes ---
        private void TxtID_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // If the typed character is NOT a letter, number, or dash, block it.
            Regex regex = new Regex("[^a-zA-Z0-9-]");
            e.Handled = regex.IsMatch(e.Text);
        }

        // --- Block the spacebar key entirely ---
        private void TxtID_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (lstDiscounts.SelectedItem is Discount selected)
            {
                if (selected.DiscountType == "Government" && string.IsNullOrWhiteSpace(txtID.Text))
                {
                    MessageBox.Show("A valid Government ID is required for this discount.", "Validation Error");
                    return;
                }

                if (selected.DiscountType == "Manual")
                {
                    if (string.IsNullOrWhiteSpace(txtReason.Text) || !decimal.TryParse(txtManualValue.Text, out decimal val))
                    {
                        MessageBox.Show("Please enter a valid amount and a reason for the override.", "Validation Error");
                        return;
                    }
                    selected.DiscountValue = val;
                }

                selected.ReferenceID = txtID.Text.Trim(); // Trim added for safety
                selected.Reason = txtReason.Text.Trim();
                SelectedDiscount = selected;

                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}