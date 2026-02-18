using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SLICE_System.Views
{
    public partial class CookingAnimationView : UserControl
    {
        public CookingAnimationView()
        {
            InitializeComponent();
            this.IsVisibleChanged += CookingAnimationView_IsVisibleChanged;
        }

        private void CookingAnimationView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == true)
            {
                Storyboard sb = this.Resources["CookingStory"] as Storyboard;
                if (sb != null)
                {
                    sb.Stop();  // 1. Reset everything to start state
                    sb.Begin(); // 2. Start fresh
                }
            }
        }
    }
}