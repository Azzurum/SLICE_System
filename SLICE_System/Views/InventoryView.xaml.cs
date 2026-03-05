using System.Windows;
using System.Windows.Controls;
using SLICE_System.Models; // Required for the User object
using SLICE_System.ViewModels;

namespace SLICE_System.Views
{
    public partial class InventoryView : UserControl
    {
        // We now require the system to pass the currently logged-in user into this view
        public InventoryView(User currentUser)
        {
            InitializeComponent();

            // Keeps your existing MVVM data binding intact
            this.DataContext = new InventoryViewModel();

            // --- SECURITY CHECK (Role-Based Access Control) ---
            // Check if the user has the authority to manage Central Headquarters inventory
            if (currentUser.Role == "Owner" || currentUser.Role == "Super-Admin" || currentUser.Role == "Logistics Admin")
            {
                // Show the restricted top action buttons
                btnDispatch.Visibility = Visibility.Visible;
                btnBuyStock.Visibility = Visibility.Visible;
                btnAddItem.Visibility = Visibility.Visible;

                // Show the Edit & Trash action column in the DataGrid
                if (colActions != null)
                    colActions.Visibility = Visibility.Visible;
            }
            else
            {
                // Hide the Edit & Trash action column completely for unauthorized users
                if (colActions != null)
                    colActions.Visibility = Visibility.Collapsed;
            }
        }
    }
}