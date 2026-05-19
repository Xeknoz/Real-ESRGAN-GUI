using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace RealESRGAN_GUI
{
    public sealed class HeaderActionControl : ContentControl
    {
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(HeaderActionControl),
            new PropertyMetadata(false));

        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
            nameof(Click),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HeaderActionControl));

        static HeaderActionControl()
        {
            FocusableProperty.OverrideMetadata(
                typeof(HeaderActionControl),
                new FrameworkPropertyMetadata(true));
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        internal void RaiseClick()
        {
            if (!IsEnabled)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            if (e.Handled || !IsEnabled)
            {
                return;
            }

            e.Handled = true;
            RaiseClick();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || !IsEnabled)
            {
                return;
            }

            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                RaiseClick();
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new HeaderActionControlAutomationPeer(this);
        }

        private sealed class HeaderActionControlAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
        {
            public HeaderActionControlAutomationPeer(HeaderActionControl owner)
                : base(owner)
            {
            }

            public void Invoke()
            {
                if (Owner is HeaderActionControl control)
                {
                    control.RaiseClick();
                }
            }

            public override object? GetPattern(PatternInterface patternInterface)
            {
                return patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Button;
            }

            protected override string GetClassNameCore()
            {
                return nameof(HeaderActionControl);
            }
        }
    }
}
