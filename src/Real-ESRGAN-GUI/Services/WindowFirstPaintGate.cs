using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace RealESRGAN_GUI.Services
{
    internal static class WindowFirstPaintGate
    {
        internal const string LauncherReadyPropertyName = "RealESRGAN_GUI_RenderReady";

        private const int WmEraseBackground = 0x0014;
        private const int DwmwaTransitionsForcedDisabled = 3;
        private const int DwmwaCloak = 13;
        private static readonly TimeSpan PaintWaitTimeout = TimeSpan.FromSeconds(2);
        private static readonly ConditionalWeakTable<Window, NativePaintGuard> PaintGuards = new();
        private static readonly ConditionalWeakTable<Window, DwmCloakRevealGuard> RevealGuards = new();

        internal static void PrepareForFirstPaint(Window window, string backgroundResourceKey = "BgBrush")
        {
            if (PaintGuards.TryGetValue(window, out _))
                return;

            var guard = new NativePaintGuard(window, backgroundResourceKey);
            PaintGuards.Add(window, guard);
            guard.Attach();
        }

        internal static void MarkLauncherReadyWhenStable(Window window)
        {
            _ = MarkLauncherReadyWhenStableAsync(window);
        }

        internal static void CloakUntilStablePaint(Window window)
        {
            if (RevealGuards.TryGetValue(window, out _))
                return;

            var guard = new DwmCloakRevealGuard(window);
            RevealGuards.Add(window, guard);
            guard.Attach();
            _ = guard.RevealWhenStableAsync();
        }

        private static async Task MarkLauncherReadyWhenStableAsync(Window window)
        {
            try
            {
                await WaitForStablePaintAsync(window);
                SetLauncherReady(window);
            }
            catch
            {
                // Keep startup resilient; the launcher still has its timeout path.
            }
            finally
            {
                SetTransitionsForcedDisabled(window, disabled: false);
            }
        }

        private static async Task WaitForStablePaintAsync(Window window)
        {
            await WaitForContentRenderedAsync(window);
            await WaitWithTimeoutAsync(WaitForRenderingFrameAsync(window));
            await WaitWithTimeoutAsync(WaitForRenderingFrameAsync(window));
            FlushDwm();
        }

        private static Task WaitForContentRenderedAsync(Window window)
        {
            if (window.Dispatcher.HasShutdownStarted)
                return Task.CompletedTask;

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler? rendered = null;
            EventHandler? closed = null;

            rendered = (_, _) =>
            {
                window.ContentRendered -= rendered;
                window.Closed -= closed;
                completion.TrySetResult();
            };

            closed = (_, _) =>
            {
                window.ContentRendered -= rendered;
                window.Closed -= closed;
                completion.TrySetResult();
            };

            window.ContentRendered += rendered;
            window.Closed += closed;
            return completion.Task;
        }

        private static Task WaitForRenderingFrameAsync(Window window)
        {
            if (window.Dispatcher.HasShutdownStarted)
                return Task.CompletedTask;

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler? rendering = null;
            EventHandler? closed = null;

            rendering = (_, _) =>
            {
                CompositionTarget.Rendering -= rendering;
                window.Closed -= closed;
                completion.TrySetResult();
            };

            closed = (_, _) =>
            {
                CompositionTarget.Rendering -= rendering;
                window.Closed -= closed;
                completion.TrySetResult();
            };

            CompositionTarget.Rendering += rendering;
            window.Closed += closed;
            return completion.Task;
        }

        private static async Task WaitWithTimeoutAsync(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(PaintWaitTimeout));
            if (completed == task)
                await task;
        }

        private static void SetLauncherReady(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                SetPropW(hwnd, LauncherReadyPropertyName, new IntPtr(1));
                window.Closed += (_, _) => RemovePropW(hwnd, LauncherReadyPropertyName);
            }
            catch
            {
                // Launcher will eventually timeout if the HWND property cannot be set.
            }
        }

        private static void SetTransitionsForcedDisabled(Window window, bool disabled)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                SetDwmBoolAttribute(hwnd, DwmwaTransitionsForcedDisabled, disabled);
            }
            catch
            {
                // DWM attributes are best-effort and unsupported in some environments.
            }
        }

        private static bool SetDwmBoolAttribute(IntPtr hwnd, int attribute, bool enabled)
        {
            try
            {
                int value = enabled ? 1 : 0;
                return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void FlushDwm()
        {
            try
            {
                DwmFlush();
            }
            catch
            {
                // DWM may be unavailable in constrained sessions; the render waits above still apply.
            }
        }

        private sealed class NativePaintGuard
        {
            private readonly Window _window;
            private readonly string _backgroundResourceKey;
            private HwndSource? _source;

            internal NativePaintGuard(Window window, string backgroundResourceKey)
            {
                _window = window;
                _backgroundResourceKey = backgroundResourceKey;
            }

            internal void Attach()
            {
                _window.SourceInitialized += OnSourceInitialized;
                _window.Closed += OnClosed;

                IntPtr hwnd = new WindowInteropHelper(_window).Handle;
                if (hwnd != IntPtr.Zero)
                    AttachToSource(hwnd);
            }

            private void OnSourceInitialized(object? sender, EventArgs e)
            {
                IntPtr hwnd = new WindowInteropHelper(_window).Handle;
                if (hwnd != IntPtr.Zero)
                    AttachToSource(hwnd);
            }

            private void AttachToSource(IntPtr hwnd)
            {
                if (_source is not null)
                    return;

                _source = HwndSource.FromHwnd(hwnd);
                _source?.AddHook(WndProc);
                SetTransitionsForcedDisabled(_window, disabled: true);
            }

            private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (msg != WmEraseBackground)
                    return IntPtr.Zero;

                handled = FillBackground(wParam);
                return handled ? new IntPtr(1) : IntPtr.Zero;
            }

            private bool FillBackground(IntPtr hdc)
            {
                Color? color = ResolveBackgroundColor(_window, _backgroundResourceKey);
                if (color is null)
                    return false;

                IntPtr brush = CreateSolidBrush(ToColorRef(color.Value));
                if (brush == IntPtr.Zero)
                    return false;

                try
                {
                    if (!GetClientRect(new WindowInteropHelper(_window).Handle, out NativeRect rect))
                        return false;

                    FillRect(hdc, ref rect, brush);
                    return true;
                }
                finally
                {
                    DeleteObject(brush);
                }
            }

            private void OnClosed(object? sender, EventArgs e)
            {
                _window.SourceInitialized -= OnSourceInitialized;
                _source?.RemoveHook(WndProc);
                _source = null;
            }
        }

        private sealed class DwmCloakRevealGuard
        {
            private readonly Window _window;
            private IntPtr _hwnd;
            private bool _cloaked;
            private bool _closed;
            private bool _finished;

            internal DwmCloakRevealGuard(Window window)
            {
                _window = window;
            }

            internal void Attach()
            {
                _window.SourceInitialized += OnSourceInitialized;
                _window.Closed += OnClosed;
                CloakIfReady();
            }

            internal async Task RevealWhenStableAsync()
            {
                try
                {
                    await WaitWithTimeoutAsync(WaitForStablePaintAsync(_window));
                }
                catch
                {
                    // The dialog must never stay invisible because a visual refinement failed.
                }
                finally
                {
                    Reveal();
                }
            }

            private void OnSourceInitialized(object? sender, EventArgs e)
            {
                CloakIfReady();
            }

            private void CloakIfReady()
            {
                if (_cloaked)
                    return;

                _hwnd = new WindowInteropHelper(_window).Handle;
                if (_hwnd == IntPtr.Zero)
                    return;

                _cloaked = SetDwmBoolAttribute(_hwnd, DwmwaCloak, enabled: true);
            }

            private void Reveal()
            {
                if (_finished)
                    return;

                _finished = true;

                if (_closed || _window.Dispatcher.HasShutdownStarted)
                {
                    Cleanup();
                    return;
                }

                if (_hwnd == IntPtr.Zero)
                    _hwnd = new WindowInteropHelper(_window).Handle;

                if (_hwnd != IntPtr.Zero)
                {
                    if (_cloaked)
                        SetDwmBoolAttribute(_hwnd, DwmwaCloak, enabled: false);
                }

                SetTransitionsForcedDisabled(_window, disabled: false);
                Cleanup();
            }

            private void OnClosed(object? sender, EventArgs e)
            {
                _closed = true;
                _finished = true;

                if (_cloaked && _hwnd != IntPtr.Zero)
                    SetDwmBoolAttribute(_hwnd, DwmwaCloak, enabled: false);

                Cleanup();
            }

            private void Cleanup()
            {
                _window.SourceInitialized -= OnSourceInitialized;
                _window.Closed -= OnClosed;
                RevealGuards.Remove(_window);
            }
        }

        private static Color? ResolveBackgroundColor(Window window, string backgroundResourceKey)
        {
            object? resource = Application.Current.TryFindResource(backgroundResourceKey);
            if (resource is SolidColorBrush brush)
                return brush.Color;

            if (window.Background is SolidColorBrush background)
                return background.Color;

            return null;
        }

        private static int ToColorRef(Color color)
        {
            return color.R | color.G << 8 | color.B << 16;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmFlush();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetPropW(IntPtr hwnd, string lpString, IntPtr hData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr RemovePropW(IntPtr hwnd, string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hwnd, out NativeRect lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int FillRect(IntPtr hdc, ref NativeRect lprc, IntPtr hbr);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateSolidBrush(int colorRef);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
