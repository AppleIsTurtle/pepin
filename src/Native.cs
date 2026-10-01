using System.Runtime.InteropServices;

namespace Pepin;

// Structs Win32 blittables (passées par pointeur : aucun marshalling, compatible NativeAOT).

[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
[StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
public struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public POINT pt; public uint lPrivate; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

[StructLayout(LayoutKind.Sequential)]
public struct BITMAPINFOHEADER
{
    public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
    public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
public struct WNDCLASSEXW
{
    public uint cbSize, style; public nint lpfnWndProc; public int cbClsExtra, cbWndExtra;
    public nint hInstance, hIcon, hCursor, hbrBackground, lpszMenuName, lpszClassName, hIconSm;
}

[StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
[StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public uint cbSize, dwTime; }
[StructLayout(LayoutKind.Sequential)] public struct ICONINFO { public int fIcon, xHotspot, yHotspot; public nint hbmMask, hbmColor; }

[StructLayout(LayoutKind.Sequential)]
public unsafe struct NOTIFYICONDATAW
{
    public uint cbSize; public nint hWnd; public uint uID, uFlags, uCallbackMessage; public nint hIcon;
    public fixed char szTip[128]; public uint dwState, dwStateMask; public fixed char szInfo[256];
    public uint uVersion; public fixed char szInfoTitle[64]; public uint dwInfoFlags; public Guid guidItem; public nint hBalloonIcon;
}

public static unsafe partial class Native
{
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_EX_LAYERED = 0x80000, WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    public const uint WM_DESTROY = 0x2, WM_QUERYENDSESSION = 0x11, WM_ENDSESSION = 0x16, WM_SETCURSOR = 0x20, WM_MOUSEACTIVATE = 0x21,
        WM_TIMER = 0x113, WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_RBUTTONUP = 0x205,
        WM_CAPTURECHANGED = 0x215, WM_APP = 0x8000, WM_ACTIVATE = 0x6, WM_KEYDOWN = 0x100, WM_RBUTTONDOWN = 0x204;
    public const int VK_ESCAPE = 0x1B, MA_ACTIVATE = 1, SW_SHOW = 5;
    public const int MA_NOACTIVATE = 3, SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
    public const uint ULW_ALPHA = 2;
    public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;
    public static readonly nint HWND_TOPMOST = -1;
    public const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    public const nint IDC_HAND = 32649;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    [LibraryImport("user32.dll")] public static partial ushort RegisterClassExW(WNDCLASSEXW* wc);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [LibraryImport("user32.dll")] public static partial nint DefWindowProcW(nint hwnd, uint msg, nint w, nint l);
    [LibraryImport("user32.dll")] public static partial int GetMessageW(MSG* msg, nint hwnd, uint min, uint max);
    [LibraryImport("user32.dll")] public static partial int TranslateMessage(MSG* msg);
    [LibraryImport("user32.dll")] public static partial nint DispatchMessageW(MSG* msg);
    [LibraryImport("user32.dll")] public static partial void PostQuitMessage(int code);
    [LibraryImport("user32.dll")] public static partial int DestroyWindow(nint hwnd);
    [LibraryImport("user32.dll")]
    public static partial int UpdateLayeredWindow(nint hwnd, nint hdcDst, POINT* pptDst, SIZE* psize, nint hdcSrc, POINT* pptSrc, uint key, BLENDFUNCTION* blend, uint flags);
    [LibraryImport("user32.dll")] public static partial nint GetDC(nint hwnd);
    [LibraryImport("user32.dll")] public static partial int ReleaseDC(nint hwnd, nint hdc);
    [LibraryImport("user32.dll")] public static partial nuint SetTimer(nint hwnd, nuint id, uint ms, nint proc);
    [LibraryImport("user32.dll")] public static partial int KillTimer(nint hwnd, nuint id);
    [LibraryImport("user32.dll")] public static partial int GetCursorPos(POINT* pt);
    [LibraryImport("user32.dll")] public static partial nint SetCapture(nint hwnd);
    [LibraryImport("user32.dll")] public static partial int ReleaseCapture();
    [LibraryImport("user32.dll")] public static partial int GetLastInputInfo(LASTINPUTINFO* lii);
    [LibraryImport("user32.dll")] public static partial int SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [LibraryImport("user32.dll")] public static partial int ShowWindow(nint hwnd, int cmd);
    [LibraryImport("user32.dll")] public static partial int SetForegroundWindow(nint hwnd);
    [LibraryImport("user32.dll")] public static partial nint CreateIconIndirect(ICONINFO* ii);
    [LibraryImport("user32.dll")] public static partial int DestroyIcon(nint icon);
    [LibraryImport("user32.dll")] public static partial nint LoadCursorW(nint inst, nint id);
    [LibraryImport("user32.dll")] public static partial nint SetCursor(nint cursor);
    [LibraryImport("user32.dll")] public static partial nint MonitorFromPoint(POINT pt, uint flags);
    [LibraryImport("user32.dll")] public static partial int GetMonitorInfoW(nint mon, MONITORINFO* mi);
    [LibraryImport("user32.dll")] public static partial int SetProcessDpiAwarenessContext(nint ctx);
    [LibraryImport("user32.dll")] public static partial uint GetDpiForWindow(nint hwnd);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessageW(string name);

    [LibraryImport("gdi32.dll")] public static partial nint CreateCompatibleDC(nint hdc);
    [LibraryImport("gdi32.dll")] public static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* bmi, uint usage, void** bits, nint section, uint offset);
    [LibraryImport("gdi32.dll")] public static partial nint SelectObject(nint hdc, nint obj);
    [LibraryImport("gdi32.dll")] public static partial int DeleteObject(nint obj);
    [LibraryImport("gdi32.dll")] public static partial int DeleteDC(nint hdc);
    [LibraryImport("gdi32.dll")] public static partial nint CreateBitmap(int w, int h, uint planes, uint bpp, void* bits);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateFontW(int h, int w, int esc, int orient, int weight, uint italic, uint underline, uint strike,
                                           uint charset, uint outPrec, uint clipPrec, uint quality, uint pitch, string face);
    [LibraryImport("gdi32.dll")] public static partial int SetBkMode(nint hdc, int mode);
    [LibraryImport("gdi32.dll")] public static partial uint SetTextColor(nint hdc, uint color);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int DrawTextW(nint hdc, string text, int len, RECT* rect, uint format);
    public const uint DT_CENTER = 1, DT_VCENTER = 4, DT_SINGLELINE = 0x20, DT_WORDBREAK = 0x10, DT_CALCRECT = 0x400, DT_NOPREFIX = 0x800;

    [LibraryImport("shell32.dll")] public static partial int Shell_NotifyIconW(uint msg, NOTIFYICONDATAW* data);

    [LibraryImport("kernel32.dll")] public static partial nint GetModuleHandleW(nint name);
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial nint CreateMutexW(nint attrs, int initialOwner, string name);

    public static int LoWord(nint v) => (short)((long)v & 0xFFFF);
    public static int HiWord(nint v) => (short)(((long)v >> 16) & 0xFFFF);
}
