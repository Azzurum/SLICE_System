using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using SLICE_System.Models;
using SLICE_System.Services;
using SLICE_System.ViewModels;

namespace SLICE_System
{
    public partial class MainWindow : Window
    {
        private User _currentUser;

        public MainWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;

            // 1. Setup User Profile
            txtUserBadge.Text = _currentUser.FullName;
            txtUserRole.Text = _currentUser.Role;

            // 2. Apply Role-Based Security
            ApplyPermissions();

            // 3. Load Default View
            if (AccessControlService.CanAccess(_currentUser.Role, AccessControlService.Module.Dashboard) || _currentUser.Role == "Logistics Admin")
                Nav_Dashboard_Click(null, null);
            else
                Nav_MyStock_Click(null, null);
        }

        // --- NEW: BRANDING CLICK HANDLER (System Metadata) ---
        private void Nav_About_Click(object sender, RoutedEventArgs e)
        {
            Views.AboutView about = new Views.AboutView();
            about.Owner = this;
            about.ShowDialog();
        }

        // --- UPDATED: HELP CLICK HANDLER (User Instructions) ---
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            // Opens the new User Manual Window
            var manualWindow = new Views.Dialogs.UserManualWindow();
            manualWindow.Owner = this;
            manualWindow.Show(); // .Show() allows them to keep it open while using the app
        }

        private void ApplyPermissions()
        {
            string r = _currentUser.Role;
            bool isLA = (r == "Logistics Admin");

            Toggle(Btn_Dashboard, AccessControlService.CanAccess(r, AccessControlService.Module.Dashboard) || isLA);
            Grp_Dash.Visibility = Btn_Dashboard.Visibility;

            Toggle(Btn_Incoming, AccessControlService.CanAccess(r, AccessControlService.Module.IncomingOrders) || isLA);
            Toggle(Btn_MyInventory, AccessControlService.CanAccess(r, AccessControlService.Module.MyInventory) && !isLA);
            Toggle(Btn_RequestStock, AccessControlService.CanAccess(r, AccessControlService.Module.RequestStock) && !isLA);
            Toggle(Btn_Sales, AccessControlService.CanAccess(r, AccessControlService.Module.SalesPOS) && r != "Super-Admin" && !isLA);
            Toggle(Btn_Approve, AccessControlService.CanAccess(r, AccessControlService.Module.ApproveRequests) || isLA);
            Toggle(Btn_Waste, AccessControlService.CanAccess(r, AccessControlService.Module.WasteTracker) || isLA);
            Toggle(Btn_Recon, AccessControlService.CanAccess(r, AccessControlService.Module.Reconciliation) || isLA);
            Toggle(Btn_SubmitFeedback, r == "Clerk" || r == "Manager" || isLA);

            Grp_Ops.Visibility = (anyOpsVisible()) ? Visibility.Visible : Visibility.Collapsed;

            Toggle(Btn_Menu, AccessControlService.CanAccess(r, AccessControlService.Module.MenuRegistry) && !isLA);
            Toggle(Btn_Inventory, AccessControlService.CanAccess(r, AccessControlService.Module.GlobalInventory) || isLA);
            Toggle(Btn_Users, AccessControlService.CanAccess(r, AccessControlService.Module.UserAdmin) && !isLA);
            Toggle(Btn_Audit, AccessControlService.CanAccess(r, AccessControlService.Module.AuditLogs) && !isLA);
            Toggle(Btn_Finance, r == "Super-Admin" || r == "Owner");
            Toggle(Btn_ManageDiscounts, r == "Super-Admin" || r == "Owner");
            Toggle(Btn_ReviewFeedback, r == "Super-Admin" || r == "Owner");

            Grp_Admin.Visibility = (anyAdminVisible()) ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool anyOpsVisible() => Btn_Incoming.Visibility == Visibility.Visible || Btn_MyInventory.Visibility == Visibility.Visible || Btn_RequestStock.Visibility == Visibility.Visible || Btn_Sales.Visibility == Visibility.Visible || Btn_Approve.Visibility == Visibility.Visible || Btn_Waste.Visibility == Visibility.Visible || Btn_Recon.Visibility == Visibility.Visible || Btn_SubmitFeedback.Visibility == Visibility.Visible;
        private bool anyAdminVisible() => Btn_Menu.Visibility == Visibility.Visible || Btn_Inventory.Visibility == Visibility.Visible || Btn_Users.Visibility == Visibility.Visible || Btn_Audit.Visibility == Visibility.Visible || Btn_Finance.Visibility == Visibility.Visible || Btn_ManageDiscounts.Visibility == Visibility.Visible || Btn_ReviewFeedback.Visibility == Visibility.Visible;

        private void Toggle(UIElement element, bool canAccess) => element.Visibility = canAccess ? Visibility.Visible : Visibility.Collapsed;

        // Navigation Handlers
        public void Nav_Dashboard_Click(object sender, RoutedEventArgs e) => LoadView("Executive Dashboard", new Views.DashboardView(_currentUser));
        private void Nav_Incoming_Click(object sender, RoutedEventArgs e) => LoadView("Incoming Deliveries", new Views.ReceiveShipmentView(_currentUser));
        private void Nav_MyStock_Click(object sender, RoutedEventArgs e) => LoadView("My Branch Inventory", new Views.BranchStockView(_currentUser.BranchID ?? 0));
        private void Nav_RequestStock_Click(object sender, RoutedEventArgs e) => LoadView("Stock Requisition", new Views.RequestStockView(_currentUser));
        private void Nav_Sales_Click(object sender, RoutedEventArgs e) => LoadView("Revenue Analytics", new Views.SalesView { DataContext = new SalesViewModel(_currentUser.BranchID.GetValueOrDefault(), _currentUser.UserID) });
        private void Nav_ApproveRequests_Click(object sender, RoutedEventArgs e) => LoadView("Manager Approvals", new Views.ManageRequestsView(_currentUser));
        private void Nav_Waste_Click(object sender, RoutedEventArgs e) => LoadView("Waste & Loss Tracker", new Views.WasteTrackerView(_currentUser));
        private void Nav_Recon_Click(object sender, RoutedEventArgs e) => LoadView("Stock Reconciliation", new Views.ReconciliationView(_currentUser.BranchID ?? 0, _currentUser.UserID));
        private void Nav_Menu_Click(object sender, RoutedEventArgs e) => LoadView("Menu Registry", new Views.MenuView());
        private void Nav_Inventory_Click(object sender, RoutedEventArgs e) => LoadView("Central Warehouse", new Views.InventoryView(_currentUser));
        private void Nav_Users_Click(object sender, RoutedEventArgs e) => LoadView("User Administration", new Views.UsersView());
        private void Nav_Audit_Click(object sender, RoutedEventArgs e) => LoadView("System Audit Logs", new Views.AuditLogView());
        private void Nav_Finance_Click(object sender, RoutedEventArgs e) => LoadView("Financial Performance", new Views.FinanceView { DataContext = new FinanceViewModel() });
        private void Nav_ManageDiscounts_Click(object sender, RoutedEventArgs e) => LoadView("Pricing & Promotions Rules", new Views.ManageDiscountsView());
        private void Nav_SubmitFeedback_Click(object sender, RoutedEventArgs e) => LoadView("Submit Feedback", new Views.SubmitSuggestionView { DataContext = new ViewModels.SubmitSuggestionViewModel(_currentUser.UserID) });
        private void Nav_ReviewFeedback_Click(object sender, RoutedEventArgs e) => LoadView("Review Suggestions", new Views.ReviewSuggestionsView { DataContext = new ViewModels.ReviewSuggestionsViewModel() });

        private void LoadView(string title, UIElement view)
        {
            txtPageTitle.Text = title;
            MainContentArea.Child = view;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to sign out?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                new Views.LoginView().Show();
                this.Close();
            }
        }
    }
}