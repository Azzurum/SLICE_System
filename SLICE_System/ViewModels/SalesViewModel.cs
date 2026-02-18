using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data; // Required for CollectionViewSource
using System.Windows.Input;

namespace SLICE_System.ViewModels
{
    // =========================================================
    // 1. CART ITEM (Unchanged)
    // =========================================================
    public class CartItem : INotifyPropertyChanged
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

    // =========================================================
    // 2. PRODUCT DISPLAY (Updated for Filtering)
    // =========================================================
    public class ProductDisplay
    {
        public int ProductID { get; set; }
        public string RawName { get; set; }
        public decimal BasePrice { get; set; }

        // Logic: "Pizza | Cheese" -> Category: "Pizza", Name: "Cheese"
        public string Category => RawName.Contains("|") ? RawName.Split('|')[0].Trim() : "Others";
        public string DisplayName => RawName.Contains("|") ? RawName.Split('|')[1].Trim() : RawName;

        public string FormattedPrice => $"₱{BasePrice:N0}";
    }

    // =========================================================
    // 3. MAIN SALES VIEWMODEL
    // =========================================================
    public class SalesViewModel : ViewModelBase
    {
        private int _branchId;
        private int _userId;
        private bool _isCooking;
        private decimal _grandTotal;
        private SalesRepository _repo;
        private string _selectedCategory;

        // Master list of all products (hidden from UI)
        private List<ProductDisplay> _allProducts;

        // --- PROPERTIES ---
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

        // The list shown in the UI
        public ObservableCollection<ProductDisplay> FilteredProducts { get; set; }

        // Cart List
        public ObservableCollection<CartItem> CartItems { get; set; }

        // Category Tabs
        public ObservableCollection<string> Categories { get; set; }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    FilterList();
                }
            }
        }

        public decimal GrandTotal
        {
            get => _grandTotal;
            set => SetProperty(ref _grandTotal, value);
        }

        // --- COMMANDS ---
        public ICommand AddToCartCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand IncreaseQtyCommand { get; }
        public ICommand DecreaseQtyCommand { get; }

        // --- CONSTRUCTOR ---
        public SalesViewModel(int branchId, int userId)
        {
            _branchId = branchId;
            _userId = userId;
            _repo = new SalesRepository();

            CartItems = new ObservableCollection<CartItem>();
            FilteredProducts = new ObservableCollection<ProductDisplay>();
            Categories = new ObservableCollection<string>();

            // Initialize Commands
            AddToCartCommand = new RelayCommand<ProductDisplay>(AddItemToCart);
            ClearCartCommand = new RelayCommand(ClearCart);
            IncreaseQtyCommand = new RelayCommand<object>(IncreaseQty);
            DecreaseQtyCommand = new RelayCommand<object>(DecreaseQty);
            CheckoutCommand = new RelayCommand(ExecuteCheckout, () => CartItems.Count > 0 && !IsCooking);

            LoadData();
        }

        // --- INITIALIZATION ---
        private void LoadData()
        {
            var rawList = _repo.GetMenu();

            // 1. Convert DB items to Display items
            _allProducts = rawList.Select(x => new ProductDisplay
            {
                ProductID = x.ProductID,
                RawName = x.ProductName,
                BasePrice = x.BasePrice
            }).ToList();

            // 2. Extract Categories
            var uniqueCategories = _allProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

            Categories.Clear();
            Categories.Add("All"); // Default tab
            foreach (var cat in uniqueCategories)
            {
                Categories.Add(cat);
            }

            // 3. Set Default
            SelectedCategory = "All";
        }

        // --- FILTERING LOGIC ---
        private void FilterList()
        {
            if (_allProducts == null) return;

            FilteredProducts.Clear();

            IEnumerable<ProductDisplay> query;

            if (SelectedCategory == "All")
            {
                query = _allProducts;
            }
            else
            {
                query = _allProducts.Where(p => p.Category == SelectedCategory);
            }

            foreach (var item in query)
            {
                FilteredProducts.Add(item);
            }
        }

        // --- CART ACTIONS (Same as before) ---
        private void AddItemToCart(ProductDisplay product)
        {
            if (product == null) return;

            var existing = CartItems.FirstOrDefault(c => c.ProductID == product.ProductID);

            if (existing != null)
            {
                existing.Qty++;
            }
            else
            {
                CartItems.Add(new CartItem
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
            if (parameter is CartItem item) { item.Qty++; CalculateTotal(); }
        }

        private void DecreaseQty(object parameter)
        {
            if (parameter is CartItem item)
            {
                item.Qty--;
                if (item.Qty <= 0) CartItems.Remove(item);
                CalculateTotal();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void CalculateTotal() => GrandTotal = CartItems.Sum(x => x.TotalPrice);

        private void ClearCart()
        {
            CartItems.Clear();
            CalculateTotal();
            CommandManager.InvalidateRequerySuggested();
        }

        private async void ExecuteCheckout()
        {
            if (CartItems.Count == 0) return;
            IsCooking = true;
            bool success = false;

            await Task.Run(() =>
            {
                try
                {
                    foreach (var item in CartItems)
                        _repo.ProcessSale(_branchId, item.ProductID, item.Qty, _userId);
                    success = true;
                }
                catch { success = false; }
            });

            if (success)
            {
                await Task.Delay(5500);
                ClearCart();
                IsCooking = false;
            }
            else
            {
                IsCooking = false;
                System.Windows.MessageBox.Show("Transaction Failed.");
            }
        }
    }
}