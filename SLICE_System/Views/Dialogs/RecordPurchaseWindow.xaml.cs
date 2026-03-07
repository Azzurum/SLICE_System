using System.Collections.Generic;
using System.Windows;
using SLICE_System.Models;
using SLICE_System.ViewModels;

namespace SLICE_System.Views.Dialogs
{
    public partial class RecordPurchaseWindow : Window
    {
        // NEW: Accepts the list of highlighted items
        public RecordPurchaseWindow(List<MasterInventory> preSelectedItems = null)
        {
            InitializeComponent();

            // Pass the pre-selected items into the ViewModel
            this.DataContext = new PurchaseViewModel(preSelectedItems);
        }
    }
}