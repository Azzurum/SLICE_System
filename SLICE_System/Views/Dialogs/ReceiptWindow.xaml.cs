using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SLICE_System.ViewModels;

namespace SLICE_System.Views.Dialogs
{
    public partial class ReceiptWindow : Window
    {
        public ReceiptWindow(
            IEnumerable<CartItemVM> items,
            decimal subTotal,
            decimal discount,
            decimal grandTotal,
            string method,
            string reference)
        {
            InitializeComponent();

            // 1. Populate the items list in the receipt
            ReceiptItemsControl.ItemsSource = items;

            // 2. Populate the transaction metadata
            txtDate.Text = $"Date: {DateTime.Now:yyyy-MM-dd HH:mm}";
            txtCashier.Text = "Cashier: POS Register 1"; // This can be updated to the real UserID later
            txtRef.Text = $"Ref: {reference}";

            // 3. Populate the financial totals
            txtSubTotal.Text = $"₱{subTotal:N2}";
            txtDiscount.Text = $"-₱{discount:N2}";
            txtTotal.Text = $"₱{grandTotal:N2}";
            txtMethod.Text = method.ToUpper();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();

            // Show the standard Windows Print Dialog
            if (printDialog.ShowDialog() == true)
            {
                // Print ONLY the receipt border (ignores the print/close buttons at the bottom)
                printDialog.PrintVisual(PrintableReceipt, "SLICE System POS Receipt");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Close the receipt window to finish the transaction flow
            this.Close();
        }
    }
}