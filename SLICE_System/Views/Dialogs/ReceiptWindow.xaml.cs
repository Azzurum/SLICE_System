using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SLICE_System.ViewModels;

namespace SLICE_System.Views.Dialogs
{
    public partial class ReceiptWindow : Window
    {
        // Added BranchName, BranchAddress, BranchContact, and CashierName to the constructor
        public ReceiptWindow(
            IEnumerable<CartItemVM> items,
            decimal subTotal,
            decimal discount,
            decimal grandTotal,
            string method,
            string reference,
            string branchName,
            string branchAddress,
            string branchContact,
            string cashierName)
        {
            InitializeComponent();

            // 1. Populate the items list in the receipt
            ReceiptItemsControl.ItemsSource = items;

            // 2. Populate Branch Details
            txtBranchName.Text = branchName?.ToUpper();
            txtBranchAddress.Text = branchAddress;
            txtContact.Text = $"Contact: {branchContact}";

            // 3. Populate the transaction metadata
            txtDate.Text = $"Date: {DateTime.Now:yyyy-MM-dd HH:mm}";
            txtCashier.Text = $"Cashier: {cashierName}";
            txtRef.Text = $"Ref: {reference}";

            // 4. Populate the financial totals
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