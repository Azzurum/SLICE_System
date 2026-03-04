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
    // =========================================================
    // CART ITEM MODEL: Represents a line item in the current order
    // =========================================================
    public class CartItemVM : INotifyPropertyChanged
    {
        private int _qty;
        public int ProductID { get; set; }
        public string RawName { get; set; }
        public decimal BasePrice { get; set; }

        // Strips the category prefix for a cleaner receipt/cart display
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
                    OnPropertyChanged(nameof(TotalPrice)); // Auto-update total when qty changes
                }
            }
        }

        public decimal TotalPrice => BasePrice * Qty;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // =========================================================
    // PRODUCT DISPLAY MODEL: Represents the menu cards in the POS
    // =========================================================
    public class ProductDisplay
    {
        public int ProductID { get; set; }
        public string RawName { get; set; }
        public decimal BasePrice { get; set; }

        // Image Mapping
        public string ImagePath { get; set; }
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);

        // Smart POS Inventory Depletion Properties
        public int MaxCookable { get; set; }
        public bool IsInStock => MaxCookable > 0;

        // Auto-categorization based on the pipe '|' delimiter in DB
        public string Category => RawName.Contains("|") ? RawName.Split('|')[0].Trim() : "Others";

        // Extracts just the base product name (e.g., "Classic Pepperoni")
        public string DisplayName
        {
            get
            {
                string namePart = RawName.Contains("|") ? RawName.Split('|')[1].Trim() : RawName;
                return namePart.Contains("(") ? namePart.Substring(0, namePart.IndexOf("(")).Trim() : namePart;
            }
        }

        // Extracts the size variant for the UI Badge (e.g., "Large")
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

    // =========================================================
    // MAIN SALES VIEWMODEL: Handles POS logic, cart math, and checkout
    // =========================================================
    public class SalesViewModel : ViewModelBase
    {
        private int _branchId;
        private int _userId;
        private string _currentUserRole; // Used for secure discount application
        private bool _isCooking;

        private SalesRepository _repo;
        private DiscountRepository _discountRepo;

        private string _selectedCategory;
        private string _searchText;
        private List<ProductDisplay> _allProducts; // Master cache of all loaded products

        // --- DISCOUNT & TOTAL PROPERTIES ---
        private Discount _activeDiscount;
        public Discount ActiveDiscount
        {
            get => _activeDiscount;
            set
            {
                if (SetProperty(ref _activeDiscount, value))
                {
                    CalculateTotal(); // Re-run math immediately when a discount is applied/removed
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

        public bool IsCooking
        {
            get => _isCooking;
            set
            {
                if (SetProperty(ref _isCooking, value))
                    CommandManager.InvalidateRequerySuggested(); // Disable buttons during animation
            }
        }

        public DateTime CurrentDate => DateTime.Now;
        public ObservableCollection<ProductDisplay> FilteredProducts { get; set; }
        public ObservableCollection<CartItemVM> CartItems { get; set; }
        public ObservableCollection<string> Categories { get; set; }

        // Triggers UI filtering when category tabs are clicked
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    FilterList();
            }
        }

        // Triggers live UI filtering as the user types in the search bar
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilterList();
            }
        }

        // --- COMMANDS ---
        public ICommand AddToCartCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand IncreaseQtyCommand { get; }
        public ICommand DecreaseQtyCommand { get; }
        public ICommand OpenDiscountCommand { get; }
        public ICommand RemoveDiscountCommand { get; }

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

            // Map DB models to UI Display models (including ImagePath and Inventory Limits)
            _allProducts = rawList.Select(x => new ProductDisplay
            {
                ProductID = x.ProductID,
                RawName = x.ProductName,
                BasePrice = x.BasePrice,
                MaxCookable = x.MaxCookable,
                ImagePath = x.ImagePath
            }).ToList();

            var uniqueCategories = _allProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

            Categories.Clear();
            Categories.Add("All");
            foreach (var cat in uniqueCategories) Categories.Add(cat);

            SelectedCategory = "All";
        }

        // Combined Filter: Handles both Category Tabs and the Search Bar simultaneously
        private void FilterList()
        {
            if (_allProducts == null) return;
            FilteredProducts.Clear();

            var query = _allProducts.AsEnumerable();

            if (SelectedCategory != "All")
                query = query.Where(p => p.Category == SelectedCategory);

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(p => p.DisplayName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var item in query) FilteredProducts.Add(item);
        }

        private void AddItemToCart(ProductDisplay product)
        {
            if (product == null) return;

            var existing = CartItems.FirstOrDefault(c => c.ProductID == product.ProductID);
            int currentQty = existing != null ? existing.Qty : 0;

            // PREVENT OVER-SELLING: Block adding to cart if recipe ingredients are depleted
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

        // Calculates Subtotal, applies Fixed or Percentage discounts, and sets Grand Total
        private void CalculateTotal()
        {
            SubTotal = CartItems.Sum(x => x.TotalPrice);

            if (ActiveDiscount != null)
            {
                if (ActiveDiscount.ValueType == "Percentage")
                    DiscountAmount = SubTotal * (ActiveDiscount.DiscountValue / 100m);
                else
                    DiscountAmount = ActiveDiscount.DiscountValue;

                // Safety net: Prevent discounts from exceeding the total order value
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
            ActiveDiscount = null;
            CalculateTotal();
            CommandManager.InvalidateRequerySuggested();
        }

        private void OpenDiscountDialog()
        {
            if (CartItems.Count == 0)
            {
                MessageBox.Show("Please add items to the cart before applying a discount.", "Cart Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Fetches strictly the discounts allowed for the current user's role
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

            // Capture thread-safe variables for the background task
            var currentDiscount = ActiveDiscount;
            var currentDiscountAmount = DiscountAmount;

            await Task.Run(() =>
            {
                try
                {
                    // 1. Process standard item sales and deduct recipe stock
                    foreach (var item in CartItems)
                        _repo.ProcessSale(_branchId, item.ProductID, item.Qty, _userId);

                    // 2. Log Applied Discount (Offsets Gross Income in Financial Ledger)
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
                    errorMessage = ex.Message;
                }
            });

            if (success)
            {
                await Task.Delay(5500); // Display the POS cooking/receipt animation
                ClearCart();
                IsCooking = false;

                LoadData(); // Force refresh to recalculate new MaxCookable stock limits
            }
            else
            {
                IsCooking = false;
                MessageBox.Show($"Transaction Failed: {errorMessage}", "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}