using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using SLICE_System.Models;
using SLICE_System.Services;
using SLICE_System.ViewModels;

namespace SLICE_System
{
    public partial class MainWindow : Window
    {
        private User _currentUser;
        private DispatcherTimer _idleTimer;

        public MainWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;

            // 1. Initialize Header
            txtUserBadge.Text = _currentUser.FullName;
            txtUserRole.Text = _currentUser.Role;

            // 2. Configure Sidebar Visibility
            ApplyPermissions();

            // 3. Load Startup View (Delayed to ensure content frame is fully rendered first)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (AccessControlService.CanAccess(_currentUser.Role, AccessControlService.Module.Dashboard) || _currentUser.Role == "Logistics Admin")
                    Nav_Dashboard_Click(null, null);
                else
                    Nav_MyStock_Click(null, null);
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            // 4. Initialize Security Timeout (3 Mins)
            _idleTimer = new DispatcherTimer();
            _idleTimer.Interval = TimeSpan.FromMinutes(3);
            _idleTimer.Tick += IdleTimer_Tick;
            _idleTimer.Start();

            // 5. Attach global input hook to reset idle timer
            InputManager.Current.PreProcessInput += OnUserActivity;
        }

        // --- SESSION SECURITY ---
        private void OnUserActivity(object sender, PreProcessInputEventArgs e)
        {
            // Reset timer on any mouse or keyboard event
            if (e.StagingItem.Input is MouseEventArgs || e.StagingItem.Input is KeyboardEventArgs)
            {
                _idleTimer.Stop();
                _idleTimer.Start();
            }
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            // Halt monitoring
            _idleTimer.Stop();
            InputManager.Current.PreProcessInput -= OnUserActivity;

            // Hide the active UI immediately to prevent unauthorized interaction
            this.Hide();

            MessageBox.Show("For your security, your session has timed out due to inactivity.", "Session Expired", MessageBoxButton.OK, MessageBoxImage.Warning);

            // Force hard logout
            new Views.LoginView().Show();
            this.Close();
        }

        // --- GLOBAL UI ACTIONS ---
        private void Nav_About_Click(object sender, RoutedEventArgs e)
        {
            Views.AboutView about = new Views.AboutView();
            about.Owner = this;
            about.ShowDialog();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            var manualWindow = new Views.Dialogs.UserManualWindow();
            manualWindow.Owner = this;
            manualWindow.Show();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        // --- ROLE EVALUATION ---
        private void ApplyPermissions()
        {
            string r = _currentUser.Role;

            // Logistics Admin acts as a dynamic override for specific warehouse modules
            bool isLA = (r == "Logistics Admin");

            // Evaluate Dashboard Group
            Toggle(Btn_Dashboard, AccessControlService.CanAccess(r, AccessControlService.Module.Dashboard) || isLA);
            Grp_Dash.Visibility = Btn_Dashboard.Visibility;

            // Evaluate Operations Group
            Toggle(Btn_Incoming, AccessControlService.CanAccess(r, AccessControlService.Module.IncomingOrders) || isLA);
            Toggle(Btn_MyInventory, AccessControlService.CanAccess(r, AccessControlService.Module.MyInventory) && !isLA);
            Toggle(Btn_RequestStock, AccessControlService.CanAccess(r, AccessControlService.Module.RequestStock) && !isLA);
            Toggle(Btn_Sales, AccessControlService.CanAccess(r, AccessControlService.Module.SalesPOS) && r != "Super-Admin" && !isLA);
            Toggle(Btn_Approve, AccessControlService.CanAccess(r, AccessControlService.Module.ApproveRequests) || isLA);
            Toggle(Btn_Waste, AccessControlService.CanAccess(r, AccessControlService.Module.WasteTracker) || isLA);
            Toggle(Btn_Recon, AccessControlService.CanAccess(r, AccessControlService.Module.Reconciliation) || isLA);
            Toggle(Btn_SubmitFeedback, r == "Clerk" || r == "Manager" || isLA);

            Grp_Ops.Visibility = (anyOpsVisible()) ? Visibility.Visible : Visibility.Collapsed;

            // Evaluate Management Group
            Toggle(Btn_Menu, AccessControlService.CanAccess(r, AccessControlService.Module.MenuRegistry) && !isLA);
            Toggle(Btn_Inventory, AccessControlService.CanAccess(r, AccessControlService.Module.GlobalInventory) || isLA);
            Toggle(Btn_Users, AccessControlService.CanAccess(r, AccessControlService.Module.UserAdmin) && !isLA);
            Toggle(Btn_Audit, AccessControlService.CanAccess(r, AccessControlService.Module.AuditLogs) && !isLA);
            Toggle(Btn_Finance, r == "Super-Admin");
            Toggle(Btn_ManageDiscounts, r == "Super-Admin");
            Toggle(Btn_ReviewFeedback, r == "Super-Admin");

            Grp_Admin.Visibility = (anyAdminVisible()) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Helper functions to check if parent category headers should be visible
        private bool anyOpsVisible() => Btn_Incoming.Visibility == Visibility.Visible || Btn_MyInventory.Visibility == Visibility.Visible || Btn_RequestStock.Visibility == Visibility.Visible || Btn_Sales.Visibility == Visibility.Visible || Btn_Approve.Visibility == Visibility.Visible || Btn_Waste.Visibility == Visibility.Visible || Btn_Recon.Visibility == Visibility.Visible || Btn_SubmitFeedback.Visibility == Visibility.Visible;
        private bool anyAdminVisible() => Btn_Menu.Visibility == Visibility.Visible || Btn_Inventory.Visibility == Visibility.Visible || Btn_Users.Visibility == Visibility.Visible || Btn_Audit.Visibility == Visibility.Visible || Btn_Finance.Visibility == Visibility.Visible || Btn_ManageDiscounts.Visibility == Visibility.Visible || Btn_ReviewFeedback.Visibility == Visibility.Visible;

        private void Toggle(UIElement element, bool canAccess) => element.Visibility = canAccess ? Visibility.Visible : Visibility.Collapsed;

        // --- VIEW ROUTING ---
        public void Nav_Dashboard_Click(object sender, RoutedEventArgs e) => LoadView("Dashboard", new Views.DashboardView(_currentUser));
        private void Nav_Incoming_Click(object sender, RoutedEventArgs e) => LoadView("Incoming Deliveries", new Views.ReceiveShipmentView(_currentUser));
        private void Nav_MyStock_Click(object sender, RoutedEventArgs e) => LoadView("My Branch Inventory", new Views.BranchStockView(_currentUser.BranchID ?? 0));
        private void Nav_RequestStock_Click(object sender, RoutedEventArgs e) => LoadView("Stock Requisition", new Views.RequestStockView(_currentUser));
        private void Nav_Sales_Click(object sender, RoutedEventArgs e) => LoadView("Point of Sale", new Views.SalesView { DataContext = new SalesViewModel(_currentUser.BranchID.GetValueOrDefault(), _currentUser.UserID, _currentUser.Role) });
        private void Nav_ApproveRequests_Click(object sender, RoutedEventArgs e) => LoadView("Manager Approvals", new Views.ManageRequestsView(_currentUser));
        private void Nav_Waste_Click(object sender, RoutedEventArgs e) => LoadView("Waste & Loss Tracker", new Views.WasteTrackerView(_currentUser));
        private void Nav_Recon_Click(object sender, RoutedEventArgs e) => LoadView("Stock Reconciliation", new Views.ReconciliationView(_currentUser.BranchID ?? 0, _currentUser.UserID));
        private void Nav_Menu_Click(object sender, RoutedEventArgs e) => LoadView("Menu Registry", new Views.MenuView());
        private void Nav_Inventory_Click(object sender, RoutedEventArgs e) => LoadView("Central Warehouse", new Views.InventoryView(_currentUser));
        private void Nav_Users_Click(object sender, RoutedEventArgs e) => LoadView("User Administration", new Views.UsersView());
        private void Nav_Audit_Click(object sender, RoutedEventArgs e) => LoadView("Audit Logs", new Views.AuditLogView());
        private void Nav_Finance_Click(object sender, RoutedEventArgs e) => LoadView("Financial Performance", new Views.FinanceView { DataContext = new FinanceViewModel() });
        private void Nav_ManageDiscounts_Click(object sender, RoutedEventArgs e) => LoadView("Pricing & Promotions Rules", new Views.ManageDiscountsView());
        private void Nav_SubmitFeedback_Click(object sender, RoutedEventArgs e) => LoadView("Submit Feedback", new Views.SubmitSuggestionView { DataContext = new ViewModels.SubmitSuggestionViewModel(_currentUser.UserID) });
        private void Nav_ReviewFeedback_Click(object sender, RoutedEventArgs e) => LoadView("Review Suggestions", new Views.ReviewSuggestionsView { DataContext = new ViewModels.ReviewSuggestionsViewModel() });

        private void LoadView(string title, UIElement view)
        {
            txtPageTitle.Text = title;
            MainContentArea.Child = view;
        }

        // --- LOGOUT FLOW ---
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to sign out?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Enforce Z-Reading execution for operational roles
                if (AccessControlService.CanAccess(_currentUser.Role, AccessControlService.Module.SalesPOS))
                {
                    var zReading = new Views.Dialogs.ZReadingWindow(_currentUser.UserID);
                    zReading.Owner = this;

                    // Abort logout if Z-Reading is cancelled
                    if (zReading.ShowDialog() != true)
                    {
                        return;
                    }
                }

                // Cleanup session tracking
                _idleTimer.Stop();
                InputManager.Current.PreProcessInput -= OnUserActivity;

                new Views.LoginView().Show();
                this.Close();
            }
        }
    }
}