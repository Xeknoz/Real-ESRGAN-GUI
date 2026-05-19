using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RealESRGAN_GUI
{
    public sealed class HeaderPreferenceComboBox : ComboBox
    {
        private Window? _ownerWindow;
        private bool _suppressFocusBorderOnNextFocus;

        public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
            nameof(IconData),
            typeof(Geometry),
            typeof(HeaderPreferenceComboBox),
            new PropertyMetadata(null));

        public static readonly DependencyProperty IsFocusBorderVisibleProperty = DependencyProperty.Register(
            nameof(IsFocusBorderVisible),
            typeof(bool),
            typeof(HeaderPreferenceComboBox),
            new PropertyMetadata(false));

        public static CustomPopupPlacementCallback PreferencePopupPlacementCallback { get; } = PlacePopup;

        public HeaderPreferenceComboBox()
        {
            Unloaded += (_, _) => UnhookOwnerWindow();
        }

        public Geometry? IconData
        {
            get => (Geometry?)GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public bool IsFocusBorderVisible
        {
            get => (bool)GetValue(IsFocusBorderVisibleProperty);
            private set => SetValue(IsFocusBorderVisibleProperty, value);
        }

        private static CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
        {
            return new[]
            {
                new CustomPopupPlacement(
                    new Point(targetSize.Width - popupSize.Width, targetSize.Height + 3),
                    PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(
                    new Point(0, targetSize.Height + 3),
                    PopupPrimaryAxis.Horizontal),
            };
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            _suppressFocusBorderOnNextFocus = true;
            IsFocusBorderVisible = false;
            base.OnPreviewMouseDown(e);
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            if (_suppressFocusBorderOnNextFocus)
            {
                _suppressFocusBorderOnNextFocus = false;
                IsFocusBorderVisible = false;
                return;
            }

            IsFocusBorderVisible = true;
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            _suppressFocusBorderOnNextFocus = false;
            IsFocusBorderVisible = false;
            base.OnLostKeyboardFocus(e);
        }

        protected override void OnDropDownOpened(EventArgs e)
        {
            base.OnDropDownOpened(e);
            HookOwnerWindow();
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            UnhookOwnerWindow();
            base.OnDropDownClosed(e);
        }

        private void HookOwnerWindow()
        {
            var owner = Window.GetWindow(this);
            if (ReferenceEquals(owner, _ownerWindow))
            {
                return;
            }

            UnhookOwnerWindow();
            _ownerWindow = owner;
            if (_ownerWindow is null)
            {
                return;
            }

            _ownerWindow.Deactivated += OnOwnerWindowChanged;
            _ownerWindow.LocationChanged += OnOwnerWindowChanged;
            _ownerWindow.SizeChanged += OnOwnerWindowChanged;
            _ownerWindow.StateChanged += OnOwnerWindowChanged;
        }

        private void UnhookOwnerWindow()
        {
            if (_ownerWindow is null)
            {
                return;
            }

            _ownerWindow.Deactivated -= OnOwnerWindowChanged;
            _ownerWindow.LocationChanged -= OnOwnerWindowChanged;
            _ownerWindow.SizeChanged -= OnOwnerWindowChanged;
            _ownerWindow.StateChanged -= OnOwnerWindowChanged;
            _ownerWindow = null;
        }

        private void OnOwnerWindowChanged(object? sender, EventArgs e)
        {
            IsDropDownOpen = false;
        }
    }
}
