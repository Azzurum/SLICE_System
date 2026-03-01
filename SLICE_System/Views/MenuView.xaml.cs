using System.Windows.Controls;
using SLICE_System.ViewModels;

namespace SLICE_System.Views
{
    public partial class MenuView : UserControl
    {
        public MenuView()
        {
            InitializeComponent();

            // Bind the View to our new Master Recipe Builder ViewModel
            DataContext = new MenuViewModel();
        }
    }
}