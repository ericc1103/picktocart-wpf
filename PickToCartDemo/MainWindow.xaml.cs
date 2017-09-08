using FirstFloor.ModernUI.Windows.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PickToCartDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : ModernWindow, INotifyPropertyChanged
    {
        private DispatcherTimer clockTimer;
        private DispatcherTimer barcodeScannerTimer;
        private string currentTime;
        private Brush sectionABackgroundColor;
        private Brush sectionABorderColor;
        private Brush sectionBBackgroundColor;
        private Brush sectionBBorderColor;
        private Brush sectionCBorderColor;
        private Brush sectionDBackgroundColor;
        private Brush sectionDBorderColor;
        private string tempBarcode;
        private CurrentState currentState;
        private bool isClosing;
        private bool isCompleted;

        private readonly FlashWindowHelper flashWindowHelper = new FlashWindowHelper(Application.Current);
        private readonly Storyboard storyboard = new Storyboard();

        // Define colors.
        private readonly Brush BlueBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 65, 105, 226));
        private readonly Brush BlueBorderBrush = new SolidColorBrush(Color.FromArgb(255, 136, 205, 249));
        private readonly Brush RedBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 254, 0, 0));
        private readonly Brush RedBorderBrush = new SolidColorBrush(Color.FromArgb(255, 253, 122, 109));
        private readonly Brush GrayBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
        private readonly Brush GrayBorderBrush = new SolidColorBrush(Color.FromArgb(255, 105, 105, 105));
        private readonly Brush GreenBorderBrush = new SolidColorBrush(Color.FromArgb(255, 144, 238, 144));

        private const string BibleTeachCode = "5QCPCM0QW";
        private const string BoxLabelCode = "123456";
        private const char RETURN = '\r';

        public enum MapType : uint
        {
            MAPVK_VK_TO_VSC = 0x0,
            MAPVK_VSC_TO_VK = 0x1,
            MAPVK_VK_TO_CHAR = 0x2,
            MAPVK_VSC_TO_VK_EX = 0x3,
        }

        [DllImport( "user32.dll" )]
        public static extern int ToUnicode(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs( UnmanagedType.LPWStr, SizeParamIndex = 4 )]
        StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags);

        [DllImport("user32.dll")]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, MapType uMapType);

        public MainWindow()
        {
            this.InitializeComponent();

            this.ConfigureStoryboard();
            this.currentState = CurrentState.New;
            this.DataContext = this;

            this.SetupBarcodeScannerTimer();
            this.SetupClockTimer();
            this.SetCurrentState(this.currentState);
            InitializeComponent();
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            this.isClosing = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                this.SetCurrentState(CurrentState.New);
                return;
            }

            this.barcodeScannerTimer.Start();

            var keyChar = this.GetCharFromKey(e.Key);
            if (keyChar == RETURN)
            {
                this.Reset();

                if (this.currentState == CurrentState.New || this.currentState == CurrentState.Invalid)
                {
                    if (this.Barcode == BibleTeachCode)
                    {
                        this.SetCurrentState(CurrentState.Success);
                    }
                    else
                    {
                        this.SetCurrentState(CurrentState.Invalid);
                    }

                }
                else if (this.currentState == CurrentState.Success || this.currentState == CurrentState.SuccessInvalid)
                {
                    if (this.Barcode == BoxLabelCode)
                    {
                        this.SetCurrentState(CurrentState.Completed);
                    }
                    else
                    {
                        this.SetCurrentState(CurrentState.SuccessInvalid);
                    }
                }
            }
            else
            {
                this.tempBarcode += keyChar.ToString();
            }
        }

        private void ConfigureStoryboard()
        {
            var fade = new DoubleAnimation()
            {
                From = 0,
                To = 1,
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true,
                Duration = TimeSpan.FromMilliseconds(800),
            };

            Storyboard.SetTarget(fade, this.mainWindow);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Button.OpacityProperty));

            this.storyboard.Children.Add(fade);
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            if (this.isClosing || this.isCompleted)
            {
                return;
            }

            this.storyboard.Begin();
            this.flashWindowHelper.FlashApplicationWindow();
            this.SpeakAsync("Please set focus on pick the cart demo application.").ConfigureAwait(false);
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            this.storyboard.Remove();
            this.flashWindowHelper.StopFlashing();
        }

        private async Task SpeakAsync(string sentence)
        {
            await Task.Run(() => {
                var speaker = new SpeechSynthesizer();
                speaker.Rate = 1;
                speaker.Volume = 100;
                speaker.Speak(sentence);
            });
        }

        private char GetCharFromKey(Key key)
        {
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            var keyboardState = new byte[256];
            GetKeyboardState(keyboardState);

            var scanCode = MapVirtualKey((uint)virtualKey, MapType.MAPVK_VK_TO_VSC);
            var stringBuilder = new StringBuilder(2);
            int result = ToUnicode((uint)virtualKey, scanCode, keyboardState, stringBuilder, stringBuilder.Capacity, 0);

            char keyChar = '\0';
            switch (result)
            {
                case -1:
                    break;
                case 0:
                    break;
                case 1:
                {
                    keyChar = stringBuilder[0];
                    break;
                }
                default:
                {
                    keyChar = stringBuilder[0];
                    break;
                }
            }

            return keyChar;
        }       

        private void Reset(bool isStartOver = false)
        {
            this.barcodeScannerTimer.Stop();
            this.Barcode = isStartOver ? string.Empty : this.GetCodeFromRawString(this.tempBarcode);
            Debug.WriteLine(this.Barcode);
            this.tempBarcode = string.Empty;
        }

        public string Barcode { get; set; }

        public string CurrentTime
        {
            get
            {
                return this.currentTime;
            }
            set
            {
                this.currentTime = value;
                this.NotifyPropertyChanged("CurrentTime");
            }
        }

        public Brush SectionABackgroundColor
        {
            get
            {
                return this.sectionABackgroundColor;
            }
            set
            {
                this.sectionABackgroundColor = value;
                this.NotifyPropertyChanged("SectionABackgroundColor");
            }
        }

        public Brush SectionABorderColor
        {
            get
            {
                return this.sectionABorderColor;
            }
            set
            {
                this.sectionABorderColor = value;
                this.NotifyPropertyChanged("SectionABorderColor");
            }
        }

        public Brush SectionBBackgroundColor
        {
            get
            {
                return this.sectionBBackgroundColor;
            }
            set
            {
                this.sectionBBackgroundColor = value;
                this.NotifyPropertyChanged("SectionBBackgroundColor");
            }
        }

        public Brush SectionBBorderColor
        {
            get
            {
                return this.sectionBBorderColor;
            }
            set
            {
                this.sectionBBorderColor = value;
                this.NotifyPropertyChanged("SectionBBorderColor");
            }
        }

        public Brush SectionCBorderColor
        {
            get
            {
                return this.sectionCBorderColor;
            }
            set
            {
                this.sectionCBorderColor = value;
                this.NotifyPropertyChanged("SectionCBorderColor");
            }
        }

        public Brush SectionDBackgroundColor
        {
            get
            {
                return this.sectionDBackgroundColor;
            }
            set
            {
                this.sectionDBackgroundColor = value;
                this.NotifyPropertyChanged("SectionDBackgroundColor");
            }
        }

        public Brush SectionDBorderColor
        {
            get
            {
                return this.sectionDBorderColor;
            }
            set
            {
                this.sectionDBorderColor = value;
                this.NotifyPropertyChanged("SectionDBorderColor");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void ClockTimer_Tick(object sender, object e)
        {
            this.CurrentTime = DateTime.Now.ToString("h:mm tt");
        }

        private void BarcodeScannerTimer_Tick(object sender, object e)
        {
            this.tempBarcode = string.Empty;
        }

        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void SetCurrentState(CurrentState currentState)
        {
            this.currentState = currentState;
            string audioName = "chimes-glassy.mp3";
            switch (currentState)
            {
                case CurrentState.New:
                    this.isCompleted = false;
                    audioName = "arpeggio.mp3";
                    this.NewState();
                    break;
                case CurrentState.Success:
                    this.SuccessState();
                    audioName = "coins.mp3";
                    break;
                case CurrentState.SuccessInvalid:
                    this.SucessfulInvalidState();
                    audioName = "surprise-on-a-spring.mp3";
                    break;
                case CurrentState.Invalid:
                    this.InvalidState();
                    audioName = "surprise-on-a-spring.mp3";
                    break;
                case CurrentState.Completed:
                    this.SuccessState();
                    this.isCompleted = true;                    
                    audioName = "what-friends-are-for.mp3";
                    break;
            }

            MediaPlayer player = new MediaPlayer();
            player.Open(new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, "Sounds", audioName)));
            player.Play();

            if (this.isCompleted)
            {
                this.DisplayCompletedMessage();                
            }
        }


        private void SetupClockTimer()
        {
            this.clockTimer = new DispatcherTimer();
            this.clockTimer.Interval = new TimeSpan(0, 0, 1);
            this.clockTimer.Tick += ClockTimer_Tick;
            this.clockTimer.Start();
        }

        private void SetupBarcodeScannerTimer()
        {
            this.barcodeScannerTimer = new DispatcherTimer();
            this.barcodeScannerTimer.Interval = new TimeSpan(0, 0, 0, 500);
            this.barcodeScannerTimer.Tick += BarcodeScannerTimer_Tick;
        }

        private void NewState()
        {
            this.SectionABackgroundColor = this.BlueBackgroundBrush;
            this.SectionABorderColor = this.BlueBorderBrush;
            this.SectionBBackgroundColor = this.BlueBackgroundBrush;
            this.SectionBBorderColor = this.BlueBorderBrush;
            this.SectionCBorderColor = this.BlueBorderBrush;
            this.SectionDBackgroundColor = this.GrayBackgroundBrush;
            this.SectionDBorderColor = this.GrayBorderBrush;
        }

        private void SuccessState()
        {
            this.SectionABackgroundColor = this.BlueBackgroundBrush;
            this.SectionABorderColor = this.BlueBorderBrush;
            this.SectionBBackgroundColor = this.BlueBackgroundBrush;
            this.SectionBBorderColor = this.BlueBorderBrush;
            this.SectionCBorderColor = this.GreenBorderBrush;
            this.SectionDBackgroundColor = this.BlueBackgroundBrush;
            this.SectionDBorderColor = this.BlueBorderBrush;
        }

        private void InvalidState()
        {
            this.SectionABackgroundColor = this.RedBackgroundBrush;
            this.SectionABorderColor = this.RedBorderBrush;
            this.SectionBBackgroundColor = this.RedBackgroundBrush;
            this.SectionBBorderColor = this.RedBorderBrush;
            this.SectionCBorderColor = this.RedBorderBrush;
            this.SectionDBackgroundColor = this.GrayBackgroundBrush;
            this.SectionDBorderColor = this.GrayBorderBrush;
        }

        private void SucessfulInvalidState()
        {
            this.SectionABackgroundColor = this.BlueBackgroundBrush;
            this.SectionABorderColor = this.BlueBorderBrush;
            this.SectionBBackgroundColor = this.RedBackgroundBrush;
            this.SectionBBorderColor = this.RedBorderBrush;
            this.SectionCBorderColor = this.GreenBorderBrush;
            this.SectionDBackgroundColor = this.RedBackgroundBrush;
            this.SectionDBorderColor = this.RedBorderBrush;
        }

        private string GetCodeFromRawString(string rawString)
        {
            rawString = rawString.Replace("\0;", ":");
            rawString = rawString.Replace("\03", "#");
            rawString = rawString.Replace("\0", "");

            if (string.IsNullOrWhiteSpace(rawString))
            {
                return null;
            }

            var parsedString = rawString.Split('#');
            if (parsedString.Length == 2)
            {
                return parsedString[1];
            }

            return parsedString[0];
        }

        private void DisplayCompletedMessage()
        {
            MessageBox.Show("Picking Simulation is Completed.", "Pick The Cart Demo", MessageBoxButton.OK, MessageBoxImage.Information);
            this.SetCurrentState(CurrentState.New);
        }

        public enum CurrentState
        {
            New,
            Invalid,
            Success,
            SuccessInvalid,
            Completed
        }
    }
}

