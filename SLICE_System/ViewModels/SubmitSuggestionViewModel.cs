using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.ViewModels
{
    public class SubmitSuggestionViewModel : ViewModelBase
    {
        private readonly SuggestionRepository _repo = new SuggestionRepository();
        private readonly int _currentUserId;

        public ObservableCollection<string> SuggestionTypes { get; set; }

        private string _selectedType;
        public string SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value))
                {
                    UpdateDynamicPrompt();
                }
            }
        }

        private string _description;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _dynamicPrompt;
        public string DynamicPrompt { get => _dynamicPrompt; set => SetProperty(ref _dynamicPrompt, value); }

        private string _dynamicSubtext;
        public string DynamicSubtext { get => _dynamicSubtext; set => SetProperty(ref _dynamicSubtext, value); }

        // Controls the Success Animation Overlay
        private bool _isSubmitted;
        public bool IsSubmitted { get => _isSubmitted; set => SetProperty(ref _isSubmitted, value); }

        public ICommand SubmitCommand { get; }

        public SubmitSuggestionViewModel(int currentUserId)
        {
            _currentUserId = currentUserId;
            SuggestionTypes = new ObservableCollection<string>
            {
                "Add Menu Item", "Remove Menu Item", "Modify Item", "General Feedback", "Store Operations"
            };

            SubmitCommand = new RelayCommand(SubmitSuggestion);
            SelectedType = SuggestionTypes[0]; // Triggers the prompt update
        }

        private void UpdateDynamicPrompt()
        {
            switch (SelectedType)
            {
                case "Add Menu Item":
                    DynamicPrompt = "Got a killer recipe idea?";
                    DynamicSubtext = "Tell us what we should bake next. Include ingredients and why customers will love it.";
                    break;
                case "Store Operations":
                    DynamicPrompt = "How can we work smarter?";
                    DynamicSubtext = "Suggestions for kitchen flow, POS improvements, or inventory handling.";
                    break;
                case "General Feedback":
                    DynamicPrompt = "What's on your mind?";
                    DynamicSubtext = "Share customer comments, complaints, or general observations with the Owner.";
                    break;
                default:
                    DynamicPrompt = "Help us improve S.L.I.C.E.";
                    DynamicSubtext = "Provide detailed feedback so we can continue to upgrade our operations.";
                    break;
            }
        }

        private async void SubmitSuggestion()
        {
            if (string.IsNullOrWhiteSpace(Description)) return;

            // 1. Save to Database
            _repo.AddSuggestion(new CustomerSuggestion
            {
                SuggestionType = SelectedType,
                Description = Description,
                SubmittedBy = _currentUserId
            });

            // 2. Trigger the Success Animation (XAML listens to this)
            IsSubmitted = true;

            // 3. Wait for 3 seconds while they admire the animation
            await Task.Delay(3000);

            // 4. Reset the form silently
            Description = string.Empty;
            SelectedType = SuggestionTypes[0];
            IsSubmitted = false;
        }
    }
}