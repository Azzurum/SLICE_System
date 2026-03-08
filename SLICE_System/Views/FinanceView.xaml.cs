using System.Windows.Controls;

namespace SLICE_System.Views
{
    /// <summary>
    /// Interaction logic for FinanceView.xaml
    /// </summary>
    public partial class FinanceView : UserControl
    {
        public FinanceView()
        {
            InitializeComponent();

            // Note: DataContext is correctly assigned externally in MainWindow.xaml.cs 
            // via Nav_Finance_Click, which allows the view to remain lightweight.
        }
    }
}