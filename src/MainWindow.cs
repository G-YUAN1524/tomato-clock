using System;
using System.Globalization;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using Path = System.IO.Path;

namespace TomatoClock
{
    public sealed class MainWindow : Window
    {
        private enum TimerMode
        {
            Focus,
            Break
        }

        private enum TimerState
        {
            Idle,
            Running,
            Paused
        }

        private const int DefaultFocusSeconds = 25 * 60;
        private const int BreakSeconds = 5 * 60;
        private const int MaxDurationSeconds = 180 * 60;

        private readonly AppSettings _settings;
        private DispatcherTimer _ticker;
        private DispatcherTimer _saveTimer;

        private Grid _root;
        private Border _card;
        private Grid _ringHost;
        private ProgressRing _ring;
        private Ellipse _statusDot;
        private TextBlock _statusText;
        private TextBlock _timeText;
        private TextBlock _subText;
        private ToggleButton _pinButton;
        private Button _minButton;
        private Button _closeButton;
        private ToggleButton _preset25;
        private ToggleButton _preset30;
        private ToggleButton _preset45;
        private ToggleButton _presetCustom;
        private Button _primaryButton;
        private TextBlock _primaryIcon;
        private TextBlock _primaryLabel;
        private Button _resetButton;

        private Border _customOverlay;
        private Border _customCard;
        private Button _customClose;
        private TextBox _customMinutesBox;
        private TextBox _customSecondsBox;
        private TextBlock _customError;
        private Button _chip5;
        private Button _chip15;
        private Button _chip20;
        private Button _chip30;
        private Button _customCancel;
        private Button _customApply;

        private Border _finishOverlay;
        private Border _finishCard;
        private Border _finishIconBubble;
        private TextBlock _finishIcon;
        private Button _finishClose;
        private TextBlock _finishEyebrow;
        private TextBlock _finishTitle;
        private TextBlock _finishSubtitle;
        private Button _finishPrimary;
        private Button _finishSecondary;

        private Brush _coralButton;
        private Brush _sageButton;
        private Brush _coralRing;
        private Brush _sageRing;
        private Brush _trackBrush;
        private Brush _tickBrush;
        private Brush _coralSolid;
        private Brush _sageSolid;
        private Brush _inkBrush;
        private Brush _sageInkBrush;
        private DropShadowEffect _coralShadow;
        private DropShadowEffect _sageShadow;

        private TimerMode _mode = TimerMode.Focus;
        private TimerState _state = TimerState.Idle;
        private int _focusSeconds = DefaultFocusSeconds;
        private int _customSeconds = 10 * 60;
        private int _totalSeconds = DefaultFocusSeconds;
        private double _remainingSeconds = DefaultFocusSeconds;
        private DateTime _endAt;
        private int _lastShownSecond = -1;
        private string _selectedPreset = "25";
        private bool _pinned = true;
        private bool _finishIsBreak;
        private bool _updatingPresetSelection;

        private bool _isLoaded;
        private bool _preview;
        private string _previewState = "idle";
        private string _screenshotPath;

        private Forms.NotifyIcon _trayIcon;
        private Drawing.Icon _appIcon;
        private SoundPlayer _player;
        private Stream _chimeStream;

        public MainWindow()
        {
            _settings = AppSettings.Load();
            _focusSeconds = Clamp(_settings.FocusSeconds, 1, MaxDurationSeconds);
            _customSeconds = _settings.CustomSeconds > 0 ? _settings.CustomSeconds : 10 * 60;
            _pinned = _settings.Pinned;

            Title = "番茄时钟";
            Width = 348;
            Height = 500;
            WindowStyle = System.Windows.WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Topmost = _pinned;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Icon = LoadWindowIcon();

            LoadInterface();
            InitializeBrushes();
            InitializeRing();
            InitializeState();
            InitializeTimers();
            WireEvents();

            Loaded += OnLoaded;
            Closing += OnClosing;
            PreviewKeyDown += OnPreviewKeyDown;
            LocationChanged += OnLocationChanged;
        }

        public void EnablePreview(string screenshotPath, string state)
        {
            _preview = true;
            _screenshotPath = screenshotPath;
            _previewState = string.IsNullOrWhiteSpace(state) ? "idle" : state;
            ShowInTaskbar = false;
            Left = -2200;
            Top = 80;
        }

        private void LoadInterface()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("MainWindow.xaml"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("找不到界面资源 MainWindow.xaml。");
                }
                _root = (Grid)XamlReader.Load(stream);
            }
            Content = _root;

            _card = Find<Border>("Card");
            _ringHost = Find<Grid>("RingHost");
            _statusDot = Find<Ellipse>("StatusDot");
            _statusText = Find<TextBlock>("StatusText");
            _timeText = Find<TextBlock>("TimeText");
            _subText = Find<TextBlock>("SubText");
            _pinButton = Find<ToggleButton>("PinButton");
            _minButton = Find<Button>("MinButton");
            _closeButton = Find<Button>("CloseButton");
            _preset25 = Find<ToggleButton>("Preset25");
            _preset30 = Find<ToggleButton>("Preset30");
            _preset45 = Find<ToggleButton>("Preset45");
            _presetCustom = Find<ToggleButton>("PresetCustom");
            _primaryButton = Find<Button>("PrimaryButton");
            _primaryIcon = Find<TextBlock>("PrimaryIcon");
            _primaryLabel = Find<TextBlock>("PrimaryLabel");
            _resetButton = Find<Button>("ResetButton");

            _customOverlay = Find<Border>("CustomOverlay");
            _customCard = Find<Border>("CustomCard");
            _customClose = Find<Button>("CustomClose");
            _customMinutesBox = Find<TextBox>("CustomMinutesBox");
            _customSecondsBox = Find<TextBox>("CustomSecondsBox");
            _customError = Find<TextBlock>("CustomError");
            _chip5 = Find<Button>("Chip5");
            _chip15 = Find<Button>("Chip15");
            _chip20 = Find<Button>("Chip20");
            _chip30 = Find<Button>("Chip30");
            _customCancel = Find<Button>("CustomCancel");
            _customApply = Find<Button>("CustomApply");

            _finishOverlay = Find<Border>("FinishOverlay");
            _finishCard = Find<Border>("FinishCard");
            _finishIconBubble = Find<Border>("FinishIconBubble");
            _finishIcon = Find<TextBlock>("FinishIcon");
            _finishClose = Find<Button>("FinishClose");
            _finishEyebrow = Find<TextBlock>("FinishEyebrow");
            _finishTitle = Find<TextBlock>("FinishTitle");
            _finishSubtitle = Find<TextBlock>("FinishSubtitle");
            _finishPrimary = Find<Button>("FinishPrimary");
            _finishSecondary = Find<Button>("FinishSecondary");
        }

        private T Find<T>(string name) where T : class
        {
            object value = _root.FindName(name);
            T typed = value as T;
            if (typed == null)
            {
                throw new InvalidOperationException("界面元素缺失: " + name);
            }
            return typed;
        }

        private void InitializeBrushes()
        {
            _coralButton = (Brush)_root.FindResource("CoralGradient");
            _sageButton = (Brush)_root.FindResource("SageGradient");
            _inkBrush = (Brush)_root.FindResource("Ink");

            _coralRing = CreateGradient(Color.FromRgb(226, 100, 76), Color.FromRgb(246, 170, 128));
            _sageRing = CreateGradient(Color.FromRgb(111, 147, 120), Color.FromRgb(170, 198, 157));
            _coralSolid = FreezeBrush(new SolidColorBrush(Color.FromRgb(226, 100, 76)));
            _sageSolid = FreezeBrush(new SolidColorBrush(Color.FromRgb(111, 147, 120)));
            _sageInkBrush = FreezeBrush(new SolidColorBrush(Color.FromRgb(66, 91, 72)));
            _trackBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(78, 208, 185, 158)));
            _tickBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(50, 130, 105, 82)));

            _coralShadow = new DropShadowEffect();
            _coralShadow.Color = Color.FromRgb(148, 57, 41);
            _coralShadow.BlurRadius = 18;
            _coralShadow.ShadowDepth = 4;
            _coralShadow.Direction = 270;
            _coralShadow.Opacity = 0.28;
            _coralShadow.Freeze();

            _sageShadow = new DropShadowEffect();
            _sageShadow.Color = Color.FromRgb(52, 84, 61);
            _sageShadow.BlurRadius = 18;
            _sageShadow.ShadowDepth = 4;
            _sageShadow.Direction = 270;
            _sageShadow.Opacity = 0.25;
            _sageShadow.Freeze();
        }

        private static LinearGradientBrush CreateGradient(Color start, Color end)
        {
            LinearGradientBrush brush = new LinearGradientBrush(start, end, new Point(0, 0), new Point(1, 1));
            brush.Freeze();
            return brush;
        }

        private static Brush FreezeBrush(Brush brush)
        {
            brush.Freeze();
            return brush;
        }

        private void InitializeRing()
        {
            _ring = new ProgressRing();
            _ring.Width = 220;
            _ring.Height = 220;
            _ring.Thickness = 10;
            _ring.Progress = 1.0;
            _ring.ProgressBrush = _coralRing;
            _ring.TrackBrush = _trackBrush;
            _ring.TickBrush = _tickBrush;
            _ringHost.Children.Add(_ring);
        }

        private void InitializeState()
        {
            _mode = TimerMode.Focus;
            _state = TimerState.Idle;
            _totalSeconds = _focusSeconds;
            _remainingSeconds = _focusSeconds;
            _selectedPreset = MatchPreset(_focusSeconds);

            _pinButton.IsChecked = _pinned;
            _pinButton.ToolTip = _pinned ? "取消固定" : "固定在桌面";
            Topmost = _pinned;

            UpdateUI();
            UpdateClock(true);
        }

        private void InitializeTimers()
        {
            _ticker = new DispatcherTimer(DispatcherPriority.Render);
            _ticker.Interval = TimeSpan.FromMilliseconds(100);
            _ticker.Tick += OnTick;

            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(900);
            _saveTimer.Tick += delegate
            {
                _saveTimer.Stop();
                SaveSettings();
            };
        }

        private void WireEvents()
        {
            _pinButton.Checked += OnPinChanged;
            _pinButton.Unchecked += OnPinChanged;
            _minButton.Click += delegate { WindowState = System.Windows.WindowState.Minimized; };
            _closeButton.Click += delegate { Close(); };

            _preset25.Checked += OnPresetChecked;
            _preset30.Checked += OnPresetChecked;
            _preset45.Checked += OnPresetChecked;
            _presetCustom.Checked += OnPresetChecked;
            _preset25.Unchecked += OnPresetUnchecked;
            _preset30.Unchecked += OnPresetUnchecked;
            _preset45.Unchecked += OnPresetUnchecked;
            _presetCustom.Unchecked += OnPresetUnchecked;

            _primaryButton.Click += OnPrimaryClick;
            _resetButton.Click += delegate { ResetCurrent(); };

            _card.MouseLeftButtonDown += OnCardMouseDown;

            _customOverlay.MouseLeftButtonDown += OnCustomOverlayMouseDown;
            _customClose.Click += delegate { HideCustomOverlay(); };
            _customCancel.Click += delegate { HideCustomOverlay(); };
            _customApply.Click += delegate { ApplyCustomDuration(); };
            _chip5.Click += delegate { SetCustomFields(5, 0); };
            _chip15.Click += delegate { SetCustomFields(15, 0); };
            _chip20.Click += delegate { SetCustomFields(20, 0); };
            _chip30.Click += delegate { SetCustomFields(30, 0); };

            _customMinutesBox.PreviewTextInput += OnDigitsOnly;
            _customSecondsBox.PreviewTextInput += OnDigitsOnly;
            DataObject.AddPastingHandler(_customMinutesBox, OnDurationPaste);
            DataObject.AddPastingHandler(_customSecondsBox, OnDurationPaste);
            _customMinutesBox.KeyDown += OnCustomBoxKeyDown;
            _customSecondsBox.KeyDown += OnCustomBoxKeyDown;

            _finishOverlay.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
            };
            _finishClose.Click += delegate { DismissFinish(); };
            _finishSecondary.Click += delegate { DismissFinish(); };
            _finishPrimary.Click += OnFinishPrimaryClick;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_preview)
            {
                Left = -2200;
                Top = 80;
                ApplyPreviewState();
                DispatcherTimer captureTimer = new DispatcherTimer();
                captureTimer.Interval = TimeSpan.FromMilliseconds(550);
                captureTimer.Tick += delegate
                {
                    captureTimer.Stop();
                    CapturePreview();
                };
                captureTimer.Start();
                return;
            }

            PositionWindow();
            _isLoaded = true;
            LoadChime();
            CreateTrayIcon();
            UpdateUI();
            UpdateClock(true);

            // A gentle first-run cue that the note can be dragged and pinned.
            _pinButton.ToolTip = _pinned ? "已固定在桌面 · 点击取消" : "固定在桌面";
        }

        private void PositionWindow()
        {
            double width = ActualWidth > 0 ? ActualWidth : 348;
            double height = ActualHeight > 0 ? ActualHeight : 500;
            Rect work = SystemParameters.WorkArea;

            double left = _settings.Left;
            double top = _settings.Top;
            bool hasSavedPosition = !double.IsNaN(left) && !double.IsNaN(top) &&
                                    IsPositionVisible(left, top, width, height);

            if (!hasSavedPosition)
            {
                left = work.Right - width - 28;
                top = work.Top + 80;
            }

            Left = left;
            Top = top;
        }

        private static bool IsPositionVisible(double left, double top, double width, double height)
        {
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            double centerX = left + width / 2.0;
            double centerY = top + 28.0;
            return centerX >= virtualLeft && centerX <= virtualRight &&
                   centerY >= virtualTop && centerY <= virtualBottom;
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (!_isLoaded || _preview) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void OnPinChanged(object sender, RoutedEventArgs e)
        {
            _pinned = _pinButton.IsChecked == true;
            Topmost = _pinned;
            _pinButton.ToolTip = _pinned ? "已固定在桌面 · 点击取消" : "固定在桌面";
            SaveSettings();
        }

        private void OnPresetChecked(object sender, RoutedEventArgs e)
        {
            if (_updatingPresetSelection) return;
            ToggleButton button = sender as ToggleButton;
            if (button == null) return;

            if (button == _preset25)
            {
                SetFocusDuration(25 * 60, false);
            }
            else if (button == _preset30)
            {
                SetFocusDuration(30 * 60, false);
            }
            else if (button == _preset45)
            {
                SetFocusDuration(45 * 60, false);
            }
            else if (button == _presetCustom)
            {
                ShowCustomOverlay();
                // Keep the previous preset visually selected until the custom value is applied.
                UpdatePresetSelection();
            }
        }

        private void OnPresetUnchecked(object sender, RoutedEventArgs e)
        {
            if (_updatingPresetSelection) return;
            ToggleButton button = sender as ToggleButton;
            if (button == null) return;

            bool isActive = _mode == TimerMode.Focus &&
                ((button == _preset25 && _selectedPreset == "25") ||
                 (button == _preset30 && _selectedPreset == "30") ||
                 (button == _preset45 && _selectedPreset == "45") ||
                 (button == _presetCustom && _selectedPreset == "custom"));
            if (!isActive) return;

            // A toggle cannot be unselected by clicking its active pill; put it back.
            _updatingPresetSelection = true;
            try
            {
                button.IsChecked = true;
            }
            finally
            {
                _updatingPresetSelection = false;
            }

            if (button == _presetCustom)
            {
                ShowCustomOverlay();
            }
        }

        private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            try
            {
                DragMove();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
                // DragMove can race with a button release; harmless.
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                if (e.Key == Key.Escape)
                {
                    HideCustomOverlay();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    ApplyCustomDuration();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (_customOverlay.Visibility == Visibility.Visible)
                {
                    HideCustomOverlay();
                    e.Handled = true;
                }
                else if (_finishOverlay.Visibility == Visibility.Visible)
                {
                    DismissFinish();
                    e.Handled = true;
                }
                return;
            }

            if (_customOverlay.Visibility == Visibility.Visible || _finishOverlay.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Enter && _finishOverlay.Visibility == Visibility.Visible)
                {
                    OnFinishPrimaryClick(null, null);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Space)
            {
                OnPrimaryClick(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.None)
            {
                ResetCurrent();
                e.Handled = true;
            }
        }

        private void OnPrimaryClick(object sender, RoutedEventArgs e)
        {
            if (_mode == TimerMode.Break)
            {
                ResetFocusToIdle();
                return;
            }

            if (_state == TimerState.Running)
            {
                PauseTimer();
            }
            else
            {
                StartTimer();
            }
        }

        private void StartTimer()
        {
            if (_state == TimerState.Running) return;
            if (_remainingSeconds <= 0.001)
            {
                _remainingSeconds = _totalSeconds;
            }
            if (_remainingSeconds <= 0.001) return;

            _endAt = DateTime.Now.AddSeconds(_remainingSeconds);
            _state = TimerState.Running;
            _ticker.Start();
            UpdateUI();
        }

        private void PauseTimer()
        {
            if (_state != TimerState.Running) return;
            _remainingSeconds = Math.Max(0.0, (_endAt - DateTime.Now).TotalSeconds);
            _ticker.Stop();

            if (_remainingSeconds <= 0.001)
            {
                _state = TimerState.Running;
                CompleteTimer();
                return;
            }

            _state = TimerState.Paused;
            UpdateUI();
            UpdateClock(true);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_state != TimerState.Running) return;

            double remaining = (_endAt - DateTime.Now).TotalSeconds;
            if (remaining <= 0.0)
            {
                _remainingSeconds = 0.0;
                CompleteTimer();
                return;
            }

            _remainingSeconds = remaining;
            UpdateClock(false);
        }

        private void CompleteTimer()
        {
            _ticker.Stop();
            _state = TimerState.Idle;
            _remainingSeconds = 0.0;
            UpdateUI();
            UpdateClock(true);

            PlayChime();
            RestoreForAlert();

            if (_mode == TimerMode.Break)
            {
                ShowBreakFinished();
            }
            else
            {
                ShowFocusFinished();
            }
        }

        private void RestoreForAlert()
        {
            bool wasMinimized = WindowState == System.Windows.WindowState.Minimized;
            if (wasMinimized)
            {
                WindowState = System.Windows.WindowState.Normal;
            }
            Show();
            Activate();

            if (wasMinimized && _trayIcon != null)
            {
                string message = _mode == TimerMode.Break
                    ? "眼睛休息好啦，准备好继续专注了吗？"
                    : "时间到啦，该做眼保健操了。";
                try
                {
                    _trayIcon.ShowBalloonTip(4000, "番茄时钟", message, Forms.ToolTipIcon.Info);
                }
                catch
                {
                    // Balloon tips can be disabled by policy.
                }
            }
        }

        private void SetFocusDuration(int seconds, bool custom)
        {
            seconds = Clamp(seconds, 1, MaxDurationSeconds);
            _ticker.Stop();
            _mode = TimerMode.Focus;
            _state = TimerState.Idle;
            _focusSeconds = seconds;
            _settings.FocusSeconds = seconds;
            _totalSeconds = seconds;
            _remainingSeconds = seconds;
            _lastShownSecond = -1;

            if (custom)
            {
                _customSeconds = seconds;
                _settings.CustomSeconds = seconds;
                _selectedPreset = "custom";
            }
            else
            {
                _selectedPreset = MatchPreset(seconds);
            }

            UpdateUI();
            UpdateClock(true);
            SaveSettings();
        }

        private void ResetCurrent()
        {
            if (_mode == TimerMode.Break)
            {
                ResetFocusToIdle();
                return;
            }

            _ticker.Stop();
            _state = TimerState.Idle;
            _totalSeconds = _focusSeconds;
            _remainingSeconds = _focusSeconds;
            _lastShownSecond = -1;
            UpdateUI();
            UpdateClock(true);
        }

        private void ResetFocusToIdle()
        {
            _ticker.Stop();
            _mode = TimerMode.Focus;
            _state = TimerState.Idle;
            _totalSeconds = _focusSeconds;
            _remainingSeconds = _focusSeconds;
            _lastShownSecond = -1;
            UpdateUI();
            UpdateClock(true);
        }

        private void StartBreak()
        {
            _ticker.Stop();
            _mode = TimerMode.Break;
            _state = TimerState.Idle;
            _totalSeconds = BreakSeconds;
            _remainingSeconds = BreakSeconds;
            _lastShownSecond = -1;
            UpdateUI();
            UpdateClock(true);
            StartTimer();
        }

        private void UpdateUI()
        {
            bool isBreak = _mode == TimerMode.Break;
            _ring.ProgressBrush = isBreak ? _sageRing : _coralRing;
            _ring.TrackBrush = _trackBrush;
            _ring.TickBrush = _tickBrush;
            _statusDot.Fill = isBreak ? _sageSolid : _coralSolid;
            _primaryButton.Background = isBreak ? _sageButton : _coralButton;
            _primaryButton.Effect = isBreak ? _sageShadow : _coralShadow;
            _timeText.Foreground = isBreak ? _sageInkBrush : _inkBrush;

            if (isBreak)
            {
                _statusText.Text = "护眼休息";
                _subText.Text = "放松双眼 · 5 分钟";
                _primaryIcon.Text = "\uE73E";
                _primaryLabel.Text = "结束休息";
            }
            else if (_state == TimerState.Running)
            {
                _statusText.Text = "专注中";
                _subText.Text = FriendlyDuration(_totalSeconds);
                _primaryIcon.Text = "\uE769";
                _primaryLabel.Text = "暂停一下";
            }
            else if (_state == TimerState.Paused)
            {
                _statusText.Text = "已暂停";
                _subText.Text = FriendlyDuration(_totalSeconds);
                _primaryIcon.Text = "\uE768";
                _primaryLabel.Text = "继续专注";
            }
            else
            {
                _statusText.Text = "准备专注";
                _subText.Text = FriendlyDuration(_totalSeconds);
                _primaryIcon.Text = "\uE768";
                _primaryLabel.Text = "开始专注";
            }

            _pinButton.ToolTip = _pinned ? "已固定在桌面 · 点击取消" : "固定在桌面";
            UpdatePresetSelection();
            UpdatePulse();
        }

        private void UpdatePresetSelection()
        {
            bool focus = _mode == TimerMode.Focus;
            _updatingPresetSelection = true;
            try
            {
                _preset25.IsChecked = focus && _selectedPreset == "25";
                _preset30.IsChecked = focus && _selectedPreset == "30";
                _preset45.IsChecked = focus && _selectedPreset == "45";
                _presetCustom.IsChecked = focus && _selectedPreset == "custom";
            }
            finally
            {
                _updatingPresetSelection = false;
            }

            string tooltip = "自定义时长";
            if (_customSeconds > 0)
            {
                tooltip += " · 上次 " + FriendlyDuration(_customSeconds);
            }
            _presetCustom.ToolTip = tooltip;
        }

        private void UpdatePulse()
        {
            if (_state == TimerState.Running)
            {
                DoubleAnimation pulse = new DoubleAnimation(0.28, 1.0, TimeSpan.FromMilliseconds(950));
                pulse.AutoReverse = true;
                pulse.RepeatBehavior = RepeatBehavior.Forever;
                pulse.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
                _statusDot.BeginAnimation(UIElement.OpacityProperty, pulse);
            }
            else
            {
                _statusDot.BeginAnimation(UIElement.OpacityProperty, null);
                _statusDot.Opacity = 1.0;
            }
        }

        private void UpdateClock(bool force)
        {
            int seconds = (int)Math.Ceiling(Math.Max(0.0, _remainingSeconds) - 0.000001);
            if (_state == TimerState.Idle && _remainingSeconds <= 0.0001)
            {
                seconds = 0;
            }

            if (force || seconds != _lastShownSecond)
            {
                string text = FormatTime(seconds);
                _timeText.Text = text;
                _timeText.FontSize = text.Length > 5 ? 44 : 54;
                Title = "番茄时钟 · " + text;
                _lastShownSecond = seconds;
            }

            _ring.Progress = _totalSeconds > 0
                ? Math.Max(0.0, Math.Min(1.0, _remainingSeconds / _totalSeconds))
                : 0.0;
        }

        private void ShowCustomOverlay()
        {
            if (_customOverlay.Visibility == Visibility.Visible) return;

            int seed = _selectedPreset == "custom" && _customSeconds > 0 ? _customSeconds : _focusSeconds;
            SetCustomFields(seed / 60, seed % 60);
            _customError.Text = "";

            ShowOverlay(_customOverlay);
            if (!_preview)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
                {
                    _customMinutesBox.Focus();
                    _customMinutesBox.SelectAll();
                }));
            }
        }

        private void HideCustomOverlay()
        {
            HideOverlay(_customOverlay);
        }

        private void SetCustomFields(int minutes, int seconds)
        {
            _customMinutesBox.Text = Math.Max(0, minutes).ToString(CultureInfo.InvariantCulture);
            _customSecondsBox.Text = Math.Max(0, seconds).ToString("00", CultureInfo.InvariantCulture);
            _customError.Text = "";
        }

        private void ApplyCustomDuration()
        {
            int minutes;
            int seconds;
            if (!TryParseNonNegative(_customMinutesBox.Text, out minutes) || minutes > 180)
            {
                ShowCustomError("分钟需要是 0 – 180 之间的整数。");
                return;
            }
            if (!TryParseNonNegative(_customSecondsBox.Text, out seconds) || seconds > 59)
            {
                ShowCustomError("秒需要是 0 – 59 之间的整数。");
                return;
            }

            int total = minutes * 60 + seconds;
            if (total < 1)
            {
                ShowCustomError("至少设置 1 秒的倒计时。");
                return;
            }
            if (total > MaxDurationSeconds)
            {
                ShowCustomError("最长支持 180 分钟。");
                return;
            }

            HideCustomOverlay();
            SetFocusDuration(total, true);
        }

        private void ShowCustomError(string message)
        {
            _customError.Text = message;
        }

        private static bool TryParseNonNegative(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return true;
            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                   value >= 0;
        }

        private void OnDigitsOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsAsciiDigits(e.Text);
        }

        private static bool IsAsciiDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        private void OnDurationPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                e.CancelCommand();
                return;
            }

            string text = e.SourceDataObject.GetData(DataFormats.UnicodeText, true) as string;
            if (!IsAsciiDigits(text == null ? "" : text.Trim()))
            {
                e.CancelCommand();
            }
        }

        private void OnCustomBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyCustomDuration();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideCustomOverlay();
                e.Handled = true;
            }
        }

        private void OnCustomOverlayMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == _customOverlay)
            {
                HideCustomOverlay();
            }
            e.Handled = true;
        }

        private void ShowFocusFinished()
        {
            _finishIsBreak = false;
            _finishEyebrow.Text = "眼 保 健 操";
            _finishTitle.Text = "该做眼保健操啦";
            _finishSubtitle.Text = "时间到啦。看看远处，眨眨眼，\n给眼睛 5 分钟的放松时间。";
            _finishPrimary.Content = "休息 5 分钟";
            _finishSecondary.Content = "稍后再休息";
            _finishPrimary.Background = _sageButton;
            _finishIcon.Text = "\uE890";
            _finishIcon.Foreground = _sageSolid;
            _finishIconBubble.Background = FreezeBrush(new SolidColorBrush(Color.FromArgb(30, 111, 147, 120)));

            ShowOverlay(_finishOverlay);
        }

        private void ShowBreakFinished()
        {
            _finishIsBreak = true;
            _finishEyebrow.Text = "休 息 完 成";
            _finishTitle.Text = "眼睛休息好啦";
            _finishSubtitle.Text = "状态回来了吗？继续下一轮专注，\n记得保持 20-20-20 的护眼节奏。";
            _finishPrimary.Content = "开始下一轮";
            _finishSecondary.Content = "先不开始";
            _finishPrimary.Background = _coralButton;
            _finishIcon.Text = "\uE73E";
            _finishIcon.Foreground = _coralSolid;
            _finishIconBubble.Background = FreezeBrush(new SolidColorBrush(Color.FromArgb(30, 226, 100, 76)));

            ShowOverlay(_finishOverlay);
        }

        private void OnFinishPrimaryClick(object sender, RoutedEventArgs e)
        {
            HideOverlay(_finishOverlay);

            if (_finishIsBreak)
            {
                ResetFocusToIdle();
                StartTimer();
            }
            else
            {
                StartBreak();
            }
        }

        private void DismissFinish()
        {
            HideOverlay(_finishOverlay);
            ResetFocusToIdle();
        }

        private void ShowOverlay(Border overlay)
        {
            overlay.Visibility = Visibility.Visible;
            overlay.BeginAnimation(UIElement.OpacityProperty, null);

            if (_preview)
            {
                overlay.Opacity = 1.0;
                return;
            }

            overlay.Opacity = 0.0;
            DoubleAnimation fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(170));
            fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            overlay.BeginAnimation(UIElement.OpacityProperty, fade);

            Border card = overlay == _customOverlay ? _customCard : _finishCard;
            TranslateTransform slide = new TranslateTransform(0, 8);
            card.RenderTransform = slide;
            DoubleAnimation rise = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(210));
            rise.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            slide.BeginAnimation(TranslateTransform.YProperty, rise);
        }

        private void HideOverlay(Border overlay)
        {
            if (overlay.Visibility != Visibility.Visible) return;
            overlay.BeginAnimation(UIElement.OpacityProperty, null);

            if (_preview)
            {
                overlay.Visibility = Visibility.Collapsed;
                overlay.Opacity = 0.0;
                return;
            }

            DoubleAnimation fade = new DoubleAnimation(overlay.Opacity, 0.0, TimeSpan.FromMilliseconds(120));
            fade.Completed += delegate
            {
                // If the overlay was reopened during the fade, leave it visible.
                if (overlay.Opacity <= 0.05)
                {
                    overlay.Visibility = Visibility.Collapsed;
                    overlay.Opacity = 0.0;
                }
            };
            overlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void SaveSettings()
        {
            if (_preview) return;

            _settings.FocusSeconds = _focusSeconds;
            _settings.CustomSeconds = _customSeconds;
            _settings.Pinned = _pinned;
            if (WindowState == System.Windows.WindowState.Normal)
            {
                _settings.Left = Left;
                _settings.Top = Top;
            }
            _settings.Save();
        }

        private void LoadChime()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                _chimeStream = assembly.GetManifestResourceStream("chime.wav");
                if (_chimeStream != null)
                {
                    _player = new SoundPlayer(_chimeStream);
                    _player.Load();
                }
            }
            catch
            {
                _player = null;
            }
        }

        private void PlayChime()
        {
            if (_preview || _player == null) return;
            try
            {
                _player.Play();
            }
            catch
            {
                // Audio is optional.
            }
        }

        private ImageSource LoadWindowIcon()
        {
            try
            {
                using (Drawing.Icon icon = Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location))
                {
                    if (icon == null) return null;
                    BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
            }
            catch
            {
                return null;
            }
        }

        private void CreateTrayIcon()
        {
            try
            {
                _appIcon = Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                if (_appIcon == null)
                {
                    _appIcon = Drawing.SystemIcons.Application;
                }

                _trayIcon = new Forms.NotifyIcon();
                _trayIcon.Icon = _appIcon;
                _trayIcon.Text = "番茄时钟";
                _trayIcon.Visible = true;
                _trayIcon.DoubleClick += delegate { RestoreFromTray(); };

                Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
                menu.Items.Add("显示番茄时钟", null, delegate { RestoreFromTray(); });
                menu.Items.Add(new Forms.ToolStripSeparator());
                menu.Items.Add("退出", null, delegate { Close(); });
                _trayIcon.ContextMenuStrip = menu;
            }
            catch
            {
                _trayIcon = null;
            }
        }

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == System.Windows.WindowState.Minimized)
            {
                WindowState = System.Windows.WindowState.Normal;
            }
            Activate();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _ticker.Stop();
            _saveTimer.Stop();
            SaveSettings();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_player != null)
            {
                _player.Dispose();
                _player = null;
            }
            if (_chimeStream != null)
            {
                _chimeStream.Dispose();
                _chimeStream = null;
            }
            if (_appIcon != null)
            {
                _appIcon.Dispose();
                _appIcon = null;
            }
        }

        private void ApplyPreviewState()
        {
            string state = (_previewState ?? "idle").ToLowerInvariant();
            switch (state)
            {
                case "running":
                    _mode = TimerMode.Focus;
                    _state = TimerState.Running;
                    _totalSeconds = 25 * 60;
                    _remainingSeconds = 17 * 60 + 42;
                    break;
                case "paused":
                    _mode = TimerMode.Focus;
                    _state = TimerState.Paused;
                    _totalSeconds = 25 * 60;
                    _remainingSeconds = 17 * 60 + 42;
                    break;
                case "break":
                    _mode = TimerMode.Break;
                    _state = TimerState.Running;
                    _totalSeconds = BreakSeconds;
                    _remainingSeconds = 3 * 60 + 18;
                    break;
                case "break-paused":
                    _mode = TimerMode.Break;
                    _state = TimerState.Paused;
                    _totalSeconds = BreakSeconds;
                    _remainingSeconds = 3 * 60 + 18;
                    break;
                case "custom":
                    _mode = TimerMode.Focus;
                    _state = TimerState.Idle;
                    _totalSeconds = 30 * 60;
                    _remainingSeconds = 30 * 60;
                    break;
                case "finish":
                    _mode = TimerMode.Focus;
                    _state = TimerState.Idle;
                    _totalSeconds = 25 * 60;
                    _remainingSeconds = 0;
                    break;
                case "break-finish":
                    _mode = TimerMode.Break;
                    _state = TimerState.Idle;
                    _totalSeconds = BreakSeconds;
                    _remainingSeconds = 0;
                    break;
                default:
                    _mode = TimerMode.Focus;
                    _state = TimerState.Idle;
                    _totalSeconds = 25 * 60;
                    _remainingSeconds = 25 * 60;
                    break;
            }

            UpdateUI();
            UpdateClock(true);

            if (state == "custom")
            {
                ShowCustomOverlay();
            }
            else if (state == "finish")
            {
                ShowFocusFinished();
            }
            else if (state == "break-finish")
            {
                ShowBreakFinished();
            }
        }

        private void CapturePreview()
        {
            string path = _screenshotPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(Environment.CurrentDirectory, "preview.png");
            }

            try
            {
                _root.UpdateLayout();
                double pad = 42;
                double width = _root.Width + pad * 2;
                double height = _root.Height + pad * 2;
                double scale = 2.0;

                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    dc.PushTransform(new ScaleTransform(scale, scale));

                    LinearGradientBrush background = new LinearGradientBrush(
                        Color.FromRgb(228, 233, 238),
                        Color.FromRgb(168, 183, 198),
                        new Point(0, 0),
                        new Point(1, 1));
                    dc.DrawRectangle(background, null, new Rect(0, 0, width, height));

                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), null,
                        new Point(width * 0.18, height * 0.16), 150, 150);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)), null,
                        new Point(width * 0.86, height * 0.82), 180, 180);

                    VisualBrush cardBrush = new VisualBrush(_root);
                    cardBrush.Stretch = Stretch.Fill;
                    dc.DrawRectangle(cardBrush, null, new Rect(pad, pad, _root.Width, _root.Height));
                    dc.Pop();
                }

                int pixelWidth = (int)Math.Round(width * scale);
                int pixelHeight = (int)Math.Round(height * scale);
                RenderTargetBitmap bitmap = new RenderTargetBitmap(
                    pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                using (FileStream stream = File.Create(path))
                {
                    encoder.Save(stream);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(path + ".error.txt", ex.ToString());
                }
                catch
                {
                    // Nothing else to do in preview mode.
                }
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private static string MatchPreset(int seconds)
        {
            if (seconds == 25 * 60) return "25";
            if (seconds == 30 * 60) return "30";
            if (seconds == 45 * 60) return "45";
            return "custom";
        }

        private static string FormatTime(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            int secs = seconds % 60;
            if (hours > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs);
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, secs);
        }

        private static string FriendlyDuration(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            int secs = seconds % 60;

            if (hours > 0 && minutes == 0 && secs == 0) return hours + " 小时";
            if (hours > 0) return hours + " 小时 " + minutes + " 分钟";
            if (minutes > 0 && secs == 0) return minutes + " 分钟";
            if (minutes > 0) return minutes + " 分 " + secs + " 秒";
            return secs + " 秒";
        }
    }
}
