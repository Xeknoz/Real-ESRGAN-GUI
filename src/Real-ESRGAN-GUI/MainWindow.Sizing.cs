using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private const uint MonitorDefaultToNearest = 2;
        private const double EdgeTolerance = 1.0;
        private const int DwmExtendedFrameBounds = 9;
        private const int GwlStyle = -16;
        private const int WsMaximizeBox = 0x00010000;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpFrameChanged = 0x0020;
        private const double DefaultWindowWidth = 860;
        private const double MinimumWindowWidth = 560;
        private const double CompactLayoutBreakpoint = 760;
        private const double MinimumWindowHeight = 520;
        private bool? _isCompactLayout;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect rect, int attributeSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect MonitorArea;
            public NativeRect WorkArea;
            public uint Flags;
        }

        private readonly struct WindowFrameInsets
        {
            public WindowFrameInsets(double left, double top, double right, double bottom)
            {
                Left = Math.Max(0, left);
                Top = Math.Max(0, top);
                Right = Math.Max(0, right);
                Bottom = Math.Max(0, bottom);
            }

            public double Left { get; }
            public double Top { get; }
            public double Right { get; }
            public double Bottom { get; }
            public double Horizontal => Left + Right;
            public double Vertical => Top + Bottom;
        }

        private void ConfigureWindowSizing(bool keepInsideWorkArea = true)
        {
            var workArea = GetCurrentWorkArea();
            var frameInsets = GetWindowFrameInsets();
            double maxWidth = Math.Max(1, workArea.Width);
            double maxHeight = Math.Max(1, workArea.Height);
            double maxOuterWidth = maxWidth + frameInsets.Horizontal;
            double maximumWindowWidth = Math.Min(DefaultWindowWidth, maxOuterWidth);
            double minimumWindowWidth = Math.Min(MinimumWindowWidth, maximumWindowWidth);
            double workAreaOuterHeight = maxHeight + frameInsets.Vertical;

            MinWidth = minimumWindowWidth;
            MaxWidth = maximumWindowWidth;
            MinHeight = Math.Min(MinimumWindowHeight, workAreaOuterHeight);
            MaxHeight = workAreaOuterHeight;

            if (WindowState == WindowState.Normal)
            {
                if (Width > maximumWindowWidth) Width = maximumWindowWidth;
                if (Width < minimumWindowWidth) Width = minimumWindowWidth;
                if (Height > workAreaOuterHeight) Height = workAreaOuterHeight;

                if (keepInsideWorkArea)
                    KeepNormalWindowInsideWorkArea(workArea, frameInsets);
            }

            MainScrollViewer.MaxHeight = Math.Max(80, maxHeight);
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            double currentWidth = ActualWidth > 0 ? ActualWidth : Width;
            bool useCompactLayout = currentWidth <= CompactLayoutBreakpoint;
            if (_isCompactLayout == useCompactLayout)
                return;

            _isCompactLayout = useCompactLayout;
            ApplyHeaderLayout(useCompactLayout);
            ApplyFoldersLayout(useCompactLayout);
            ApplyBasicSettingsLayout(useCompactLayout);
            ApplyAdvancedSettingsLayout(useCompactLayout);
            ApplyRunButtonsLayout(useCompactLayout);
        }

        private void ApplyHeaderLayout(bool compact)
        {
            HeaderContentColumn.Width = new GridLength(1, GridUnitType.Star);
            HeaderActionsColumn.Width = compact ? new GridLength(0) : GridLength.Auto;
            HeaderCompactGap.Height = compact ? new GridLength(14) : new GridLength(0);
            HeaderActionsRow.Height = compact ? GridLength.Auto : new GridLength(0);

            Grid.SetRow(HeaderTitlePanel, 0);
            Grid.SetColumn(HeaderTitlePanel, 0);
            Grid.SetRow(HeaderActionsPanel, compact ? 2 : 0);
            Grid.SetColumn(HeaderActionsPanel, compact ? 0 : 1);
            HeaderActionsPanel.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }

        private void ApplyFoldersLayout(bool compact)
        {
            FoldersFirstColumn.Width = new GridLength(1, GridUnitType.Star);
            FoldersColumnGap.Width = compact ? new GridLength(0) : new GridLength(18);
            FoldersSecondColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

            FoldersFirstRow.Height = GridLength.Auto;
            FoldersRowGap.Height = compact ? new GridLength(18) : new GridLength(0);
            FoldersSecondRow.Height = compact ? GridLength.Auto : new GridLength(0);

            Grid.SetRow(InputCard, 0);
            Grid.SetColumn(InputCard, 0);
            Grid.SetRow(OutputCard, compact ? 2 : 0);
            Grid.SetColumn(OutputCard, compact ? 0 : 2);
        }

        private void ApplyBasicSettingsLayout(bool compact)
        {
            BasicSettingsFirstColumn.Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(1.15, GridUnitType.Star);
            BasicSettingsFirstColumnGap.Width = compact ? new GridLength(0) : new GridLength(16);
            BasicSettingsSecondColumn.Width = compact ? new GridLength(0) : new GridLength(0.8, GridUnitType.Star);
            BasicSettingsSecondColumnGap.Width = compact ? new GridLength(0) : new GridLength(16);
            BasicSettingsThirdColumn.Width = compact ? new GridLength(0) : new GridLength(1.2, GridUnitType.Star);

            BasicSettingsFirstRow.Height = GridLength.Auto;
            BasicSettingsSecondRowGap.Height = compact ? new GridLength(12) : new GridLength(0);
            BasicSettingsSecondRow.Height = compact ? GridLength.Auto : new GridLength(0);
            BasicSettingsThirdRowGap.Height = compact ? new GridLength(12) : new GridLength(0);
            BasicSettingsThirdRow.Height = compact ? GridLength.Auto : new GridLength(0);

            Grid.SetRow(ModelField, 0);
            Grid.SetColumn(ModelField, 0);
            Grid.SetRow(ScaleField, compact ? 2 : 0);
            Grid.SetColumn(ScaleField, compact ? 0 : 2);
            Grid.SetRow(FormatField, compact ? 4 : 0);
            Grid.SetColumn(FormatField, compact ? 0 : 4);
        }

        private void ApplyAdvancedSettingsLayout(bool compact)
        {
            AdvancedSettingsFirstColumn.Width = new GridLength(1, GridUnitType.Star);
            AdvancedSettingsColumnGap.Width = compact ? new GridLength(0) : new GridLength(16);
            AdvancedSettingsSecondColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

            AdvancedSettingsSecondRowGap.Height = compact ? new GridLength(12) : new GridLength(0);
            AdvancedSettingsSecondRow.Height = compact ? GridLength.Auto : new GridLength(0);

            Grid.SetRow(ThreadsField, 0);
            Grid.SetColumn(ThreadsField, 0);
            Grid.SetRow(GpuField, compact ? 2 : 0);
            Grid.SetColumn(GpuField, compact ? 0 : 2);
            Grid.SetColumnSpan(TtaCheck, compact ? 1 : 3);
        }

        private void ApplyRunButtonsLayout(bool compact)
        {
            RunButtonsPanel.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
            StartButton.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            StopButton.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            StopButton.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(10, 0, 0, 0);
        }

        private void ConfigureWindowChromeForVerticalResize()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            IntPtr currentStyle = GetWindowStyle(hwnd);
            int updatedStyle = currentStyle.ToInt32() & ~WsMaximizeBox;
            if (updatedStyle == currentStyle.ToInt32())
                return;

            SetWindowStyle(hwnd, new IntPtr(updatedStyle));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpFrameChanged);
        }

        private static IntPtr GetWindowStyle(IntPtr hwnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, GwlStyle)
                : new IntPtr(GetWindowLong32(hwnd, GwlStyle));
        }

        private static void SetWindowStyle(IntPtr hwnd, IntPtr style)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(hwnd, GwlStyle, style);
            }
            else
            {
                SetWindowLong32(hwnd, GwlStyle, style.ToInt32());
            }
        }

        private void KeepNormalWindowInsideWorkArea(Rect workArea, WindowFrameInsets frameInsets)
        {
            if (WindowState != WindowState.Normal)
                return;

            double visibleLeft = Left + frameInsets.Left;
            if (visibleLeft < workArea.Left)
            {
                Left = workArea.Left - frameInsets.Left;
            }

            double visibleRight = Left + Width - frameInsets.Right;
            double rightOverflow = visibleRight - workArea.Right;
            if (rightOverflow > EdgeTolerance)
            {
                Left = workArea.Right + frameInsets.Right - Width;
            }

            double visibleTop = Top + frameInsets.Top;
            if (visibleTop < workArea.Top)
            {
                Top = workArea.Top - frameInsets.Top;
            }

            double visibleBottom = Top + Height - frameInsets.Bottom;
            double bottomOverflow = visibleBottom - workArea.Bottom;
            if (bottomOverflow > EdgeTolerance)
            {
                Top = workArea.Bottom + frameInsets.Bottom - Height;
            }
        }

        private WindowFrameInsets GetWindowFrameInsets()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return new WindowFrameInsets(0, 0, 0, 0);

            if (!GetWindowRect(hwnd, out NativeRect outer) ||
                DwmGetWindowAttribute(hwnd, DwmExtendedFrameBounds, out NativeRect visible, Marshal.SizeOf<NativeRect>()) != 0)
            {
                return new WindowFrameInsets(0, 0, 0, 0);
            }

            var source = PresentationSource.FromVisual(this);
            Matrix transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

            Point outerTopLeft = transform.Transform(new Point(outer.Left, outer.Top));
            Point outerBottomRight = transform.Transform(new Point(outer.Right, outer.Bottom));
            Point visibleTopLeft = transform.Transform(new Point(visible.Left, visible.Top));
            Point visibleBottomRight = transform.Transform(new Point(visible.Right, visible.Bottom));

            return new WindowFrameInsets(
                visibleTopLeft.X - outerTopLeft.X,
                visibleTopLeft.Y - outerTopLeft.Y,
                outerBottomRight.X - visibleBottomRight.X,
                outerBottomRight.Y - visibleBottomRight.Y);
        }

        private Rect GetCurrentWorkArea()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    var source = PresentationSource.FromVisual(this);
                    if (source?.CompositionTarget is not null)
                    {
                        Matrix transform = source.CompositionTarget.TransformFromDevice;
                        Point topLeft = transform.Transform(new Point(info.WorkArea.Left, info.WorkArea.Top));
                        Point bottomRight = transform.Transform(new Point(info.WorkArea.Right, info.WorkArea.Bottom));
                        return new Rect(topLeft, bottomRight);
                    }

                    return new Rect(info.WorkArea.Left, info.WorkArea.Top, info.WorkArea.Width, info.WorkArea.Height);
                }
            }

            return SystemParameters.WorkArea;
        }
    }
}
