using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class WasteTrackerView : UserControl
    {
        private User _user;

        public WasteTrackerView(User user)
        {
            InitializeComponent();
            _user = user;
            LoadData();
        }

        private void LoadData()
        {
            // 1. Load Items
            InventoryRepository invRepo = new InventoryRepository();
            if (_user.BranchID != null)
            {
                cmbItems.ItemsSource = invRepo.GetStockForBranch(_user.BranchID.Value);
            }

            // 2. Load Recent Logs
            LoadRecentLogs();
        }

        private void LoadRecentLogs()
        {
            if (_user.BranchID != null)
            {
                WasteRepository wasteRepo = new WasteRepository();
                icWasteLog.ItemsSource = wasteRepo.GetRecentWaste(_user.BranchID.Value);
            }
        }

        private async void LogWaste_Click(object sender, RoutedEventArgs e)
        {
            if (cmbItems.SelectedItem == null || string.IsNullOrWhiteSpace(txtQty.Text))
            {
                MessageBox.Show("Please select an item and enter quantity.", "Missing Info");
                return;
            }

            try
            {
                // 1. Play "Compactor" Animation
                await PlayCompactorSequence();

                // 2. Save Data
                WasteRepository repo = new WasteRepository();
                var waste = new WasteRecord
                {
                    BranchID = _user.BranchID.Value,
                    ItemID = (int)cmbItems.SelectedValue,
                    QtyWasted = decimal.Parse(txtQty.Text),
                    Reason = txtReason.Text,
                    RecordedBy = _user.UserID
                };

                repo.RecordWaste(waste);

                // 3. Reset UI
                txtQty.Clear();
                txtReason.Clear();
                cmbItems.SelectedIndex = -1;
                LoadRecentLogs(); // Refresh the log list
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private async Task PlayCompactorSequence()
        {
            Overlay_Compactor.Visibility = Visibility.Visible;

            // 1. Plate Slams Down
            DoubleAnimation dropAnim = new DoubleAnimation
            {
                From = -800,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            // 2. Shake Screen (Visual Vibration)
            DoubleAnimation shakeAnim = new DoubleAnimation(0, 5, TimeSpan.FromMilliseconds(50)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(5) };

            CompactorPlate.BeginAnimation(Canvas.TopProperty, dropAnim); // We use Margin/Translate in Grid, but let's use Margin here

            // Actually, since it's in a Grid, let's animate the Margin.Top
            ThicknessAnimation slamDown = new ThicknessAnimation(new Thickness(0, -800, 0, 0), new Thickness(0, 0, 0, 0), TimeSpan.FromMilliseconds(300));
            slamDown.EasingFunction = new BounceEase { Bounces = 1, Bounciness = 0.5 };

            CompactorPlate.BeginAnimation(MarginProperty, slamDown);

            // Wait for impact
            await Task.Delay(300);

            // Shake the Main Card
            TranslateTransform trans = new TranslateTransform();
            MainCard.RenderTransform = trans;
            trans.BeginAnimation(TranslateTransform.XProperty, shakeAnim);

            await Task.Delay(1000); // "Compacting..."

            // 3. Plate Retracts
            ThicknessAnimation liftUp = new ThicknessAnimation(new Thickness(0, 0, 0, 0), new Thickness(0, -800, 0, 0), TimeSpan.FromMilliseconds(500));
            CompactorPlate.BeginAnimation(MarginProperty, liftUp);

            await Task.Delay(500);
            Overlay_Compactor.Visibility = Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Navigate Back
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.Nav_Dashboard_Click(null, null);
            }
        }
    }
}