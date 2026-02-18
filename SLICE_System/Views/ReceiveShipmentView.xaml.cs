using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Required for ObservableCollection
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SLICE_System.Data;
using SLICE_System.Models;

namespace SLICE_System.Views
{
    public partial class ReceiveShipmentView : UserControl
    {
        private User _currentUser;

        // View Model for the visual boxes
        public class PizzaBoxVM
        {
            public double RotationAngle { get; set; }
            public Thickness MarginOffset { get; set; }
        }

        public ReceiveShipmentView(User user)
        {
            InitializeComponent();
            _currentUser = user;
            this.Loaded += (s, e) => PlayEntranceAnimation();
            LoadShipments();
        }

        private void LoadShipments()
        {
            LogisticsRepository repo = new LogisticsRepository();
            if (_currentUser.BranchID != null)
            {
                var list = repo.GetIncomingShipments(_currentUser.BranchID.Value);
                icShipments.ItemsSource = list;

                pnlEmptyState.Visibility = (list == null || list.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

                // --- CALCULATE REAL VOLUME ---
                double totalVolume = 0;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        // Now using the real TotalQuantity from the DB query
                        totalVolume += (double)item.TotalQuantity;
                    }
                }
                GeneratePizzaStack(totalVolume);
            }
        }

        // --- DYNAMIC STACK GENERATOR ---
        private void GeneratePizzaStack(double totalUnits)
        {
            var boxes = new ObservableCollection<PizzaBoxVM>();
            int boxCount = 0;

            // LOGIC: Target ~40 boxes for 50,000 units.
            // 50,000 / 1250 = 40 boxes.
            if (totalUnits > 0 && totalUnits < 1000)
            {
                boxCount = 2; // Minimum representation
            }
            else
            {
                boxCount = (int)(totalUnits / 1250);
                if (boxCount < 3 && totalUnits > 0) boxCount = 3;
            }

            // SAFETY CAP: 45 boxes max
            if (boxCount > 45) boxCount = 45;

            // Generate "Organic" Boxes
            Random rnd = new Random();
            for (int i = 0; i < boxCount; i++)
            {
                boxes.Add(new PizzaBoxVM
                {
                    // Rotation: Random between -2 and 2 degrees
                    RotationAngle = rnd.Next(-2, 3),

                    // Offset: Horizontal (-5 to 5) and Vertical Overlap (-10)
                    // The -10 overlap allows 40 boxes to fit in the vertical space
                    MarginOffset = new Thickness(rnd.Next(-5, 5), 0, 0, -10)
                });
            }

            icPizzaStack.ItemsSource = boxes;
        }

        private async void Receive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MeshLogistics shipment)
            {
                try
                {
                    LogisticsRepository repo = new LogisticsRepository();
                    repo.ReceiveShipment(shipment.TransferID);
                    await PlayStampAnimation();
                    LoadShipments(); // Refresh list & Re-calculate stack size
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private void PlayEntranceAnimation()
        {
            DoubleAnimation topAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.8)) { EasingFunction = new CubicEase() };
            ShutterTop.BeginAnimation(HeightProperty, topAnim);

            DoubleAnimation botAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.8)) { EasingFunction = new CubicEase() };
            ShutterBottom.BeginAnimation(HeightProperty, botAnim);
        }

        private async Task PlayStampAnimation()
        {
            Overlay_Stamp.Visibility = Visibility.Visible;
            DoubleAnimation scaleAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new BounceEase { Bounces = 1, Bounciness = 2 }
            };

            StampScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            StampScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            await Task.Delay(1500);

            Overlay_Stamp.Visibility = Visibility.Collapsed;
            StampScale.ScaleX = 3; StampScale.ScaleY = 3;
        }
    }
}