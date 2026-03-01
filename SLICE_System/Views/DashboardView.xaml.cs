using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class DashboardView : UserControl, System.ComponentModel.INotifyPropertyChanged
    {
        private DashboardRepository _repo;
        private User _currentUser;

        // Chart Data
        public SeriesCollection BranchSeries { get; set; }
        public string[] BranchLabels { get; set; }
        public Func<double, string> CurrencyFormatter { get; set; }
        public SeriesCollection ProductSeries { get; set; }

        // Dynamic UI Properties
        public bool IsAdmin => _currentUser?.Role == "Super-Admin";
        public Visibility AdminOnlyVisibility => IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        public string DashboardTitle => IsAdmin ? "Enterprise Overview" : "Branch Overview";

        // Metrics Bindings
        private DashboardMetrics _currentMetrics = new DashboardMetrics();
        public DashboardMetrics Metrics
        {
            get => _currentMetrics;
            set { _currentMetrics = value; OnPropertyChanged(nameof(Metrics)); }
        }

        public DashboardView(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _repo = new DashboardRepository();
            CurrencyFormatter = value => value.ToString("C0");

            DataContext = this;

            LoadBranches();
            dpStart.SelectedDate = DateTime.Today;
            dpEnd.SelectedDate = DateTime.Today;

            LoadDashboard();
        }

        private void LoadBranches()
        {
            var branches = _repo.GetAllBranches();
            branches.Insert(0, new Branch { BranchID = 0, BranchName = "All Branches" });
            cmbBranches.ItemsSource = branches;

            if (IsAdmin)
            {
                cmbBranches.SelectedIndex = 0; // Admin sees all
                cmbBranches.IsEnabled = true;
            }
            else
            {
                // Manager is locked to their own branch
                cmbBranches.SelectedValue = _currentUser.BranchID;
                cmbBranches.IsEnabled = false;
            }
        }

        private void LoadDashboard()
        {
            DateTime start = dpStart.SelectedDate ?? DateTime.Today;
            DateTime end = (dpEnd.SelectedDate ?? DateTime.Today).AddDays(1).AddTicks(-1);

            int? branchId = (int?)cmbBranches.SelectedValue;
            if (branchId == 0) branchId = null;

            Metrics = _repo.GetMetrics(start, end, branchId);
            Metrics.RecentActivity = _repo.GetRecentActivity(start, end, branchId);

            // Chart Setup
            BranchSeries = new SeriesCollection
            {
                new ColumnSeries { Title = "Revenue", Values = new ChartValues<decimal>(Metrics.BranchRanking.Select(x => x.TotalRevenue)), Fill = System.Windows.Media.Brushes.SeaGreen }
            };

            if (IsAdmin) // Only show expenses on the chart to Admin
            {
                BranchSeries.Add(new ColumnSeries { Title = "Expenses", Values = new ChartValues<decimal>(Metrics.BranchRanking.Select(x => x.TotalExpense)), Fill = System.Windows.Media.Brushes.IndianRed });
            }

            BranchLabels = Metrics.BranchRanking.Select(x => x.BranchName).ToArray();

            ProductSeries = new SeriesCollection();
            var colors = new[] { "#2C3E50", "#E67E22", "#27AE60", "#2980B9", "#8E44AD" };
            int c = 0;
            foreach (var prod in Metrics.TopProducts)
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

            OnPropertyChanged(null); // Force UI refresh
        }

        private void cmbDateRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlCustomDate == null) return;
            var selected = (cmbDateRange.SelectedItem as ComboBoxItem)?.Content.ToString();
            DateTime today = DateTime.Today;

            switch (selected)
            {
                case "Today": dpStart.SelectedDate = today; dpEnd.SelectedDate = today; pnlCustomDate.Visibility = Visibility.Collapsed; break;
                case "Yesterday": dpStart.SelectedDate = today.AddDays(-1); dpEnd.SelectedDate = today.AddDays(-1); pnlCustomDate.Visibility = Visibility.Collapsed; break;
                case "Last 7 Days": dpStart.SelectedDate = today.AddDays(-7); dpEnd.SelectedDate = today; pnlCustomDate.Visibility = Visibility.Collapsed; break;
                case "Last 30 Days": dpStart.SelectedDate = today.AddDays(-30); dpEnd.SelectedDate = today; pnlCustomDate.Visibility = Visibility.Collapsed; break;
                case "Custom Range": pnlCustomDate.Visibility = Visibility.Visible; break;
            }
            if (selected != "Custom Range") LoadDashboard();
        }

        private void cmbBranches_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadDashboard();
        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadDashboard();

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}