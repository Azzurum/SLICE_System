using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LiveCharts;
using LiveCharts.Wpf;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class DashboardView : UserControl
    {
        private DashboardRepository _repo;

        // Chart Data Properties (Bound to XAML)
        public SeriesCollection BranchSeries { get; set; }
        public string[] BranchLabels { get; set; }
        public Func<double, string> CurrencyFormatter { get; set; }
        public SeriesCollection ProductSeries { get; set; }
        public SeriesCollection InventorySeries { get; set; }
        public string[] InventoryLabels { get; set; }

        // Goal Tracker Properties
        public decimal DailySalesTarget { get; set; } = 25000;
        public decimal CurrentSales { get; set; }
        public double SalesProgress { get; set; }

        public DashboardView()
        {
            InitializeComponent();
            _repo = new DashboardRepository();
            CurrencyFormatter = value => value.ToString("C0");

            DataContext = this;

            // Initialize Controls
            LoadBranches();

            // Default to Today
            dpStart.SelectedDate = DateTime.Today;
            dpEnd.SelectedDate = DateTime.Today;

            // Load Data
            LoadDashboard();
        }

        private void LoadBranches()
        {
            try
            {
                var branches = _repo.GetAllBranches();
                // Add "All Branches" option manually
                branches.Insert(0, new Branch { BranchID = 0, BranchName = "All Branches" });

                cmbBranches.ItemsSource = branches;
                cmbBranches.SelectedIndex = 0; // Default to "All Branches"
            }
            catch (Exception ex) { MessageBox.Show("Failed to load branches: " + ex.Message); }
        }

        private void LoadDashboard()
        {
            try
            {
                // 1. Get Filter Values from UI
                // FIX: Ensure the Start is 00:00:00 and End is 23:59:59
                DateTime start = dpStart.SelectedDate ?? DateTime.Today;
                DateTime rawEnd = dpEnd.SelectedDate ?? DateTime.Today;

                // Force "End Date" to include the entire day (up to 11:59:59 PM)
                DateTime end = rawEnd.Date.AddDays(1).AddTicks(-1);

                int? branchId = (int?)cmbBranches.SelectedValue;
                if (branchId == 0) branchId = null; // 0 means "All"

                // 2. Fetch Data from Repository
                var metrics = _repo.GetMetrics(start, end, branchId);

                // 3. UI Updates - KPI Cards
                txtRevenue.Text = $"₱{metrics.TotalSalesValue:N2}";
                txtTxCount.Text = $"{metrics.TotalSalesCount} Transactions";
                txtAlerts.Text = metrics.LowStockCount.ToString();
                txtShipments.Text = metrics.PendingShipments.ToString();

                // Goal Progress
                CurrentSales = metrics.TotalSalesValue;
                SalesProgress = DailySalesTarget > 0 ? (double)(CurrentSales / DailySalesTarget) : 0;

                // 4. Branch Chart Config
                BranchSeries = new SeriesCollection
                {
                    new RowSeries
                    {
                        Title = "Revenue",
                        Values = new ChartValues<decimal>(metrics.BranchRanking.Select(x => x.TotalRevenue)),
                        Fill = System.Windows.Media.Brushes.SteelBlue,
                        DataLabels = true
                    }
                };
                BranchLabels = metrics.BranchRanking.Select(x => x.BranchName).ToArray();

                // 5. Product Pie Config
                ProductSeries = new SeriesCollection();
                var colors = new[] { "#E74C3C", "#F1C40F", "#2ECC71", "#3498DB", "#9B59B6" };
                int c = 0;
                foreach (var prod in metrics.TopProducts)
                {
                    ProductSeries.Add(new PieSeries
                    {
                        Title = prod.ProductName,
                        Values = new ChartValues<int> { prod.QuantitySold },
                        DataLabels = true,
                        Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(colors[c % colors.Length])
                    });
                    c++;
                }

                // 6. Inventory Stacked Config
                InventorySeries = new SeriesCollection
                {
                    new StackedColumnSeries { Title = "Good", Values = new ChartValues<int>(metrics.BranchStockHealth.Select(x => x.GoodStock)), Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#27AE60") },
                    new StackedColumnSeries { Title = "Low", Values = new ChartValues<int>(metrics.BranchStockHealth.Select(x => x.LowStock)), Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F39C12") },
                    new StackedColumnSeries { Title = "Critical", Values = new ChartValues<int>(metrics.BranchStockHealth.Select(x => x.CriticalStock)), Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#C0392B") }
                };
                InventoryLabels = metrics.BranchStockHealth.Select(x => x.BranchName).ToArray();

                // 7. Data Grids
                dgAlerts.ItemsSource = metrics.Alerts;
                metrics.RecentActivity = _repo.GetRecentActivity(start, end, branchId);
                dgActivity.ItemsSource = metrics.RecentActivity;

                // Animation Logic
                if (metrics.LowStockCount > 0) StartPulseAnimation(); else StopPulseAnimation();

                // Notify UI to redraw bindings
                OnPropertyChanged(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard: " + ex.Message);
            }
        }

        // --- EVENT HANDLERS ---

        private void cmbDateRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlCustomDate == null) return; // Prevent init crash

            var selected = (cmbDateRange.SelectedItem as ComboBoxItem)?.Content.ToString();

            // FIX: Use DateTime.Today (00:00:00) instead of DateTime.Now
            DateTime today = DateTime.Today;

            // Date Range Presets
            switch (selected)
            {
                case "Today":
                    dpStart.SelectedDate = today;
                    dpEnd.SelectedDate = today;
                    pnlCustomDate.Visibility = Visibility.Collapsed;
                    break;
                case "Yesterday":
                    dpStart.SelectedDate = today.AddDays(-1);
                    dpEnd.SelectedDate = today.AddDays(-1);
                    pnlCustomDate.Visibility = Visibility.Collapsed;
                    break;
                case "Last 7 Days":
                    dpStart.SelectedDate = today.AddDays(-7);
                    dpEnd.SelectedDate = today;
                    pnlCustomDate.Visibility = Visibility.Collapsed;
                    break;
                case "Last 30 Days":
                    dpStart.SelectedDate = today.AddDays(-30);
                    dpEnd.SelectedDate = today;
                    pnlCustomDate.Visibility = Visibility.Collapsed;
                    break;
                case "Custom Range":
                    pnlCustomDate.Visibility = Visibility.Visible;
                    break;
            }

            // Auto-refresh unless custom range is selected
            if (selected != "Custom Range") LoadDashboard();
        }

        private void cmbBranches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Auto-refresh when branch changes
            LoadDashboard();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadDashboard();

        // --- ANIMATION HELPERS ---

        private void StartPulseAnimation()
        {
            DoubleAnimation blurAnim = new DoubleAnimation { From = 15, To = 30, Duration = TimeSpan.FromSeconds(1), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            DoubleAnimation opacityAnim = new DoubleAnimation { From = 0.4, To = 0.7, Duration = TimeSpan.FromSeconds(1), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            AlertShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, blurAnim);
            AlertShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, opacityAnim);
        }

        private void StopPulseAnimation()
        {
            AlertShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, null);
            AlertShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
        }

        // INotifyPropertyChanged Implementation
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}