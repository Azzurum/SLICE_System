using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using SLICE_System.Models;
using SLICE_System.Services; // Ensure AccessControlService is here
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

            // 3. Load Default View based on role capabilities
            if (AccessControlService.CanAccess(_currentUser.Role, AccessControlService.Module.Dashboard) || _currentUser.Role == "Logistics Admin")
                Nav_Dashboard_Click(null, null);
            else if (_currentUser.Role == "Logistics Admin")
                Nav_Inventory_Click(null, null); // Fallback specifically for Logistics Admin
            else
                Nav_MyStock_Click(null, null); // Fallback for clerks
        }

        private void ApplyPermissions()
        {
            string r = _currentUser.Role;
            bool isLA = (r == "Logistics Admin");

            // 1. DASHBOARD GROUP
            bool canViewDashboard = AccessControlService.CanAccess(r, AccessControlService.Module.Dashboard) || isLA;
            Toggle(Btn_Dashboard, canViewDashboard);
            Grp_Dash.Visibility = canViewDashboard ? Visibility.Visible : Visibility.Collapsed;

            // 2. OPERATIONS GROUP
            // LA needs to see Incoming, Approvals, Waste, and Recon. They DO NOT need Sales, Branch Inventory, or Request Stock.
            Toggle(Btn_Incoming, AccessControlService.CanAccess(r, AccessControlService.Module.IncomingOrders) || isLA);
            Toggle(Btn_MyInventory, AccessControlService.CanAccess(r, AccessControlService.Module.MyInventory) && !isLA);
            Toggle(Btn_RequestStock, AccessControlService.CanAccess(r, AccessControlService.Module.RequestStock) && !isLA);
            Toggle(Btn_Sales, AccessControlService.CanAccess(r, AccessControlService.Module.SalesPOS) && !isLA);
            Toggle(Btn_Approve, AccessControlService.CanAccess(r, AccessControlService.Module.ApproveRequests) || isLA);
            Toggle(Btn_Waste, AccessControlService.CanAccess(r, AccessControlService.Module.WasteTracker) || isLA);
            Toggle(Btn_Recon, AccessControlService.CanAccess(r, AccessControlService.Module.Reconciliation) || isLA);

            Toggle(Btn_SubmitFeedback, r == "Clerk" || r == "Manager" || isLA);

            bool anyOps = Btn_Incoming.Visibility == Visibility.Visible ||
                          Btn_MyInventory.Visibility == Visibility.Visible ||
                          Btn_RequestStock.Visibility == Visibility.Visible ||
                          Btn_Sales.Visibility == Visibility.Visible ||
                          Btn_Approve.Visibility == Visibility.Visible ||
                          Btn_Waste.Visibility == Visibility.Visible ||
                          Btn_Recon.Visibility == Visibility.Visible ||
                          Btn_SubmitFeedback.Visibility == Visibility.Visible;

            Grp_Ops.Visibility = anyOps ? Visibility.Visible : Visibility.Collapsed;

            // 3. ADMIN GROUP
            // LA only needs access to the Central Warehouse in the admin group.
            Toggle(Btn_Menu, AccessControlService.CanAccess(r, AccessControlService.Module.MenuRegistry) && !isLA);
            Toggle(Btn_Inventory, AccessControlService.CanAccess(r, AccessControlService.Module.GlobalInventory) || isLA);
            Toggle(Btn_Users, AccessControlService.CanAccess(r, AccessControlService.Module.UserAdmin) && !isLA);
            Toggle(Btn_Audit, AccessControlService.CanAccess(r, AccessControlService.Module.AuditLogs) && !isLA);

            Toggle(Btn_Finance, r == "Super-Admin" || r == "Owner");
            Toggle(Btn_ManageDiscounts, r == "Super-Admin" || r == "Owner");
            Toggle(Btn_ReviewFeedback, r == "Super-Admin" || r == "Owner");

            // Hide Admin Header if all children are hidden
            bool anyAdmin = Btn_Menu.Visibility == Visibility.Visible ||
                            Btn_Inventory.Visibility == Visibility.Visible ||
                            Btn_Users.Visibility == Visibility.Visible ||
                            Btn_Audit.Visibility == Visibility.Visible ||
                            Btn_Finance.Visibility == Visibility.Visible ||
                            Btn_ManageDiscounts.Visibility == Visibility.Visible ||
                            Btn_ReviewFeedback.Visibility == Visibility.Visible;

            Grp_Admin.Visibility = anyAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        // Helper to shorten code
        private void Toggle(UIElement element, bool canAccess)
        {
            element.Visibility = canAccess ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- NAVIGATION HANDLERS ---

        public void Nav_Dashboard_Click(object sender, RoutedEventArgs e)
            => LoadView("Executive Dashboard", new Views.DashboardView(_currentUser));

        private void Nav_Incoming_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Incoming Deliveries";
            MainContentArea.Child = new Views.ReceiveShipmentView(_currentUser);
        }

        private void Nav_MyStock_Click(object sender, RoutedEventArgs e)
            => LoadView("My Branch Inventory", new Views.BranchStockView(_currentUser.BranchID ?? 0));

        private void Nav_RequestStock_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Stock Requisition";
            MainContentArea.Child = new Views.RequestStockView(_currentUser);
        }

        private void Nav_Sales_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Revenue Analytics";
            var view = new Views.SalesView();
            var viewModel = new SalesViewModel(_currentUser.BranchID.GetValueOrDefault(), _currentUser.UserID);
            view.DataContext = viewModel;
            MainContentArea.Child = view;
        }

        private void Nav_ApproveRequests_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Manager Approvals";
            MainContentArea.Child = new Views.ManageRequestsView(_currentUser);
        }

        private void Nav_Waste_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Waste & Loss Tracker";
            MainContentArea.Child = new Views.WasteTrackerView(_currentUser);
        }

        private void Nav_Recon_Click(object sender, RoutedEventArgs e)
            => LoadView("Stock Reconciliation", new Views.ReconciliationView(_currentUser.BranchID ?? 0, _currentUser.UserID));

        private void Nav_Menu_Click(object sender, RoutedEventArgs e) => LoadView("Menu Registry", new Views.MenuView());

        private void Nav_Inventory_Click(object sender, RoutedEventArgs e) => LoadView("Central Warehouse", new Views.InventoryView(_currentUser));

        private void Nav_Users_Click(object sender, RoutedEventArgs e) => LoadView("User Administration", new Views.UsersView());
        private void Nav_Audit_Click(object sender, RoutedEventArgs e) => LoadView("System Audit Logs", new Views.AuditLogView());

        private void Nav_Finance_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Financial Performance";
            var view = new Views.FinanceView();
            var viewModel = new FinanceViewModel();
            view.DataContext = viewModel;
            MainContentArea.Child = view;
        }

        private void Nav_ManageDiscounts_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Pricing & Promotions Rules";
            MainContentArea.Child = new Views.ManageDiscountsView();
        }

        private void Nav_SubmitFeedback_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Submit Feedback";
            var view = new Views.SubmitSuggestionView();
            var viewModel = new ViewModels.SubmitSuggestionViewModel(_currentUser.UserID);
            view.DataContext = viewModel;
            MainContentArea.Child = view;
        }

        private void Nav_ReviewFeedback_Click(object sender, RoutedEventArgs e)
        {
            txtPageTitle.Text = "Review Suggestions";
            var view = new Views.ReviewSuggestionsView();
            var viewModel = new ViewModels.ReviewSuggestionsViewModel();
            view.DataContext = viewModel;
            MainContentArea.Child = view;
        }

        // HELPERS
        private void LoadView(string title, UIElement view)
        {
            txtPageTitle.Text = title;
            MainContentArea.Child = view;
        }

        // WINDOW CONTROLS
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) => this.DragMove();
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            Views.AboutView about = new Views.AboutView();
            about.ShowDialog();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to sign out?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Views.LoginView login = new Views.LoginView();
                login.Show();
                this.Close();
            }
        }
    }
}