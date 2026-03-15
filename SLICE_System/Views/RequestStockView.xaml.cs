using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FontAwesome.WPF;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class RequestStockView : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<MarketItem> AllMarketItems { get; set; } = new ObservableCollection<MarketItem>();
        public ObservableCollection<MarketItem> FilteredItems { get; set; } = new ObservableCollection<MarketItem>();
        public ObservableCollection<MarketItem> UrgentItems { get; set; } = new ObservableCollection<MarketItem>();
        public ObservableCollection<MarketItem> CartItems { get; set; } = new ObservableCollection<MarketItem>();

        public bool HasUrgentItems => UrgentItems.Any();

        private const int HEADQUARTERS_BRANCH_ID = 4;
        private User _currentUser;
        private InventoryRepository _invRepo = new InventoryRepository();
        private LogisticsRepository _logRepo = new LogisticsRepository();

        // NEW: Search and Category tracking properties
        private string _currentCategory = "All";
        private string _searchText = "";

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyFilters(); // Dynamically filter as user types!
            }
        }

        public RequestStockView(User user)
        {
            InitializeComponent();
            _currentUser = user;
            DataContext = this;

            LoadData();
            StartBackgroundAnimation();
        }

        private void LoadData()
        {
            AllMarketItems.Clear();
            UrgentItems.Clear();

            // 1. Get what HQ has available to order
            var hqStock = _invRepo.GetStockForBranch(HEADQUARTERS_BRANCH_ID);

            // 2. Get my branch's stock to check what I am currently lacking
            var myStock = _invRepo.GetStockForBranch(_currentUser.BranchID.Value);

            foreach (var s in hqStock)
            {
                var myBranchItem = myStock.FirstOrDefault(x => x.ItemID == s.ItemID);
                decimal myQty = myBranchItem != null ? myBranchItem.CurrentQuantity : 0;
                decimal threshold = myBranchItem != null ? myBranchItem.LowStockThreshold : 10;

                var item = new MarketItem
                {
                    ItemID = s.ItemID,
                    Name = s.ItemName,
                    Unit = s.BaseUnit,
                    Category = DetermineCategory(s.ItemName),
                    Icon = GetIconForIngredient(s.ItemName),
                    CurrentStock = s.CurrentQuantity, // HQ's supply limit
                    MyBranchStock = myQty,            // My actual inventory level
                    IsUrgent = myQty <= threshold,    // Triggers the Urgent UI section
                    ImagePath = s.ImagePath
                };

                AllMarketItems.Add(item);
                if (item.IsUrgent) UrgentItems.Add(item);
            }

            ApplyFilters();
            OnPropertyChanged(nameof(HasUrgentItems));
        }

        private string DetermineCategory(string name)
        {
            name = name.ToLower();
            if (name.Contains("cheese") || name.Contains("meat") || name.Contains("pepperoni") || name.Contains("bacon")) return "Perishables";
            if (name.Contains("box") || name.Contains("packaging")) return "Packaging";
            if (name.Contains("dough") || name.Contains("flour")) return "Dough & Flour";
            return "Dry Goods";
        }

        // Updated to use the unified filter logic
        private void Category_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Content != null)
            {
                _currentCategory = rb.Content.ToString();
                ApplyFilters();
            }
        }

        // NEW: Combined logic handles both Radio buttons AND Search box
        private void ApplyFilters()
        {
            FilteredItems.Clear();
            foreach (var item in AllMarketItems)
            {
                bool matchesCategory = _currentCategory == "All" || item.Category == _currentCategory;
                bool matchesSearch = string.IsNullOrWhiteSpace(SearchText) || item.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;

                if (matchesCategory && matchesSearch)
                {
                    FilteredItems.Add(item);
                }
            }
        }

        private void Card_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border b && b.DataContext is MarketItem item)
            {
                ScaleTransform scale = new ScaleTransform(1.0, 1.0);
                b.RenderTransform = scale;
                b.RenderTransformOrigin = new Point(0.5, 0.5);
                DoubleAnimation anim = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(100));
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

                AddToCart(item);
            }
        }

        private void AddToCart(MarketItem item)
        {
            var existing = CartItems.FirstOrDefault(x => x.ItemID == item.ItemID);
            if (existing != null) existing.RequestQty++;
            else
            {
                CartItems.Add(new MarketItem { ItemID = item.ItemID, Name = item.Name, Unit = item.Unit, RequestQty = 1, Icon = item.Icon });
            }
            UpdateTotals();
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MarketItem item)
            {
                CartItems.Remove(item);
                UpdateTotals();
            }
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (CartItems.Count == 0) return;
            try
            {
                MeshLogistics header = new MeshLogistics
                {
                    FromBranchID = HEADQUARTERS_BRANCH_ID,
                    ToBranchID = _currentUser.BranchID.Value,
                    ReceiverID = _currentUser.UserID
                };

                var details = CartItems.Select(x => new WaybillDetail { ItemID = x.ItemID, Quantity = x.RequestQty }).ToList();
                _logRepo.RequestStock(header, details);

                await PlayTicketAnimation();
                CartItems.Clear();
                UpdateTotals();
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private async Task PlayTicketAnimation()
        {
            DoubleAnimation snapUp = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(150));
            TicketTranslate.BeginAnimation(TranslateTransform.YProperty, snapUp);
            await Task.Delay(150);

            DoubleAnimation slideRight = new DoubleAnimation(0, 800, TimeSpan.FromMilliseconds(400)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            TicketTranslate.BeginAnimation(TranslateTransform.XProperty, slideRight);
            TicketRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 5, TimeSpan.FromMilliseconds(400)));
            TicketPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)));

            await Task.Delay(400);

            TicketTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            TicketTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            TicketRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            TicketPanel.BeginAnimation(OpacityProperty, null);
            TicketTranslate.X = -500; TicketTranslate.Y = 0; TicketRotate.Angle = 0; TicketPanel.Opacity = 0;

            DoubleAnimation slideIn = new DoubleAnimation(-500, 0, TimeSpan.FromMilliseconds(500)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
            TicketPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
            TicketTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);

            await Task.Delay(500);
        }

        private void UpdateTotals() { if (txtTotalUnits != null) txtTotalUnits.Text = $"{CartItems.Sum(x => x.RequestQty):N0}"; }

        private FontAwesomeIcon GetIconForIngredient(string name)
        {
            if (string.IsNullOrEmpty(name)) return FontAwesomeIcon.Leaf;
            name = name.ToLower();
            if (name.Contains("cheese")) return FontAwesomeIcon.DotCircleOutline;
            if (name.Contains("dough") || name.Contains("flour")) return FontAwesomeIcon.Cloud;
            if (name.Contains("sauce") || name.Contains("tomato")) return FontAwesomeIcon.Tint;
            if (name.Contains("meat") || name.Contains("pepperoni") || name.Contains("bacon")) return FontAwesomeIcon.Cutlery;
            if (name.Contains("box") || name.Contains("packaging")) return FontAwesomeIcon.Cube;
            return FontAwesomeIcon.Leaf;
        }

        private void StartBackgroundAnimation()
        {
            Dispatcher.InvokeAsync(async () =>
            {
                Random rnd = new Random();
                while (true)
                {
                    if (this.Visibility != Visibility.Visible || AnimCanvas == null) { await Task.Delay(1000); continue; }
                    ImageAwesome icon = new ImageAwesome { Icon = FontAwesomeIcon.PieChart, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAD7A0")), Width = rnd.Next(20, 50), Opacity = 0.2 };
                    Canvas.SetLeft(icon, rnd.Next(0, (int)ActualWidth)); Canvas.SetTop(icon, ActualHeight + 50);
                    AnimCanvas.Children.Add(icon);
                    icon.BeginAnimation(Canvas.TopProperty, new DoubleAnimation { From = ActualHeight + 50, To = -100, Duration = TimeSpan.FromSeconds(rnd.Next(10, 20)) });
                    icon.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 0, TimeSpan.FromSeconds(5)) { BeginTime = TimeSpan.FromSeconds(5) });
                    await Task.Delay(2000);
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MarketItem : INotifyPropertyChanged
    {
        public int ItemID { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }

        public decimal CurrentStock { get; set; } // HQ Stock
        public decimal MyBranchStock { get; set; } // Branch's Actual Stock
        public bool IsUrgent { get; set; }

        public FontAwesomeIcon Icon { get; set; }

        public string ImagePath { get; set; }
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);

        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImagePath)) return null;
                return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "Inventory", ImagePath);
            }
        }

        private decimal _requestQty;
        public decimal RequestQty
        {
            get => _requestQty;
            set { _requestQty = value; OnPropertyChanged(nameof(RequestQty)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}