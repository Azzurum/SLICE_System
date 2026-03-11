using System.Windows;
using SLICE_System.ViewModels;
using SLICE_System.Models;

namespace SLICE_System.Views.Dialogs
{
    public partial class PaymentGatewayWindow : Window
    {
        public PaymentResult PaymentResult { get; private set; }

        public PaymentGatewayWindow(decimal totalAmount)
        {
            InitializeComponent();
            var vm = new PaymentGatewayViewModel(totalAmount);
            
            // Link the VM's close action to physically close this window
            vm.CloseAction = (result) =>
            {
                PaymentResult = result;
                this.DialogResult = result.IsSuccess; 
            };

            DataContext = vm;
        }
    }
}