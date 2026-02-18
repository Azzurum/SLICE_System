using System.Windows.Controls;
using SLICE_System.Data;

namespace SLICE_System.Views
{
    public partial class AuditLogView : UserControl
    {
        private AuditRepository _repo;

        public AuditLogView()
        {
            InitializeComponent();
            _repo = new AuditRepository();
            LoadLogs();
        }

        private void LoadLogs(string search = "")
        {
            // Simple fetch. In a real app with thousands of logs, 
            // you would add pagination here.
            dgAudit.ItemsSource = _repo.GetSystemHistory(search);
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Auto-search as user types
            LoadLogs(txtSearch.Text);
        }
    }
}