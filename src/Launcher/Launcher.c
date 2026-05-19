// src/Launcher/Launcher.c
// Win32 native splash launcher for Real-ESRGAN GUI.
// Displays a HiDPI-aware themed splash screen while starting the WPF application.
//
// Build: run .\src\Launcher\build.ps1 from the repository root.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <dwmapi.h>
#include <strsafe.h>
#include "resource.h"

#if defined(__has_include)
#if __has_include("Launcher.version.h")
#include "Launcher.version.h"
#endif
#endif

#ifndef LAUNCHER_DISPLAY_VERSION
#define LAUNCHER_DISPLAY_VERSION L"v0.0.0"
#endif

// ---- Design values (96 DPI baseline) ----
#define DESIGN_W      400
#define DESIGN_H      130
#define TIMER_ID      1
#define TIMER_MS      16
#define FADE_STEPS    12
#define TIMEOUT_MS    15000
#define NOTICE_W      420
#define NOTICE_H      170
#define NOTICE_MUTEX_NAME L"Global\\RealESRGAN_AlreadyRunningNotice"
#define QUERY_LANGUAGE_MESSAGE_NAME L"RealESRGAN_GUI_QueryLanguage"
#define QUERY_LANGUAGE_ZH 1
#define QUERY_LANGUAGE_EN 2
#define MAIN_WINDOW_READY_PROP_NAME L"RealESRGAN_GUI_RenderReady"

static HWND   g_hwnd   = NULL;
static HWND   g_mainHwnd = NULL;
static HANDLE g_hProc  = NULL;
static DWORD  g_pid    = 0;
static BOOL   g_dark   = TRUE;
static BOOL   g_zh     = TRUE;
static BOOL   g_found  = FALSE;
static int    g_pulse  = -40;
static int    g_dpi    = 96;
static int    g_w      = DESIGN_W;
static int    g_h      = DESIGN_H;
static BOOL   g_noticeDone = FALSE;
static BOOL   g_noticeButtonHot = FALSE;
static RECT   g_noticeButtonRect = {0};

// Scale a 96-DPI design value to the current monitor DPI.
static int S(int v) { return MulDiv(v, g_dpi, 96); }

static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
static LRESULT CALLBACK NoticeWndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
static void    DetectLocaleAndTheme(void);
static void    Paint(HWND hWnd);
static void    PaintNotice(HWND hWnd);
static void    Launch(void);
static BOOL    FindMainWindow(void);
static BOOL    IsAppAlreadyRunning(void);
static HWND    FindExistingAppWindow(void);
static void    ActivateAppWindow(HWND hwnd);
static void    ActivateNoticeWindow(HWND hwnd);
static void    ResolveRunningInstanceLanguage(HWND hwnd);
static LPCWSTR NoticeFontFace(void);
static void    ShowAlreadyRunningNotice(HINSTANCE hInst, HWND owner);

int WINAPI wWinMain(HINSTANCE hInst, HINSTANCE hPrev, LPWSTR lpCmd, int nShow)
{
    (void)hPrev; (void)lpCmd; (void)nShow;

    // ---- Set Per-Monitor V2 DPI awareness (Win10 1703+) ----
    typedef BOOL (WINAPI *SetProcessDpiAwarenessContextFn)(DPI_AWARENESS_CONTEXT);
    HMODULE hUser32 = GetModuleHandleW(L"user32.dll");
    SetProcessDpiAwarenessContextFn setDpiCtx =
        (SetProcessDpiAwarenessContextFn)GetProcAddress(hUser32, "SetProcessDpiAwarenessContext");
    if (setDpiCtx) {
        setDpiCtx(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    } else {
        SetProcessDPIAware();
    }

    // ---- Query primary monitor DPI ----
    HDC hdcScreen = GetDC(NULL);
    g_dpi = GetDeviceCaps(hdcScreen, LOGPIXELSX);
    ReleaseDC(NULL, hdcScreen);

    g_w = S(DESIGN_W);
    g_h = S(DESIGN_H);

    DetectLocaleAndTheme();

    // ---- Single instance mutex ----
    HANDLE mutex = CreateMutexW(NULL, TRUE, L"Global\\RealESRGAN_Launcher");
    if (GetLastError() == ERROR_ALREADY_EXISTS) {
        HWND existing = FindExistingAppWindow();
        if (existing) {
            ActivateAppWindow(existing);
        }
        ShowAlreadyRunningNotice(hInst, existing);
        if (mutex) { ReleaseMutex(mutex); CloseHandle(mutex); }
        return 0;
    }

    if (IsAppAlreadyRunning()) {
        HWND existing = FindExistingAppWindow();
        if (existing) {
            ActivateAppWindow(existing);
        }
        ShowAlreadyRunningNotice(hInst, existing);
        if (mutex) { ReleaseMutex(mutex); CloseHandle(mutex); }
        return 0;
    }

    // ---- Register window class ----
    WNDCLASSEXW wc = { sizeof(wc) };
    wc.lpfnWndProc   = WndProc;
    wc.hInstance     = hInst;
    wc.lpszClassName = L"RESG_Splash";
    wc.hCursor       = LoadCursorW(NULL, (LPCWSTR)IDC_ARROW);
    wc.hIcon         = LoadIconW(hInst, MAKEINTRESOURCEW(IDI_APP_ICON));
    wc.hIconSm       = LoadIconW(hInst, MAKEINTRESOURCEW(IDI_APP_ICON));
    RegisterClassExW(&wc);

    int cx = GetSystemMetrics(SM_CXSCREEN);
    int cy = GetSystemMetrics(SM_CYSCREEN);

    g_hwnd = CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        L"RESG_Splash", L"", WS_POPUP,
        (cx - g_w) / 2, (cy - g_h) / 2,
        g_w, g_h,
        NULL, NULL, hInst, NULL);

    if (!g_hwnd) {
        if (mutex) { ReleaseMutex(mutex); CloseHandle(mutex); }
        return 1;
    }

    // ---- DWM: dark title bar + rounded corners (Win11) ----
    int useDark = g_dark ? 1 : 0;
    DwmSetWindowAttribute(g_hwnd, 20, &useDark, sizeof(int));
    DWM_WINDOW_CORNER_PREFERENCE corner = 2; // DWMWCP_ROUND
    DwmSetWindowAttribute(g_hwnd, 33, &corner, sizeof(corner));

    // ---- Show a visible splash first, then start the main app beside it ----
    SetLayeredWindowAttributes(g_hwnd, 0, 255, LWA_ALPHA);
    ShowWindow(g_hwnd, SW_SHOW);
    UpdateWindow(g_hwnd);
    DwmFlush(); // Commit the first visible frame before CreateProcess can block.

    // Launch immediately after the first visible frame so both phases run together.
    Launch();
    if (!g_pid) {
        DestroyWindow(g_hwnd);
        if (mutex) { ReleaseMutex(mutex); CloseHandle(mutex); }
        return 1;
    }

    SetTimer(g_hwnd, TIMER_ID, TIMER_MS, NULL);

    // ---- Message loop: animate + poll for main window ----
    MSG msg;
    DWORD t0 = GetTickCount();
    BOOL foundWindow = FALSE;
    while (!g_found) {
        while (PeekMessageW(&msg, NULL, 0, 0, PM_REMOVE)) {
            if (msg.message == WM_QUIT) { g_found = TRUE; break; }
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        if (g_found) break;

        if (FindMainWindow()) {
            foundWindow = TRUE;
            g_found = TRUE;
            break;
        }
        if (GetTickCount() - t0 > TIMEOUT_MS) break;
        Sleep(5);
    }

    if (foundWindow) {
        // Main window is ready — dismiss splash immediately, no fade-out
        HWND mainWindow = g_mainHwnd;
        DestroyWindow(g_hwnd);
        ActivateAppWindow(mainWindow);
    } else {
        // Timeout or early exit — graceful fade-out
        for (int i = FADE_STEPS - 1; i >= 0; i--) {
            SetLayeredWindowAttributes(g_hwnd, 0, (BYTE)(i * 255 / FADE_STEPS), LWA_ALPHA);
            UpdateWindow(g_hwnd);
            Sleep(6);
        }
        DestroyWindow(g_hwnd);
    }

    if (g_hProc) CloseHandle(g_hProc);
    if (mutex) { ReleaseMutex(mutex); CloseHandle(mutex); }
    return 0;
}

static void DetectLocaleAndTheme(void)
{
    WCHAR locale[16] = {0};
    GetUserDefaultLocaleName(locale, 16);
    g_zh = (locale[0] == L'z' && locale[1] == L'h');

    HKEY hKey;
    DWORD value = 1, size = sizeof(value);
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
            L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
            0, KEY_READ, &hKey) == ERROR_SUCCESS) {
        RegQueryValueExW(hKey, L"AppsUseLightTheme", NULL, NULL, (LPBYTE)&value, &size);
        RegCloseKey(hKey);
    }
    g_dark = (value == 0);
}

static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    (void)lParam;
    switch (msg) {
    case WM_PAINT:
        Paint(hWnd);
        return 0;

    case WM_TIMER:
        if (wParam == TIMER_ID) {
            g_pulse += S(5);
            if (g_pulse > g_w + S(40)) g_pulse = -S(40);
            InvalidateRect(hWnd, NULL, FALSE);
        }
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

static LRESULT CALLBACK NoticeWndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg) {
    case WM_PAINT:
        PaintNotice(hWnd);
        return 0;

    case WM_MOUSEMOVE:
    {
        POINT pt = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        BOOL hot = PtInRect(&g_noticeButtonRect, pt);
        if (hot != g_noticeButtonHot) {
            g_noticeButtonHot = hot;
            InvalidateRect(hWnd, &g_noticeButtonRect, FALSE);
        }
        TRACKMOUSEEVENT tme = { sizeof(tme), TME_LEAVE, hWnd, 0 };
        TrackMouseEvent(&tme);
        return 0;
    }

    case WM_MOUSELEAVE:
        if (g_noticeButtonHot) {
            g_noticeButtonHot = FALSE;
            InvalidateRect(hWnd, &g_noticeButtonRect, FALSE);
        }
        return 0;

    case WM_LBUTTONUP:
    {
        POINT pt = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        if (PtInRect(&g_noticeButtonRect, pt)) {
            DestroyWindow(hWnd);
        }
        return 0;
    }

    case WM_KEYDOWN:
        if (wParam == VK_RETURN || wParam == VK_SPACE || wParam == VK_ESCAPE) {
            DestroyWindow(hWnd);
            return 0;
        }
        break;

    case WM_SETCURSOR:
    {
        POINT pt;
        GetCursorPos(&pt);
        ScreenToClient(hWnd, &pt);
        if (PtInRect(&g_noticeButtonRect, pt)) {
            SetCursor(LoadCursorW(NULL, (LPCWSTR)IDC_HAND));
            return TRUE;
        }
        break;
    }

    case WM_DESTROY:
        g_noticeDone = TRUE;
        return 0;
    }

    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

static void Paint(HWND hWnd)
{
    PAINTSTRUCT ps;
    HDC hdc = BeginPaint(hWnd, &ps);

    RECT rc;
    GetClientRect(hWnd, &rc);
    int w = rc.right - rc.left;
    int h = rc.bottom - rc.top;

    // ---- Double-buffer: draw to memory DC, blit to screen ----
    HDC memDC = CreateCompatibleDC(hdc);
    HBITMAP memBmp = CreateCompatibleBitmap(hdc, w, h);
    HBITMAP oldBmp = (HBITMAP)SelectObject(memDC, memBmp);

    HDC dc = memDC; // All drawing targets memDC

    // Background fill
    COLORREF bg = g_dark ? RGB(27, 31, 28) : RGB(247, 244, 238);
    HBRUSH hbrBg = CreateSolidBrush(bg);
    FillRect(dc, &rc, hbrBg);
    DeleteObject(hbrBg);

    // 1px border
    HPEN hPen = CreatePen(PS_SOLID, S(1), g_dark ? RGB(61, 70, 61) : RGB(217, 209, 196));
    HPEN hOldPen = (HPEN)SelectObject(dc, hPen);
    HBRUSH hOldBrush = (HBRUSH)SelectObject(dc, GetStockObject(NULL_BRUSH));
    Rectangle(dc, rc.left, rc.top, rc.right, rc.bottom);
    SelectObject(dc, hOldPen);
    SelectObject(dc, hOldBrush);
    DeleteObject(hPen);

    // Text colours
    COLORREF clrText  = g_dark ? RGB(244, 240, 232) : RGB(34, 32, 28);
    COLORREF clrMuted = g_dark ? RGB(192, 184, 171) : RGB(109, 103, 94);
    COLORREF clrSubtle= g_dark ? RGB(142, 150, 142) : RGB(148, 141, 130);
    COLORREF clrAccent= g_dark ? RGB(102, 194, 178) : RGB(40, 108, 103);
    COLORREF clrTrack = g_dark ? RGB(43, 51, 45)  : RGB(232, 224, 213);

    SetBkMode(dc, TRANSPARENT);

    // Title: "Real-ESRGAN"
    HFONT hFontTitle = CreateFontW(-S(22), 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Microsoft YaHei UI");
    HFONT hOldFont = (HFONT)SelectObject(dc, hFontTitle);
    SetTextColor(dc, clrText);
    RECT rcTitle = { S(28), S(20), S(280), S(46) };
    DrawTextW(dc, L"Real-ESRGAN", -1, &rcTitle, DT_LEFT | DT_SINGLELINE);

    // Version
    HFONT hFontVer = CreateFontW(-S(11), 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Segoe UI");
    SelectObject(dc, hFontVer);
    SetTextColor(dc, clrSubtle);
    RECT rcVer = { S(170), S(24), g_w - S(28), S(42) };
    DrawTextW(dc, LAUNCHER_DISPLAY_VERSION, -1, &rcVer, DT_RIGHT | DT_SINGLELINE | DT_END_ELLIPSIS);

    // Subtitle
    HFONT hFontSub = CreateFontW(-S(12), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Microsoft YaHei UI");
    SelectObject(dc, hFontSub);
    SetTextColor(dc, clrMuted);
    RECT rcSub = { S(28), S(48), S(220), S(66) };
    DrawTextW(dc, g_zh ? L"图像超分辨率工具" : L"Image Super-Resolution", -1, &rcSub, DT_LEFT | DT_SINGLELINE);

    // Status
    HFONT hFontStatus = CreateFontW(-S(11), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, L"Microsoft YaHei UI");
    SelectObject(dc, hFontStatus);
    SetTextColor(dc, clrMuted);
    RECT rcStatus = { g_w - S(130), S(48), g_w - S(28), S(66) };
    DrawTextW(dc, g_zh ? L"正在启动..." : L"Starting...", -1, &rcStatus, DT_RIGHT | DT_SINGLELINE);

    // Progress track
    HBRUSH hbrTrack = CreateSolidBrush(clrTrack);
    RECT rcTrack = { S(28), g_h - S(26), g_w - S(28), g_h - S(24) };
    FillRect(dc, &rcTrack, hbrTrack);
    DeleteObject(hbrTrack);

    // Progress pulse (clipped to track bounds)
    HBRUSH hbrPulse = CreateSolidBrush(clrAccent);
    RECT rcPulse = { g_pulse, g_h - S(26), g_pulse + S(40), g_h - S(24) };
    if (rcPulse.right > rcTrack.left && rcPulse.left < rcTrack.right) {
        if (rcPulse.left < rcTrack.left) rcPulse.left = rcTrack.left;
        if (rcPulse.right > rcTrack.right) rcPulse.right = rcTrack.right;
        FillRect(dc, &rcPulse, hbrPulse);
    }
    DeleteObject(hbrPulse);

    SelectObject(dc, hOldFont);
    DeleteObject(hFontTitle);
    DeleteObject(hFontVer);
    DeleteObject(hFontSub);
    DeleteObject(hFontStatus);

    // Blit memory buffer to screen
    BitBlt(hdc, 0, 0, w, h, memDC, 0, 0, SRCCOPY);

    SelectObject(memDC, oldBmp);
    DeleteObject(memBmp);
    DeleteDC(memDC);

    EndPaint(hWnd, &ps);
}

static void PaintNotice(HWND hWnd)
{
    PAINTSTRUCT ps;
    HDC hdc = BeginPaint(hWnd, &ps);

    RECT rc;
    GetClientRect(hWnd, &rc);
    int w = rc.right - rc.left;
    int h = rc.bottom - rc.top;

    HDC memDC = CreateCompatibleDC(hdc);
    HBITMAP memBmp = CreateCompatibleBitmap(hdc, w, h);
    HBITMAP oldBmp = (HBITMAP)SelectObject(memDC, memBmp);
    HDC dc = memDC;

    COLORREF clrSurface = g_dark ? RGB(20, 29, 35) : RGB(255, 255, 255);
    COLORREF clrBorder  = g_dark ? RGB(38, 58, 67) : RGB(226, 234, 240);
    COLORREF clrText    = g_dark ? RGB(244, 247, 250) : RGB(24, 34, 43);
    COLORREF clrMuted   = g_dark ? RGB(181, 192, 201) : RGB(85, 99, 110);
    COLORREF clrAccent  = g_noticeButtonHot
        ? (g_dark ? RGB(94, 234, 212) : RGB(11, 98, 92))
        : (g_dark ? RGB(45, 212, 191) : RGB(15, 118, 110));
    COLORREF clrOnAccent = g_dark ? RGB(6, 49, 45) : RGB(255, 255, 255);

    HBRUSH hbrSurface = CreateSolidBrush(clrSurface);
    FillRect(dc, &rc, hbrSurface);
    DeleteObject(hbrSurface);

    HPEN hPen = CreatePen(PS_SOLID, S(1), clrBorder);
    HPEN hOldPen = (HPEN)SelectObject(dc, hPen);
    HBRUSH hOldBrush = (HBRUSH)SelectObject(dc, GetStockObject(NULL_BRUSH));
    RoundRect(dc, rc.left, rc.top, rc.right, rc.bottom, S(18), S(18));
    SelectObject(dc, hOldPen);
    SelectObject(dc, hOldBrush);
    DeleteObject(hPen);

    SetBkMode(dc, TRANSPARENT);

    HFONT hFontTitle = CreateFontW(-S(18), 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, NoticeFontFace());
    HFONT hOldFont = (HFONT)SelectObject(dc, hFontTitle);
    SetTextColor(dc, clrText);
    RECT rcTitle = { S(24), S(22), w - S(24), S(48) };
    DrawTextW(dc, g_zh ? L"提示" : L"Notice", -1, &rcTitle, DT_LEFT | DT_SINGLELINE | DT_END_ELLIPSIS);

    HFONT hFontMessage = CreateFontW(-S(13), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, NoticeFontFace());
    SelectObject(dc, hFontMessage);
    SetTextColor(dc, clrMuted);
    RECT rcMessage = { S(24), S(58), w - S(24), h - S(70) };
    DrawTextW(dc,
        g_zh ? L"Real-ESRGAN GUI 已经在运行中。" : L"Real-ESRGAN GUI is already running.",
        -1, &rcMessage, DT_LEFT | DT_WORDBREAK);

    g_noticeButtonRect.left = w - S(120);
    g_noticeButtonRect.top = h - S(56);
    g_noticeButtonRect.right = w - S(24);
    g_noticeButtonRect.bottom = h - S(22);

    HBRUSH hbrButton = CreateSolidBrush(clrAccent);
    HPEN hButtonPen = CreatePen(PS_SOLID, S(1), clrAccent);
    hOldPen = (HPEN)SelectObject(dc, hButtonPen);
    hOldBrush = (HBRUSH)SelectObject(dc, hbrButton);
    RoundRect(dc,
        g_noticeButtonRect.left,
        g_noticeButtonRect.top,
        g_noticeButtonRect.right,
        g_noticeButtonRect.bottom,
        S(12),
        S(12));
    SelectObject(dc, hOldPen);
    SelectObject(dc, hOldBrush);
    DeleteObject(hButtonPen);
    DeleteObject(hbrButton);

    HFONT hFontButton = CreateFontW(-S(12), 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_SWISS, NoticeFontFace());
    SelectObject(dc, hFontButton);
    SetTextColor(dc, clrOnAccent);
    DrawTextW(dc, g_zh ? L"确定" : L"OK", -1, &g_noticeButtonRect,
        DT_CENTER | DT_VCENTER | DT_SINGLELINE);

    SelectObject(dc, hOldFont);
    DeleteObject(hFontTitle);
    DeleteObject(hFontMessage);
    DeleteObject(hFontButton);

    BitBlt(hdc, 0, 0, w, h, memDC, 0, 0, SRCCOPY);
    SelectObject(memDC, oldBmp);
    DeleteObject(memBmp);
    DeleteDC(memDC);

    EndPaint(hWnd, &ps);
}

static void Launch(void)
{
    WCHAR launcherPath[MAX_PATH];
    GetModuleFileNameW(NULL, launcherPath, MAX_PATH);

    WCHAR* pSlash = wcsrchr(launcherPath, L'\\');
    if (pSlash) *(pSlash + 1) = L'\0';

    WCHAR appPath[MAX_PATH];
    StringCchCopyW(appPath, MAX_PATH, launcherPath);
    StringCchCatW(appPath, MAX_PATH, L"Real-ESRGAN GUI.exe");

    if (GetFileAttributesW(appPath) == INVALID_FILE_ATTRIBUTES) {
        // Fallback: try without hyphen
        StringCchCopyW(appPath, MAX_PATH, launcherPath);
        StringCchCatW(appPath, MAX_PATH, L"RealESRGAN GUI.exe");
        if (GetFileAttributesW(appPath) == INVALID_FILE_ATTRIBUTES) {
            MessageBoxW(g_hwnd,
                g_zh ? L"找不到主程序文件。" : L"Main application not found.",
                g_zh ? L"启动错误" : L"Launch Error",
                MB_OK | MB_ICONERROR);
            return;
        }
    }

    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    WCHAR commandLine[MAX_PATH + 32];
    StringCchPrintfW(commandLine, ARRAYSIZE(commandLine), L"\"%s\" --from-launcher", appPath);

    if (CreateProcessW(appPath, commandLine, NULL, NULL, FALSE,
                       CREATE_NEW_PROCESS_GROUP, NULL, launcherPath, &si, &pi)) {
        g_hProc = pi.hProcess;
        g_pid   = pi.dwProcessId;
        AllowSetForegroundWindow(g_pid);
        CloseHandle(pi.hThread);
    } else {
        DWORD err = GetLastError();
        WCHAR msg[256];
        StringCchPrintfW(msg, 256,
            g_zh ? L"无法启动主程序。\n错误代码: %lu" : L"Failed to launch.\nError: %lu",
            err);
        MessageBoxW(g_hwnd, msg,
            g_zh ? L"启动错误" : L"Launch Error",
            MB_OK | MB_ICONERROR);
    }
}

static BOOL CALLBACK EnumWindowProc(HWND hwnd, LPARAM lParam)
{
    (void)lParam;
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (pid == (DWORD)lParam && IsWindowVisible(hwnd)) {
        WCHAR title[256] = {0};
        GetWindowTextW(hwnd, title, 256);
        if (title[0] != L'\0' && GetPropW(hwnd, MAIN_WINDOW_READY_PROP_NAME)) {
            g_mainHwnd = hwnd;
            g_found = TRUE;
            return FALSE;
        }
    }
    return TRUE;
}

static BOOL FindMainWindow(void)
{
    if (!g_pid) return FALSE;
    g_found = FALSE;
    g_mainHwnd = NULL;
    EnumWindows(EnumWindowProc, (LPARAM)g_pid);
    return g_found;
}

static BOOL IsAppAlreadyRunning(void)
{
    HANDLE existing = OpenMutexW(SYNCHRONIZE, FALSE, L"Global\\RealESRGAN_GUI_SingleInstance");
    if (!existing) return FALSE;
    CloseHandle(existing);
    return TRUE;
}

static HWND FindExistingAppWindow(void)
{
    HWND existing = FindWindowW(L"HwndWrapper[RealESRGAN_GUI*", NULL);
    if (!existing) {
        existing = FindWindowW(NULL, L"Real-ESRGAN GUI");
    }
    return existing;
}

static void ResolveRunningInstanceLanguage(HWND hwnd)
{
    if (!hwnd || !IsWindow(hwnd)) return;

    UINT message = RegisterWindowMessageW(QUERY_LANGUAGE_MESSAGE_NAME);
    if (!message) return;

    DWORD_PTR result = 0;
    LRESULT sent = SendMessageTimeoutW(
        hwnd,
        message,
        0,
        0,
        SMTO_ABORTIFHUNG,
        200,
        &result);

    if (!sent) return;

    if (result == QUERY_LANGUAGE_ZH) {
        g_zh = TRUE;
    } else if (result == QUERY_LANGUAGE_EN) {
        g_zh = FALSE;
    }
}

static LPCWSTR NoticeFontFace(void)
{
    return g_zh ? L"Microsoft YaHei UI" : L"Segoe UI";
}

static void ActivateNoticeWindow(HWND hwnd)
{
    if (!hwnd || !IsWindow(hwnd)) return;

    ShowWindow(hwnd, SW_SHOWNORMAL);
    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
        SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    SetForegroundWindow(hwnd);
}

static void ShowAlreadyRunningNotice(HINSTANCE hInst, HWND owner)
{
    ResolveRunningInstanceLanguage(owner);

    BOOL ownsNoticeMutex = FALSE;
    HANDLE noticeMutex = CreateMutexW(NULL, TRUE, NOTICE_MUTEX_NAME);
    if (noticeMutex) {
        if (GetLastError() == ERROR_ALREADY_EXISTS) {
            HWND notice = NULL;
            for (int i = 0; i < 10 && !notice; i++) {
                notice = FindWindowW(L"RESG_Notice", NULL);
                if (!notice) Sleep(25);
            }

            if (notice) {
                ActivateNoticeWindow(notice);
            }

            CloseHandle(noticeMutex);
            return;
        }

        ownsNoticeMutex = TRUE;
    }

    WNDCLASSEXW wc = { sizeof(wc) };
    wc.lpfnWndProc   = NoticeWndProc;
    wc.hInstance     = hInst;
    wc.lpszClassName = L"RESG_Notice";
    wc.hCursor       = LoadCursorW(NULL, (LPCWSTR)IDC_ARROW);
    wc.hIcon         = LoadIconW(hInst, MAKEINTRESOURCEW(IDI_APP_ICON));
    wc.hIconSm       = LoadIconW(hInst, MAKEINTRESOURCEW(IDI_APP_ICON));
    RegisterClassExW(&wc);

    int noticeW = S(NOTICE_W);
    int noticeH = S(NOTICE_H);

    POINT origin = {0, 0};
    HMONITOR monitor = owner
        ? MonitorFromWindow(owner, MONITOR_DEFAULTTONEAREST)
        : MonitorFromPoint(origin, MONITOR_DEFAULTTOPRIMARY);
    MONITORINFO mi = { sizeof(mi) };
    if (!GetMonitorInfoW(monitor, &mi)) {
        mi.rcWork.left = 0;
        mi.rcWork.top = 0;
        mi.rcWork.right = GetSystemMetrics(SM_CXSCREEN);
        mi.rcWork.bottom = GetSystemMetrics(SM_CYSCREEN);
    }

    RECT target = mi.rcWork;
    if (owner && IsWindow(owner)) {
        GetWindowRect(owner, &target);
    }

    int x = target.left + ((target.right - target.left) - noticeW) / 2;
    int y = target.top + ((target.bottom - target.top) - noticeH) / 2;
    if (x < mi.rcWork.left) x = mi.rcWork.left;
    if (y < mi.rcWork.top) y = mi.rcWork.top;
    if (x + noticeW > mi.rcWork.right) x = mi.rcWork.right - noticeW;
    if (y + noticeH > mi.rcWork.bottom) y = mi.rcWork.bottom - noticeH;

    g_noticeDone = FALSE;
    g_noticeButtonHot = FALSE;
    SetRectEmpty(&g_noticeButtonRect);

    HWND hwnd = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
        L"RESG_Notice",
        g_zh ? L"提示" : L"Notice",
        WS_POPUP,
        x,
        y,
        noticeW,
        noticeH,
        NULL,
        NULL,
        hInst,
        NULL);

    if (!hwnd) {
        if (ownsNoticeMutex) {
            ReleaseMutex(noticeMutex);
        }
        if (noticeMutex) {
            CloseHandle(noticeMutex);
        }
        return;
    }

    int useDark = g_dark ? 1 : 0;
    DwmSetWindowAttribute(hwnd, 20, &useDark, sizeof(int));
    DWM_WINDOW_CORNER_PREFERENCE corner = 2;
    DwmSetWindowAttribute(hwnd, 33, &corner, sizeof(corner));

    ShowWindow(hwnd, SW_SHOW);
    UpdateWindow(hwnd);
    SetForegroundWindow(hwnd);
    SetFocus(hwnd);

    MSG msg;
    while (!g_noticeDone && GetMessageW(&msg, NULL, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    if (ownsNoticeMutex) {
        ReleaseMutex(noticeMutex);
    }
    if (noticeMutex) {
        CloseHandle(noticeMutex);
    }
}

static void ActivateAppWindow(HWND hwnd)
{
    if (!hwnd || !IsWindow(hwnd)) return;

    if (IsIconic(hwnd)) {
        ShowWindow(hwnd, SW_RESTORE);
    } else {
        ShowWindow(hwnd, SW_SHOWNORMAL);
    }

    const UINT flags = SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW;
    SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, flags);

    DWORD currentThread = GetCurrentThreadId();
    DWORD targetThread = GetWindowThreadProcessId(hwnd, NULL);
    HWND foreground = GetForegroundWindow();
    DWORD foregroundThread = foreground ? GetWindowThreadProcessId(foreground, NULL) : 0;

    BOOL attachedTarget = FALSE;
    BOOL attachedForeground = FALSE;
    if (targetThread && targetThread != currentThread) {
        attachedTarget = AttachThreadInput(currentThread, targetThread, TRUE);
    }
    if (foregroundThread &&
        foregroundThread != currentThread &&
        foregroundThread != targetThread) {
        attachedForeground = AttachThreadInput(currentThread, foregroundThread, TRUE);
    }

    BringWindowToTop(hwnd);
    SetForegroundWindow(hwnd);
    SetFocus(hwnd);

    if (attachedForeground) {
        AttachThreadInput(currentThread, foregroundThread, FALSE);
    }
    if (attachedTarget) {
        AttachThreadInput(currentThread, targetThread, FALSE);
    }

    // If Windows still declines foreground activation, briefly pulse topmost so
    // the newly launched UI does not remain hidden behind another application.
    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, flags);
    SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, flags);
    SetForegroundWindow(hwnd);
}
