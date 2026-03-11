using SLICE_System.Data;
using SLICE_System.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace SLICE_System.ViewModels
{
    public class AuditLogViewModel : ViewModelBase
    {
        private readonly AuditRepository _repo;
        private string _searchText;
        private DateTime? _selectedDate;
        private CancellationTokenSource _searchCancellationTokenSource;

        public ObservableCollection<AuditEntry> AuditLogs { get; set; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Triggers the debounced search to prevent UI freezing
                    TriggerSearchAsync();
                }
            }
        }

        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    LoadLogsAsync();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ClearDateCommand { get; }

        public AuditLogViewModel()
        {
            _repo = new AuditRepository();
            AuditLogs = new ObservableCollection<AuditEntry>();

            RefreshCommand = new RelayCommand(LoadLogsAsync);
            ExportCommand = new RelayCommand(ExportLogsToCSV, () => AuditLogs.Any());
            ClearDateCommand = new RelayCommand(() => SelectedDate = null);

            LoadLogsAsync();
        }

        // --- DEBOUNCED SEARCH LOGIC ---
        // Waits 300ms after the user stops typing before hitting the database
        private async void TriggerSearchAsync()
        {
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            var token = _searchCancellationTokenSource.Token;

            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                {
                    LoadLogsAsync();
                }
            }
            catch (TaskCanceledException) { /* Ignored */ }
        }

        private async void LoadLogsAsync()
        {
            try
            {
                string currentSearch = SearchText ?? string.Empty;
                DateTime? filterDate = SelectedDate;

                // Offload to background thread to keep UI perfectly smooth
                var logs = await Task.Run(() =>
                {
                    var rawLogs = _repo.GetSystemHistory(currentSearch);

                    // Apply Date Filter in memory if a date is selected
                    if (filterDate.HasValue)
                    {
                        rawLogs = rawLogs.Where(l => l.Timestamp.Date == filterDate.Value.Date).ToList();
                    }

                    // Limit to top 200 to prevent RAM overflow on massive databases
                    return rawLogs.Take(200).ToList();
                });

                // Safely update UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AuditLogs.Clear();
                    foreach (var log in logs) AuditLogs.Add(log);
                    CommandManager.InvalidateRequerySuggested(); // Refresh Export button state
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load logs: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportLogsToCSV()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"SLICE_Audit_Logs_{DateTime.Now:yyyyMMdd}.csv",
                Title = "Export Audit Logs"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new StringBuilder();
                    // NEW: Added Reference Column to the CSV Header
                    csv.AppendLine("Timestamp,Action Type,Reference,User,Branch,Details");

                    foreach (var log in AuditLogs)
                    {
                        // Wrap description in quotes to prevent commas from breaking the CSV columns
                        string safeDescription = $"\"{log.Description?.Replace("\"", "\"\"")}\"";

                        // NEW: Added log.ReferenceNumber to the row output
                        csv.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.ActionType},{log.ReferenceNumber},{log.PerformedBy},{log.BranchName},{safeDescription}");
                    }

                    File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show("Logs successfully exported!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export logs: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}