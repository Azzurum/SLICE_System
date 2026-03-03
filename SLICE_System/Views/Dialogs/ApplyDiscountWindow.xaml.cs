using System.Windows;
using System.Windows.Controls;
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

                selected.ReferenceID = txtID.Text;
                selected.Reason = txtReason.Text;
                SelectedDiscount = selected;

                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}