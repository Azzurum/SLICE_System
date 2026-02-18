using System.Windows.Controls;
using SLICE_System.ViewModels;

namespace SLICE_System.Views
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();
            this.DataContext = new InventoryViewModel();
        }
    }
}