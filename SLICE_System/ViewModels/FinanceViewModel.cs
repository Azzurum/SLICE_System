using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.ObjectModel;

namespace SLICE_System.ViewModels
{
    public class FinanceViewModel : ViewModelBase
    {
        private readonly FinanceRepository _repo;
        private decimal _revenue;
        private decimal _expenses;
        private decimal _wasteCost;

        public decimal TotalRevenue
        {
            get => _revenue;
            set => SetProperty(ref _revenue, value);
        }

        public decimal TotalExpenses
        {
            get => _expenses;
            set => SetProperty(ref _expenses, value);
        }

        public decimal TotalWasteCost
        {
            get => _wasteCost;
            set => SetProperty(ref _wasteCost, value);
        }

        public decimal NetProfit => TotalRevenue - TotalExpenses;

        public ObservableCollection<FinancialLedger> RecentTransactions { get; set; }

        public FinanceViewModel()
        {
            _repo = new FinanceRepository();
            RecentTransactions = new ObservableCollection<FinancialLedger>();
            LoadData();
        }

        private void LoadData()
        {
            // 1. Get Totals (Current Month Default)
            var metrics = _repo.GetPnLMetrics(new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1), DateTime.Now);

            TotalRevenue = metrics.TotalRevenue;
            TotalExpenses = metrics.TotalExpenses;
            TotalWasteCost = metrics.TotalWasteCost;

            OnPropertyChanged(nameof(NetProfit));

            // 2. Get List
            var list = _repo.GetRecentTransactions();
            RecentTransactions.Clear();
            foreach (var item in list) RecentTransactions.Add(item);
        }
    }
}