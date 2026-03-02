using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.ViewModels
{
    public class ReviewSuggestionsViewModel : ViewModelBase
    {
        private readonly SuggestionRepository _repo = new SuggestionRepository();

        // The Kanban Columns
        public ObservableCollection<CustomerSuggestion> PendingSuggestions { get; set; }
        public ObservableCollection<CustomerSuggestion> UnderReviewSuggestions { get; set; }
        public ObservableCollection<CustomerSuggestion> ResolvedSuggestions { get; set; }

        // Sliding Drawer State
        private CustomerSuggestion _selectedSuggestion;
        public CustomerSuggestion SelectedSuggestion { get => _selectedSuggestion; set => SetProperty(ref _selectedSuggestion, value); }

        private string _editNotes;
        public string EditNotes { get => _editNotes; set => SetProperty(ref _editNotes, value); }

        private bool _isDrawerOpen;
        public bool IsDrawerOpen { get => _isDrawerOpen; set => SetProperty(ref _isDrawerOpen, value); }

        // Success Flash State
        private bool _isActionSuccess;
        public bool IsActionSuccess { get => _isActionSuccess; set => SetProperty(ref _isActionSuccess, value); }

        // Commands
        public ICommand SelectCommand { get; }
        public ICommand CloseDrawerCommand { get; }
        public ICommand ReviewCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        public ReviewSuggestionsViewModel()
        {
            PendingSuggestions = new ObservableCollection<CustomerSuggestion>();
            UnderReviewSuggestions = new ObservableCollection<CustomerSuggestion>();
            ResolvedSuggestions = new ObservableCollection<CustomerSuggestion>();

            SelectCommand = new RelayCommand<CustomerSuggestion>(OpenDrawer);
            CloseDrawerCommand = new RelayCommand(() => IsDrawerOpen = false);

            ReviewCommand = new RelayCommand(async () => await ProcessDecision("Under Review"));
            ApproveCommand = new RelayCommand(async () => await ProcessDecision("Approved"));
            RejectCommand = new RelayCommand(async () => await ProcessDecision("Rejected"));

            LoadData();
        }

        private void LoadData()
        {
            PendingSuggestions.Clear();
            UnderReviewSuggestions.Clear();
            ResolvedSuggestions.Clear();

            var all = _repo.GetAllSuggestions();

            foreach (var item in all)
            {
                if (item.Status == "Pending") PendingSuggestions.Add(item);
                else if (item.Status == "Under Review") UnderReviewSuggestions.Add(item);
                else ResolvedSuggestions.Add(item); // Approved and Rejected go to Resolved
            }
        }

        private void OpenDrawer(CustomerSuggestion suggestion)
        {
            if (suggestion == null) return;
            SelectedSuggestion = suggestion;
            EditNotes = suggestion.OwnerNotes ?? ""; // Pre-fill notes if they exist
            IsDrawerOpen = true;
        }

        private async Task ProcessDecision(string newStatus)
        {
            if (SelectedSuggestion == null) return;

            try
            {
                // 1. Save to database
                _repo.UpdateSuggestionStatus(SelectedSuggestion.SuggestionID, newStatus, EditNotes);

                // 2. Trigger the quick green "Success Flash" inside the drawer
                IsActionSuccess = true;
                await Task.Delay(600); // Wait just enough to see it
                IsActionSuccess = false;

                // 3. Slide drawer away
                IsDrawerOpen = false;
                await Task.Delay(300); // Wait for XAML animation to finish

                // 4. Refresh Data - This triggers the visual "Column Jump"
                LoadData();
            }
            catch (Exception)
            {
                // Simple error fallback
            }
        }
    }
}