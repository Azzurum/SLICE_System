using System.Windows;
using System.Windows.Controls;
using SLICE_System.Models;
using SLICE_System.ViewModels;

namespace SLICE_System.Views
{
    public partial class InventoryView : UserControl
    {
        public InventoryView(User currentUser)
        {
            InitializeComponent();

            this.DataContext = new InventoryViewModel();

            if (currentUser.Role == "Owner" || currentUser.Role == "Super-Admin" || currentUser.Role == "Logistics Admin")
            {
                btnDispatch.Visibility = Visibility.Visible;
                btnBuyStock.Visibility = Visibility.Visible;
                btnAddItem.Visibility = Visibility.Visible;

                if (colActions != null)
                    colActions.Visibility = Visibility.Visible;
            }
            else
            {
                if (colActions != null)
                    colActions.Visibility = Visibility.Collapsed;
            }
        }
    }
}