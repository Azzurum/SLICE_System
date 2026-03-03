using System.Windows.Controls;

namespace SLICE_System.Views
{
    public partial class ManageDiscountsView : UserControl
    {
        public ManageDiscountsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.ManageDiscountsViewModel();
        }
    }
}