using System.Windows.Controls;
using SLICE_System.ViewModels;

namespace SLICE_System.Views
{
    public partial class AuditLogView : UserControl
    {
        public AuditLogView()
        {
            InitializeComponent();

            // System Consistency: Properly binds the View to the ViewModel
            this.DataContext = new AuditLogViewModel();
        }
    }
}