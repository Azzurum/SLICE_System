using System;
using System.Collections.Generic;
using System.Linq; // Added for Sum()
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class ManageRequestsView : UserControl
    {
        private User _currentUser;
        private LogisticsRepository _repo = new LogisticsRepository();
        private MeshLogistics _selectedRequest;

        public ManageRequestsView(User user)
        {
            InitializeComponent();
            _currentUser = user;

            // Initial Animation: Fade in the details panel to 0
            DetailsPanel.Opacity = 0;

            LoadRequests();
        }

        private void LoadRequests()
        {
            try
            {
                int myBranchId = _currentUser.BranchID.GetValueOrDefault();
                var requests = _repo.GetPendingRequests(myBranchId);

                // EMPTY STATE LOGIC
                if (requests == null || requests.Count == 0)
                {
                    dgRequests.Visibility = Visibility.Collapsed;
                    EmptyStateOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    dgRequests.Visibility = Visibility.Visible;
                    EmptyStateOverlay.Visibility = Visibility.Collapsed;
                    dgRequests.ItemsSource = requests;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading requests: " + ex.Message);
            }
        }

        private void dgRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgRequests.SelectedItem is MeshLogistics req)
            {
                _selectedRequest = req;
                txtSelectedBranch.Text = req.ToBranchName;

                var details = _repo.GetTransferDetails(req.TransferID);
                lvDetails.ItemsSource = details;

                // Update Summary Total
                txtTotalItems.Text = details.Sum(x => x.Quantity).ToString("N0");

                // Bind Context for XAML Triggers
                DetailsPanel.DataContext = req;

                // Slide & Fade In Animation for Details Panel
                DetailsPanel.Opacity = 1;
                DoubleAnimation slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));

                PanelTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);
                DetailsPanel.BeginAnimation(OpacityProperty, fadeIn);
            }
        }

        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRequest == null) return;

            if (MessageBox.Show($"Approve shipment to {_selectedRequest.ToBranchName}?\nStock will be deducted immediately.",
                "Confirm Shipment", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                // 1. UPDATE DB
                _repo.ApproveRequest(_selectedRequest.TransferID, _currentUser.UserID);

                // 2. PLAY ANIMATION
                await PlayStampAnimation(true);

                // 3. REFRESH
                LoadRequests();
                ResetDetailsPanel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Operation Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRequest == null) return;
            if (MessageBox.Show("Reject this request?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                await PlayStampAnimation(false);
                LoadRequests();
                ResetDetailsPanel();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void ResetDetailsPanel()
        {
            dgRequests.SelectedItem = null;
            DetailsPanel.Opacity = 0;
            txtSelectedBranch.Text = "Select a Request";
            _selectedRequest = null;
        }

        private async Task PlayStampAnimation(bool isApproved)
        {
            StampOverlay.Visibility = Visibility.Visible;

            if (isApproved)
            {
                StampBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71"));
                StampIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.CheckCircle;
                StampIcon.Foreground = StampBorder.BorderBrush;
                StampText.Text = "SHIPPED";
                StampText.Foreground = StampBorder.BorderBrush;
            }
            else
            {
                StampBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                StampIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.TimesCircle;
                StampIcon.Foreground = StampBorder.BorderBrush;
                StampText.Text = "REJECTED";
                StampText.Foreground = StampBorder.BorderBrush;
            }

            // Slam Down
            DoubleAnimation scaleAnim = new DoubleAnimation(2.0, 1.0, TimeSpan.FromMilliseconds(200))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 } };
            DoubleAnimation fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100));

            StampScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            StampScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            StampOverlay.BeginAnimation(OpacityProperty, fadeAnim);

            await Task.Delay(800); // Wait for user to see it

            // Fade Out
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            StampOverlay.BeginAnimation(OpacityProperty, fadeOut);

            await Task.Delay(300);
            StampOverlay.Visibility = Visibility.Collapsed;
        }
    }
}