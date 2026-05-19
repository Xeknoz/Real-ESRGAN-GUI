using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RealESRGAN_GUI.Services
{
    internal static class RunningInstanceBridge
    {
        internal const string AlreadyRunningNoticeMutexName = @"Global\RealESRGAN_AlreadyRunningNotice";

        private const string QueryLanguageMessageName = "RealESRGAN_GUI_QueryLanguage";
        private const int LanguageChinese = 1;
        private const int LanguageEnglish = 2;
        private const int SmtoAbortIfHung = 0x0002;
        private const int SendTimeoutMs = 200;
        private const int SwShownormal = 1;

        private static readonly uint QueryLanguageMessage = RegisterWindowMessage(QueryLanguageMessageName);

        internal static void AttachLanguageResponder(Window window, Func<string> getLanguage)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            HwndSource.FromHwnd(hwnd)?.AddHook((IntPtr sourceHwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if ((uint)msg != QueryLanguageMessage)
                {
                    return IntPtr.Zero;
                }

                handled = true;
                return new IntPtr(NormalizeLanguage(getLanguage()) == "en" ? LanguageEnglish : LanguageChinese);
            });
        }

        internal static string ResolveNoticeLanguage()
        {
            return TryQueryExistingInstanceLanguage() ?? ResolveSystemLanguage();
        }

        internal static string ResolveSystemLanguage()
        {
            string name = CultureInfo.CurrentUICulture.Name;
            return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
        }

        internal static void ActivateExistingNoticeWindow()
        {
            var hwnd = FindWindow("RESG_Notice", null);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(hwnd, SwShownormal);
            SetForegroundWindow(hwnd);
        }

        private static string? TryQueryExistingInstanceLanguage()
        {
            var hwnd = FindExistingAppWindow();
            if (hwnd == IntPtr.Zero || QueryLanguageMessage == 0)
            {
                return null;
            }

            var sent = SendMessageTimeout(
                hwnd,
                QueryLanguageMessage,
                IntPtr.Zero,
                IntPtr.Zero,
                SmtoAbortIfHung,
                SendTimeoutMs,
                out IntPtr result);

            if (sent == IntPtr.Zero)
            {
                return null;
            }

            int language = result.ToInt32();
            return language switch
            {
                LanguageChinese => "zh",
                LanguageEnglish => "en",
                _ => null,
            };
        }

        private static IntPtr FindExistingAppWindow()
        {
            var hwnd = FindWindow(null, "Real-ESRGAN GUI");
            if (hwnd != IntPtr.Zero)
            {
                return hwnd;
            }

            return FindWindow("HwndWrapper[RealESRGAN_GUI*", null);
        }

        private static string NormalizeLanguage(string? language)
        {
            return string.Equals(language, "en", StringComparison.Ordinal) ? "en" : "zh";
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            int fuFlags,
            int uTimeout,
            out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
