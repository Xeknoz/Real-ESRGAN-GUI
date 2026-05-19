using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private const string LaunchReadyPropertyName = "RealESRGAN_GUI_RenderReady";
        private static readonly IntPtr LaunchReadyPropertyValue = new(1);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetProp(IntPtr hwnd, string name, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr RemoveProp(IntPtr hwnd, string name);

        private void ConfigureLaunchReadinessTransition()
        {
            Opacity = 0;
            ContentRendered += OnInitialContentRendered;
            Closed += (_, _) => ClearLaunchReadySignal();
        }

        private void OnInitialContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnInitialContentRendered;
            Opacity = 1;

            Dispatcher.BeginInvoke(new Action(MarkLaunchReady), DispatcherPriority.ApplicationIdle);
        }

        private void MarkLaunchReady()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            SetProp(hwnd, LaunchReadyPropertyName, LaunchReadyPropertyValue);
        }

        private void ClearLaunchReadySignal()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            RemoveProp(hwnd, LaunchReadyPropertyName);
        }
    }
}
