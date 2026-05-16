using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private const uint MonitorDefaultToNearest = 2;
        private const double EdgeTolerance = 1.0;
        private const int DwmExtendedFrameBounds = 9;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

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

        private void ConfigureWindowSizing()
        {
            var workArea = GetCurrentWorkArea();
            var frameInsets = GetWindowFrameInsets();
            double maxWidth = Math.Max(1, workArea.Width);
            double maxHeight = Math.Max(1, workArea.Height);
            double maxOuterWidth = maxWidth + frameInsets.Horizontal;
            double maxOuterHeight = maxHeight + frameInsets.Vertical;

            bool maximized = WindowState == WindowState.Maximized;
            MaxWidth = maximized ? double.PositiveInfinity : maxOuterWidth;
            MaxHeight = maximized ? double.PositiveInfinity : maxOuterHeight;
            MinWidth = Math.Min(860, maxOuterWidth);
            MinHeight = Math.Min(520, maxOuterHeight);

            if (WindowState == WindowState.Normal)
            {
                if (Width > maxOuterWidth) Width = maxOuterWidth;
                if (_windowHeightFrozen && Height > maxOuterHeight) Height = maxOuterHeight;
                KeepNormalWindowInsideWorkArea(workArea, frameInsets);
            }

            MainScrollViewer.MaxHeight = Math.Max(80, maxHeight);
        }

        private void FreezeAdaptiveHeight()
        {
            if (_windowHeightFrozen) return;

            UpdateLayout();
            Height = Math.Min(ActualHeight, MaxHeight);
            SizeToContent = SizeToContent.Manual;
            _windowHeightFrozen = true;
            ConfigureWindowSizing();
        }

        private void KeepNormalWindowInsideWorkArea(Rect workArea, WindowFrameInsets frameInsets)
        {
            if (WindowState != WindowState.Normal)
                return;

            double visibleTop = Top + frameInsets.Top;
            if (visibleTop < workArea.Top)
            {
                Top = workArea.Top - frameInsets.Top;
                visibleTop = workArea.Top;
            }

            if (Math.Abs(visibleTop - workArea.Top) <= EdgeTolerance &&
                Height < workArea.Height + frameInsets.Vertical - EdgeTolerance)
            {
                Top = workArea.Top - frameInsets.Top;
                Height = workArea.Height + frameInsets.Vertical;
                return;
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
