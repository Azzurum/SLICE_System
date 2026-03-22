using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QRCoder;
using SLICE_System.Models;

namespace SLICE_System.ViewModels
{
    public class PaymentGatewayViewModel : ViewModelBase
    {
        public decimal TotalAmount { get; }
        public Action<PaymentResult> CloseAction { get; set; }

        // ==========================================
        // UI STATES
        // ==========================================
        private Visibility _selectionVis = Visibility.Visible;
        private Visibility _inputVis = Visibility.Collapsed;
        private Visibility _processingVis = Visibility.Collapsed;
        private Visibility _resultVis = Visibility.Collapsed;

        public Visibility SelectionVis { get => _selectionVis; set { _selectionVis = value; OnPropertyChanged(); } }
        public Visibility InputVis { get => _inputVis; set { _inputVis = value; OnPropertyChanged(); } }
        public Visibility ProcessingVis { get => _processingVis; set { _processingVis = value; OnPropertyChanged(); } }
        public Visibility ResultVis { get => _resultVis; set { _resultVis = value; OnPropertyChanged(); } }

        // ==========================================
        // PAYMENT METHOD SELECTION
        // ==========================================
        private string _selectedMethod;
        public string SelectedMethod
        {
            get => _selectedMethod;
            set
            {
                _selectedMethod = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCardPayment));
                OnPropertyChanged(nameof(IsCashPayment));
                OnPropertyChanged(nameof(IsDigitalPayment));
                OnPropertyChanged(nameof(IsEwalletPayment));
            }
        }

        public bool IsCardPayment => SelectedMethod == "Credit / Debit Card";
        public bool IsCashPayment => SelectedMethod == "Cash";
        public bool IsDigitalPayment => SelectedMethod != "Cash";
        public bool IsEwalletPayment => SelectedMethod == "GCash" || SelectedMethod == "Maya";

        // ==========================================
        // INPUT DATA (E-Wallets)
        // ==========================================
        private string _accountNumber;
        public string AccountNumber { get => _accountNumber; set { _accountNumber = value; OnPropertyChanged(); } }

        private string _cardExpiry;
        public string CardExpiry { get => _cardExpiry; set { _cardExpiry = value; OnPropertyChanged(); } }

        private string _cardCVV;
        public string CardCVV { get => _cardCVV; set { _cardCVV = value; OnPropertyChanged(); } }

        // ==========================================
        // CASH HANDLING
        // ==========================================
        private string _amountTenderedStr;
        public string AmountTenderedStr
        {
            get => _amountTenderedStr;
            set
            {
                _amountTenderedStr = value;
                OnPropertyChanged();
                CalculateChange();
            }
        }

        private decimal _changeAmount;
        public decimal ChangeAmount { get => _changeAmount; set { _changeAmount = value; OnPropertyChanged(); } }

        private void CalculateChange()
        {
            if (decimal.TryParse(AmountTenderedStr, out decimal tendered))
                ChangeAmount = tendered >= TotalAmount ? tendered - TotalAmount : 0;
            else
                ChangeAmount = 0;
        }

        // ==========================================
        // QR CODE GENERATOR
        // ==========================================
        private BitmapImage _qrCodeImage;
        public BitmapImage QrCodeImage { get => _qrCodeImage; set { _qrCodeImage = value; OnPropertyChanged(); } }

        private void GenerateQRCode()
        {
            // Clean up the method string for the URL
            string methodUrlFriendly = SelectedMethod.Replace(" ", "").Replace("/", "").ToLower();

            string baseUrl = "https://azzurum.github.io/slice-pay/";

            // Construct the live URL with the dynamic parameters
            string qrText = $"{baseUrl}?method={methodUrlFriendly}&amount={TotalAmount:F2}&ref={ReferenceNumber}";

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrCodeBytes = qrCode.GetGraphic(20);
                using (MemoryStream ms = new MemoryStream(qrCodeBytes))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Required to prevent cross-thread crashing in WPF
                    QrCodeImage = bitmap;
                }
            }
        }

        // ==========================================
        // PROCESSING & RESULT STATE
        // ==========================================
        private string _processingMessage;
        public string ProcessingMessage { get => _processingMessage; set { _processingMessage = value; OnPropertyChanged(); } }

        private bool _isSuccessResult;
        public bool IsSuccessResult
        {
            get => _isSuccessResult;
            set
            {
                _isSuccessResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFailedResult));
            }
        }

        public bool IsFailedResult => !IsSuccessResult;

        private string _resultMessage;
        public string ResultMessage { get => _resultMessage; set { _resultMessage = value; OnPropertyChanged(); } }

        private string _referenceNumber;
        public string ReferenceNumber { get => _referenceNumber; set { _referenceNumber = value; OnPropertyChanged(); } }

        // ==========================================
        // COMMANDS & LOGIC
        // ==========================================
        public ICommand SelectMethodCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ProcessCommand { get; }
        public ICommand CloseCommand { get; }

        public PaymentGatewayViewModel(decimal amount)
        {
            TotalAmount = amount;
            SelectMethodCommand = new RelayCommand<object>(ExecuteSelectMethod);
            BackCommand = new RelayCommand<object>(ExecuteBack);
            ProcessCommand = new RelayCommand<object>(ExecuteProcess);
            CloseCommand = new RelayCommand<object>(ExecuteClose);
        }

        private void ExecuteSelectMethod(object parameter)
        {
            SelectedMethod = parameter.ToString();
            SelectionVis = Visibility.Collapsed;
            InputVis = Visibility.Visible;
            AccountNumber = ""; CardExpiry = ""; CardCVV = ""; AmountTenderedStr = "";

            // 1. GENERATE THE REFERENCE NUMBER UPFRONT!
            // This ensures the QR code, the POS screen, and the Database all use the exact same ID.
            ReferenceNumber = $"TXN-{DateTime.Now.Ticks.ToString().Substring(8)}-{new Random().Next(10, 99)}";

            // 2. Generate the QR code immediately when an e-wallet is selected
            if (IsEwalletPayment)
            {
                GenerateQRCode();
            }
        }

        private void ExecuteBack(object parameter)
        {
            InputVis = Visibility.Collapsed;
            ResultVis = Visibility.Collapsed;
            SelectionVis = Visibility.Visible;
        }

        private async void ExecuteProcess(object parameter)
        {
            // ------------------------------------------
            // 1. CASH PAYMENT FLOW 
            // ------------------------------------------
            if (IsCashPayment)
            {
                if (!decimal.TryParse(AmountTenderedStr, out decimal tendered) || tendered < TotalAmount)
                {
                    MessageBox.Show("Amount tendered must be equal to or greater than the total amount to pay.", "Invalid Cash Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                InputVis = Visibility.Collapsed;
                ResultVis = Visibility.Visible;
                IsSuccessResult = true;

                // For cash, we overwrite the TXN prefix with a CASH prefix
                ReferenceNumber = $"CASH-{DateTime.Now.Ticks.ToString().Substring(8)}";
                ResultMessage = $"Payment Complete.\nChange: ₱{ChangeAmount:N2}";

                await Task.Delay(3500);
                ExecuteClose(null);
                return;
            }

            // ------------------------------------------
            // 2. DIGITAL & CARD PAYMENT FLOW
            // ------------------------------------------
            InputVis = Visibility.Collapsed;
            ProcessingVis = Visibility.Visible;

            ProcessingMessage = IsCardPayment ? "Awaiting Terminal Response..." : $"Awaiting Customer App Confirmation...";
            await Task.Delay(1500);

            ProcessingMessage = "Authorizing Transaction...";
            await Task.Delay(1500);

            // 15% chance of realistic random failure
            bool simulateFailure = new Random().Next(1, 100) <= 15;

            ProcessingVis = Visibility.Collapsed;
            ResultVis = Visibility.Visible;

            if (!simulateFailure)
            {
                IsSuccessResult = true;

                // It keeps the one that was already generated and sent to the QR code!
                ResultMessage = "Payment Approved & Secured";

                await Task.Delay(2500);
                ExecuteClose(null);
            }
            else
            {
                IsSuccessResult = false;
                ResultMessage = IsCardPayment ? "Terminal Declined: Insufficient Funds." : "Customer App Declined or Timeout.";
            }
        }

        private bool _isClosing = false;
        private void ExecuteClose(object parameter)
        {
            // If it's already in the process of closing, do nothing and prevent the crash.
            if (_isClosing) return;
            _isClosing = true;

            var result = new PaymentResult
            {
                IsSuccess = IsSuccessResult,
                PaymentMethod = SelectedMethod,
                ReferenceNumber = ReferenceNumber,
                ErrorMessage = IsSuccessResult ? null : ResultMessage
            };
            CloseAction?.Invoke(result);
        }
    }
}