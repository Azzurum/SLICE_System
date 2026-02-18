using System;
using System.Collections.Generic;
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
        public ObservableCollection<MarketItem> MarketItems { get; set; } = new ObservableCollection<MarketItem>();
        public ObservableCollection<MarketItem> CartItems { get; set; } = new ObservableCollection<MarketItem>();
        public List<Branch> AvailableBranches { get; set; }

        private User _currentUser;
        private InventoryRepository _invRepo = new InventoryRepository();
        private LogisticsRepository _logRepo = new LogisticsRepository();

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
            AvailableBranches = _invRepo.GetShippingDestinations(_currentUser.BranchID.GetValueOrDefault());
            if (AvailableBranches.Any() && cmbSource != null) cmbSource.SelectedIndex = 0;

            var stocks = _invRepo.GetStockForBranch(_currentUser.BranchID.GetValueOrDefault());
            foreach (var s in stocks)
            {
                MarketItems.Add(new MarketItem
                {
                    ItemID = s.ItemID,
                    Name = s.ItemName,
                    Unit = s.BaseUnit,
                    Icon = GetIconForIngredient(s.ItemName),
                    CurrentStock = s.CurrentQuantity
                });
            }
        }

        private void Card_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border b && b.DataContext is MarketItem item)
            {
                // Click Animation
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
                CartItems.Add(new MarketItem
                {
                    ItemID = item.ItemID,
                    Name = item.Name,
                    Unit = item.Unit,
                    RequestQty = 1,
                    Icon = item.Icon
                });
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
            if (cmbSource.SelectedValue == null) { MessageBox.Show("Select a source."); return; }

            try
            {
                MeshLogistics header = new MeshLogistics
                {
                    FromBranchID = (int)cmbSource.SelectedValue,
                    ToBranchID = _currentUser.BranchID.Value,
                    ReceiverID = _currentUser.UserID
                };

                List<WaybillDetail> details = CartItems.Select(x => new WaybillDetail
                {
                    ItemID = x.ItemID,
                    Quantity = x.RequestQty
                }).ToList();

                _logRepo.RequestStock(header, details);

                // --- PLAY ANIMATION ---
                await PlayTicketAnimation();

                // Clear Cart AFTER animation starts to look like a fresh ticket
                CartItems.Clear();
                UpdateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        // --- REPLAYABLE ANIMATION LOGIC ---
        private async Task PlayTicketAnimation()
        {
            // 1. TICKET SNAP (Lift off paper)
            DoubleAnimation snapUp = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(150));
            TicketTranslate.BeginAnimation(TranslateTransform.YProperty, snapUp);
            await Task.Delay(150);

            // 2. SLIDE RIGHT (Along the rail)
            DoubleAnimation slideRight = new DoubleAnimation(0, 800, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            TicketTranslate.BeginAnimation(TranslateTransform.XProperty, slideRight);

            // 3. TILT (As it moves)
            DoubleAnimation tilt = new DoubleAnimation(0, 5, TimeSpan.FromMilliseconds(400));
            TicketRotate.BeginAnimation(RotateTransform.AngleProperty, tilt);

            // 4. FADE OUT
            DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            TicketPanel.BeginAnimation(OpacityProperty, fade);

            await Task.Delay(400);

            // --- RESET STATE (Invisible) ---
            // We must explicitly STOP animations by passing 'null' before setting values manually
            TicketTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            TicketTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            TicketRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            TicketPanel.BeginAnimation(OpacityProperty, null);

            // Move to Start Position (Hidden Left)
            TicketTranslate.X = -500;
            TicketTranslate.Y = 0;
            TicketRotate.Angle = 0;
            TicketPanel.Opacity = 0;

            // --- SLIDE IN NEW TICKET (Entrance) ---
            DoubleAnimation slideIn = new DoubleAnimation(-500, 0, TimeSpan.FromMilliseconds(500))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));

            TicketPanel.BeginAnimation(OpacityProperty, fadeIn);
            TicketTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);

            await Task.Delay(500);
        }

        private void UpdateTotals()
        {
            if (txtTotalUnits != null) txtTotalUnits.Text = $"{CartItems.Sum(x => x.RequestQty):N0}";
        }

        private FontAwesomeIcon GetIconForIngredient(string name)
        {
            name = name.ToLower();
            if (name.Contains("cheese")) return FontAwesomeIcon.DotCircleOutline;
            if (name.Contains("dough") || name.Contains("flour")) return FontAwesomeIcon.Cloud;
            if (name.Contains("sauce") || name.Contains("tomato")) return FontAwesomeIcon.Tint;
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
                    ImageAwesome icon = new ImageAwesome
                    {
                        Icon = FontAwesomeIcon.PieChart,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAD7A0")),
                        Width = rnd.Next(20, 50),
                        Opacity = 0.2
                    };
                    Canvas.SetLeft(icon, rnd.Next(0, (int)ActualWidth));
                    Canvas.SetTop(icon, ActualHeight + 50);
                    AnimCanvas.Children.Add(icon);

                    DoubleAnimation flyUp = new DoubleAnimation
                    {
                        From = ActualHeight + 50,
                        To = -100,
                        Duration = TimeSpan.FromSeconds(rnd.Next(10, 20))
                    };
                    DoubleAnimation fade = new DoubleAnimation(0.2, 0, TimeSpan.FromSeconds(5)) { BeginTime = TimeSpan.FromSeconds(5) };
                    icon.BeginAnimation(Canvas.TopProperty, flyUp);
                    icon.BeginAnimation(OpacityProperty, fade);
                    await Task.Delay(2000);
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // --- THIS WAS MISSING BEFORE ---
    public class MarketItem : INotifyPropertyChanged
    {
        public int ItemID { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public decimal CurrentStock { get; set; }
        public FontAwesomeIcon Icon { get; set; }

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