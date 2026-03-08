using System.Windows;
using System.Windows.Input;

namespace SLICE_System.Views.Dialogs
{
    public partial class UserManualWindow : Window
    {
        public UserManualWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Allows dragging the borderless window
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}