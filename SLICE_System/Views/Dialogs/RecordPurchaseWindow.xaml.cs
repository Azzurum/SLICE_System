using System.Windows;
using SLICE_System.ViewModels;

namespace SLICE_System.Views.Dialogs
{
    public partial class RecordPurchaseWindow : Window
    {
        public RecordPurchaseWindow()
        {
            InitializeComponent();
            var vm = new PurchaseViewModel();
            this.DataContext = vm;

            // Close window when Save is successful (optional Logic)
            // For now, user can manually close or you can implement an event in ViewModel
        }
    }
}