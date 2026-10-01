using System.Runtime.InteropServices;

namespace Pepin;

// Entrée d'un instantané Toolhelp des processus (blittable : tableau de caractères fixe).
[StructLayout(LayoutKind.Sequential)]
public unsafe struct PROCESSENTRY32W
{
    public uint dwSize, cntUsage, th32ProcessID;
    public nuint th32DefaultHeapID;
    public uint th32ModuleID, cntThreads, th32ParentProcessID;
    public int pcPriClassBase;
    public uint dwFlags;
    public fixed char szExeFile[260];
}

/// <summary>Appels Win32 pour observer les autres fenêtres et processus (lecture seule, sauf SetWindowPos).</summary>
public static unsafe partial class Native
{
    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const uint WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000;
    public const uint WS_EX_TRANSPARENT = 0x20;
    public const uint SWP_ASYNCWINDOWPOS = 0x4000;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint TH32CS_SNAPPROCESS = 2;
    public static readonly nint INVALID_HANDLE_VALUE = -1;

    [LibraryImport("user32.dll")] public static partial int EnumWindows(delegate* unmanaged<nint, nint, int> proc, nint lParam);
    [LibraryImport("user32.dll")] public static partial int EnumChildWindows(nint parent, delegate* unmanaged<nint, nint, int> proc, nint lParam);
    [LibraryImport("user32.dll")] public static partial int IsWindowVisible(nint hwnd);
    [LibraryImport("user32.dll")] public static partial int IsIconic(nint hwnd);
    [LibraryImport("user32.dll")] public static partial int IsZoomed(nint hwnd);
    [LibraryImport("user32.dll")] public static partial int IsWindow(nint hwnd);
    [LibraryImport("user32.dll")] public static partial nint GetWindowLongPtrW(nint hwnd, int index);
    [LibraryImport("user32.dll")] public static partial int GetWindowRect(nint hwnd, RECT* r);
    [LibraryImport("user32.dll")] public static partial uint GetWindowThreadProcessId(nint hwnd, uint* pid);
    [LibraryImport("user32.dll")] public static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll")] public static partial int GetWindowTextW(nint hwnd, char* buf, int max);
    [LibraryImport("user32.dll")] public static partial int GetClassNameW(nint hwnd, char* buf, int max);
    [LibraryImport("user32.dll")] public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("dwmapi.dll")] public static partial int DwmGetWindowAttribute(nint hwnd, uint attr, void* value, uint size);

    [LibraryImport("kernel32.dll")] public static partial nint OpenProcess(uint access, int inherit, uint pid);
    [LibraryImport("kernel32.dll")] public static partial int QueryFullProcessImageNameW(nint process, uint flags, char* buf, uint* size);
    [LibraryImport("kernel32.dll")] public static partial int GetProcessTimes(nint process, long* creation, long* exit, long* kernel, long* user);
    [LibraryImport("kernel32.dll")] public static partial int CloseHandle(nint h);
    [LibraryImport("kernel32.dll")] public static partial void GetSystemTimeAsFileTime(long* ft);
    [LibraryImport("kernel32.dll")] public static partial nint CreateToolhelp32Snapshot(uint flags, uint pid);
    [LibraryImport("kernel32.dll")] public static partial int Process32FirstW(nint snap, PROCESSENTRY32W* pe);
    [LibraryImport("kernel32.dll")] public static partial int Process32NextW(nint snap, PROCESSENTRY32W* pe);

    /// <summary>Cadre visible d'une fenêtre (sans les bordures invisibles de redimensionnement), repli sur GetWindowRect.</summary>
    public static bool VisibleBounds(nint hwnd, out RECT r)
    {
        RECT b;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &b, (uint)sizeof(RECT)) == 0) { r = b; return true; }
        if (GetWindowRect(hwnd, &b) != 0) { r = b; return true; }
        r = default;
        return false;
    }

    /// <summary>Vrai si DWM cache la fenêtre (appli UWP suspendue, autre bureau virtuel…).</summary>
    public static bool IsCloaked(nint hwnd)
    {
        int c = 0;
        return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &c, 4) == 0 && c != 0;
    }

    /// <summary>Vrai si le rectangle couvre tout le moniteur de la fenêtre.</summary>
    public static bool CoversMonitor(nint hwnd, in RECT r)
    {
        MONITORINFO mi = new() { cbSize = (uint)sizeof(MONITORINFO) };
        if (GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), &mi) == 0) return false;
        return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
    }
}
