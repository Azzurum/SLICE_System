using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.ViewModels
{
    public class ManageDiscountsViewModel : ViewModelBase
    {
        private readonly DiscountRepository _repo = new DiscountRepository();

        public ObservableCollection<Discount> AdminDiscounts { get; set; }

        // Form Properties
        private string _newName;
        public string NewName { get => _newName; set => SetProperty(ref _newName, value); }

        private string _newType;
        public string NewType { get => _newType; set => SetProperty(ref _newType, value); }

        private string _newValueType;
        public string NewValueType { get => _newValueType; set => SetProperty(ref _newValueType, value); }

        private decimal _newValue;
        public decimal NewValue { get => _newValue; set => SetProperty(ref _newValue, value); }

        private string _newRole;
        public string NewRole { get => _newRole; set => SetProperty(ref _newRole, value); }

        // Lists for Dropdowns
        public ObservableCollection<string> DiscountTypes { get; } = new ObservableCollection<string> { "Promo", "Manual" };
        public ObservableCollection<string> ValueTypes { get; } = new ObservableCollection<string> { "Percentage", "Fixed" };
        public ObservableCollection<string> Roles { get; } = new ObservableCollection<string> { "Clerk", "Manager", "Super-Admin" };

        public ICommand SaveCommand { get; }
        public ICommand ToggleStatusCommand { get; }

        public ManageDiscountsViewModel()
        {
            AdminDiscounts = new ObservableCollection<Discount>();
            SaveCommand = new RelayCommand(SaveDiscount);
            ToggleStatusCommand = new RelayCommand<Discount>(ToggleStatus);

            // Set Defaults
            NewType = DiscountTypes[0];
            NewValueType = ValueTypes[0];
            NewRole = Roles[0];

            LoadData();
        }

        private void LoadData()
        {
            AdminDiscounts.Clear();
            var list = _repo.GetAllAdminDiscounts();
            foreach (var d in list) AdminDiscounts.Add(d);
        }

        private void SaveDiscount()
        {
            if (string.IsNullOrWhiteSpace(NewName) || NewValue <= 0)
            {
                MessageBox.Show("Please provide a valid name and a discount value greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newDiscount = new Discount
            {
                DiscountName = NewName,
                DiscountType = NewType,
                Scope = "Order", // Hardcoded to Order-level for simplicity in Pizza POS
                ValueType = NewValueType,
                DiscountValue = NewValue,
                RequiredRole = NewRole
            };

            try
            {
                _repo.CreateDiscount(newDiscount);
                MessageBox.Show("Pricing rule created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear form
                NewName = string.Empty;
                NewValue = 0;

                LoadData(); // Refresh grid
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving discount: {ex.Message}");
            }
        }

        private void ToggleStatus(Discount discount)
        {
            if (discount == null) return;

            // Cannot disable mandated Government discounts
            if (discount.DiscountType == "Government")
            {
                MessageBox.Show("Government mandated discounts (PWD/Senior) cannot be deactivated.", "Compliance Lock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool newStatus = !discount.IsActive; // Flip current status
                _repo.ToggleDiscountStatus(discount.DiscountID, newStatus);
                LoadData(); // Refresh UI to show updated badge
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating status: {ex.Message}");
            }
        }
    }
}