using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SLICE_System.Models
{
    public class MenuItem : INotifyPropertyChanged
    {
        private string _productName;
        private decimal _basePrice;
        private bool _isAvailable;

        public int ProductID { get; set; }

        public string ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(VirtualCategory)); }
        }

        public decimal BasePrice
        {
            get => _basePrice;
            set { _basePrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedPrice)); }
        }

        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                _isAvailable = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(Opacity));
            }
        }

        // --- UI HELPERS ---

        // Extract "Pizza" from "Pizza | Pepperoni"
        public string VirtualCategory
        {
            get => ProductName.Contains("|") ? ProductName.Split('|')[0].Trim() : "General";
        }

        // Extract "Pepperoni" from "Pizza | Pepperoni"
        public string DisplayName
        {
            get => ProductName.Contains("|") ? ProductName.Split('|')[1].Trim() : ProductName;
        }

        public string FormattedPrice => $"₱{BasePrice:N2}";

        // --- UPDATED STATUS TEXT HERE ---
        public string StatusText => IsAvailable ? "Available" : "Unavailable";

        // Green for Available, Red/Gray for Unavailable
        public string StatusColor => IsAvailable ? "#27AE60" : "#C0392B";

        public double Opacity => IsAvailable ? 1.0 : 0.6;

        // MVVM Event Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}