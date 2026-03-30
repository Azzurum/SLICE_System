namespace SLICE_System.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.SettingsViewModel();
        }
    }
}