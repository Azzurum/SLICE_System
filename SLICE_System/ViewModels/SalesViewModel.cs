using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SLICE_System.ViewModels
{
    public class CartItemVM : INotifyPropertyChanged
    {
        private int _qty;
        public int ProductID { get; set; }
        public string RawName { get; set; }
        public decimal BasePrice { get; set; }

        public string DisplayName => RawName.Contains("|") ? RawName.Split('|')[1].Trim() : RawName;

        public int Qty
        {
            get => _qty;
            set
            {
                if (_qty != value)
                {
                    _qty = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        public decimal TotalPrice => BasePrice * Qty;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProductDisplay
    {
        public int ProductID { get; set; }
        public string RawName { get; set; }
        public decimal BasePrice { get; set; }

        // Smart POS Properties
        public int MaxCookable { get; set; }
        public bool IsInStock => MaxCookable > 0;

        public string Category => RawName.Contains("|") ? RawName.Split('|')[0].Trim() : "Others";
        public string DisplayName => RawName.Contains("|") ? RawName.Split('|')[1].Trim() : RawName;
        public string FormattedPrice => $"₱{BasePrice:N0}";
    }

    public class SalesViewModel : ViewModelBase
    {
        private int _branchId;
        private int _userId;
        private string _currentUserRole; // Added for discount security
        private bool _isCooking;

        private SalesRepository _repo;
        private DiscountRepository _discountRepo; // New repository
        private string _selectedCategory;
        private List<ProductDisplay> _allProducts;

        // --- NEW DISCOUNT & TOTAL PROPERTIES ---
        private Discount _activeDiscount;
        public Discount ActiveDiscount
        {
            get => _activeDiscount;
            set
            {
                if (SetProperty(ref _activeDiscount, value))
                {
                    CalculateTotal();
                    OnPropertyChanged(nameof(HasDiscount));
                }
            }
        }

        public bool HasDiscount => ActiveDiscount != null;

        private decimal _subTotal;
        public decimal SubTotal { get => _subTotal; set => SetProperty(ref _subTotal, value); }

        private decimal _discountAmount;
        public decimal DiscountAmount { get => _discountAmount; set => SetProperty(ref _discountAmount, value); }

        private decimal _grandTotal;
        public decimal GrandTotal { get => _grandTotal; set => SetProperty(ref _grandTotal, value); }
        // -------------------------------------

        public bool IsCooking
        {
            get => _isCooking;
            set
            {
                if (SetProperty(ref _isCooking, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public DateTime CurrentDate => DateTime.Now;
        public ObservableCollection<ProductDisplay> FilteredProducts { get; set; }
        public ObservableCollection<CartItemVM> CartItems { get; set; }
        public ObservableCollection<string> Categories { get; set; }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    FilterList();
            }
        }

        public ICommand AddToCartCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand IncreaseQtyCommand { get; }
        public ICommand DecreaseQtyCommand { get; }

        // New Discount Commands
        public ICommand OpenDiscountCommand { get; }
        public ICommand RemoveDiscountCommand { get; }

        // Updated constructor to accept userRole (defaults to Clerk for safety)
        public SalesViewModel(int branchId, int userId, string userRole = "Clerk")
        {
            _branchId = branchId;
            _userId = userId;
            _currentUserRole = userRole;
            _repo = new SalesRepository();
            _discountRepo = new DiscountRepository();

            CartItems = new ObservableCollection<CartItemVM>();
            FilteredProducts = new ObservableCollection<ProductDisplay>();
            Categories = new ObservableCollection<string>();

            AddToCartCommand = new RelayCommand<ProductDisplay>(AddItemToCart);
            ClearCartCommand = new RelayCommand(ClearCart);
            IncreaseQtyCommand = new RelayCommand<object>(IncreaseQty);
            DecreaseQtyCommand = new RelayCommand<object>(DecreaseQty);
            CheckoutCommand = new RelayCommand(ExecuteCheckout, () => CartItems.Count > 0 && !IsCooking);

            OpenDiscountCommand = new RelayCommand(OpenDiscountDialog);
            RemoveDiscountCommand = new RelayCommand(() => ActiveDiscount = null);

            LoadData();
        }

        private void LoadData()
        {
            var rawList = _repo.GetMenu(_branchId);

            _allProducts = rawList.Select(x => new ProductDisplay
            {
                ProductID = x.ProductID,
                RawName = x.ProductName,
                BasePrice = x.BasePrice,
                MaxCookable = x.MaxCookable
            }).ToList();

            var uniqueCategories = _allProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

            Categories.Clear();
            Categories.Add("All");
            foreach (var cat in uniqueCategories) Categories.Add(cat);

            SelectedCategory = "All";
        }

        private void FilterList()
        {
            if (_allProducts == null) return;
            FilteredProducts.Clear();
            var query = SelectedCategory == "All" ? _allProducts : _allProducts.Where(p => p.Category == SelectedCategory);
            foreach (var item in query) FilteredProducts.Add(item);
        }

        private void AddItemToCart(ProductDisplay product)
        {
            if (product == null) return;

            var existing = CartItems.FirstOrDefault(c => c.ProductID == product.ProductID);
            int currentQty = existing != null ? existing.Qty : 0;

            // RECIPE-DRIVEN LIMIT CHECK
            if (currentQty + 1 > product.MaxCookable)
            {
                MessageBox.Show($"Not enough ingredients! You can only make {product.MaxCookable} portions of {product.DisplayName}.", "Stock Warning");
                return;
            }

            if (existing != null)
                existing.Qty++;
            else
            {
                CartItems.Add(new CartItemVM
                {
                    ProductID = product.ProductID,
                    RawName = product.RawName,
                    BasePrice = product.BasePrice,
                    Qty = 1
                });
            }
            CalculateTotal();
            CommandManager.InvalidateRequerySuggested();
        }

        private void IncreaseQty(object parameter)
        {
            if (parameter is CartItemVM item)
            {
                var product = _allProducts.FirstOrDefault(p => p.ProductID == item.ProductID);

                // RECIPE-DRIVEN LIMIT CHECK
                if (product != null && item.Qty + 1 > product.MaxCookable)
                {
                    MessageBox.Show($"Not enough ingredients! You can only make {product.MaxCookable} portions of {product.DisplayName}.", "Stock Warning");
                    return;
                }
                item.Qty++;
                CalculateTotal();
            }
        }

        private void DecreaseQty(object parameter)
        {
            if (parameter is CartItemVM item)
            {
                item.Qty--;
                if (item.Qty <= 0) CartItems.Remove(item);
                CalculateTotal();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // --- UPDATED PRICING ENGINE ---
        private void CalculateTotal()
        {
            SubTotal = CartItems.Sum(x => x.TotalPrice);

            if (ActiveDiscount != null)
            {
                if (ActiveDiscount.ValueType == "Percentage")
                {
                    DiscountAmount = SubTotal * (ActiveDiscount.DiscountValue / 100m);
                }
                else // Fixed
                {
                    DiscountAmount = ActiveDiscount.DiscountValue;
                }

                // Prevent negative totals
                if (DiscountAmount > SubTotal) DiscountAmount = SubTotal;
                ActiveDiscount.CalculatedAmount = DiscountAmount;
            }
            else
            {
                DiscountAmount = 0;
            }

            GrandTotal = SubTotal - DiscountAmount;
        }

        private void ClearCart()
        {
            CartItems.Clear();
            ActiveDiscount = null; // Clear discount on reset
            CalculateTotal();
            CommandManager.InvalidateRequerySuggested();
        }

        // --- NEW DISCOUNT DIALOG HANDLER ---
        private void OpenDiscountDialog()
        {
            if (CartItems.Count == 0)
            {
                MessageBox.Show("Please add items to the cart before applying a discount.", "Cart Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var available = _discountRepo.GetAvailableDiscounts(_currentUserRole);
            var dialog = new Views.Dialogs.ApplyDiscountWindow(available);

            if (dialog.ShowDialog() == true)
            {
                ActiveDiscount = dialog.SelectedDiscount;
            }
        }

        private async void ExecuteCheckout()
        {
            if (CartItems.Count == 0) return;
            IsCooking = true;

            bool success = false;
            string errorMessage = "";

            // Capture state variables for the background thread
            var currentDiscount = ActiveDiscount;
            var currentDiscountAmount = DiscountAmount;

            await Task.Run(() =>
            {
                try
                {
                    // 1. Process standard item sales (Full Price Gross Income)
                    foreach (var item in CartItems)
                        _repo.ProcessSale(_branchId, item.ProductID, item.Qty, _userId);

                    // 2. Log Discount if one is applied (Offsets Gross Income in Financial Ledger)
                    if (currentDiscount != null && currentDiscountAmount > 0)
                    {
                        _discountRepo.LogAppliedDiscount(
                            _branchId,
                            currentDiscount.DiscountID,
                            _userId,
                            currentDiscountAmount,
                            currentDiscount.ReferenceID,
                            currentDiscount.Reason);
                    }

                    success = true;
                }
                catch (Exception ex)
                {
                    success = false;
                    errorMessage = ex.Message; // Capture the real SQL error
                }
            });

            if (success)
            {
                await Task.Delay(5500); // Wait for the cooking animation
                ClearCart();
                IsCooking = false;

                // REFRESH DATA AFTER SALE TO RE-CALCULATE MAX STOCK
                LoadData();
            }
            else
            {
                IsCooking = false;
                // Show the ACTUAL error to the user instead of a generic message
                MessageBox.Show($"Transaction Failed: {errorMessage}", "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public class ProductDisplay
        {
            public int ProductID { get; set; }
            public string RawName { get; set; }
            public decimal BasePrice { get; set; }

            public int MaxCookable { get; set; }
            public bool IsInStock => MaxCookable > 0;

            // Standard Category Split
            public string Category => RawName.Contains("|") ? RawName.Split('|')[0].Trim() : "Others";

            // Extracts just the name: "Classic Pepperoni"
            public string DisplayName
            {
                get
                {
                    string namePart = RawName.Contains("|") ? RawName.Split('|')[1].Trim() : RawName;
                    return namePart.Contains("(") ? namePart.Substring(0, namePart.IndexOf("(")).Trim() : namePart;
                }
            }

            // Extracts the size: "Small" (If it exists)
            public string SizeText
            {
                get
                {
                    if (RawName.Contains("(") && RawName.Contains(")"))
                    {
                        int start = RawName.IndexOf("(") + 1;
                        int length = RawName.IndexOf(")") - start;
                        return RawName.Substring(start, length).Trim();
                    }
                    return ""; // Returns empty if no size is specified (e.g., Drinks)
                }
            }

            public bool HasSize => !string.IsNullOrEmpty(SizeText);

            public string FormattedPrice => $"₱{BasePrice:N0}";
        }
    }
}