using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace SLICE_System.Views
{
    public partial class SalesView : UserControl
    {
        public SalesView()
        {
            InitializeComponent();
        }

        // PHYSICAL KEYBOARD BLOCKER
        // This stops the user from typing decimals, letters, or negative signs into the quantity box
        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Regex to match anything that is NOT a number from 0-9
            Regex regex = new Regex("[^0-9]+");

            // If the text being typed matches the regex (meaning it's a letter/symbol), 
            // e.Handled = true tells the application to ignore the keystroke entirely.
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}