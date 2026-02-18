using System.Windows.Controls;
using SLICE_System.ViewModels;

namespace SLICE_System.Views
{
    public partial class SalesView : UserControl
    {
        public SalesView()
        {
            InitializeComponent();

            // In MVVM, we don't need manual event handlers here anymore.
            // The logic is now in SalesViewModel.cs
        }
    }
}