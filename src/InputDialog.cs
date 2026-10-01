using System.Runtime.InteropServices;
using static Pepin.Native;

namespace Pepin;

public static unsafe partial class Native
{
    [LibraryImport("user32.dll")] public static partial int IsDialogMessageW(nint dlg, MSG* msg);
    [LibraryImport("user32.dll")] public static partial nint SendMessageW(nint hwnd, uint msg, nint w, nint l);
    [LibraryImport("user32.dll")] public static partial nint SetFocus(nint hwnd);
}

/// <summary>Petite fenêtre de saisie modale (petit mot, nom de la tortue), en Win32 pur.</summary>
public static unsafe class InputDialog
{
    const uint WS_SYSMENU = 0x80000, WS_VISIBLE = 0x10000000, WS_CHILD = 0x40000000, WS_TABSTOP = 0x10000;
    const uint ES_AUTOHSCROLL = 0x80, BS_DEFPUSHBUTTON = 1, WS_EX_DLGMODALFRAME = 1, WS_EX_CLIENTEDGE = 0x200;
    const uint WM_COMMAND = 0x111, WM_CLOSE = 0x10, WM_SETFONT = 0x30, EM_LIMITTEXT = 0xC5, EM_SETSEL = 0xB1;
    const int IDOK = 1, IDCANCEL = 2, IDEDIT = 100;
    const string Class = "PepinSaisie";

    static bool registered, done;
    static string? result;
    static nint edit;

    /// <summary>Renvoie le texte saisi, ou null si annulé.</summary>
    public static string? Ask(string title, string prompt, string initial, int maxLen, string okText)
    {
        var inst = GetModuleHandleW(0);
        if (!registered)
        {
            fixed (char* cls = Class)
            {
                var wc = new WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&Proc,
                    hInstance = inst,
                    hCursor = LoadCursorW(0, 32512),          // flèche
                    hbrBackground = 6,                        // COLOR_WINDOW + 1
                    lpszClassName = (nint)cls,
                };
                RegisterClassExW(&wc);
            }
            registered = true;
        }

        POINT c;
        GetCursorPos(&c);
        var mon = MonitorFromPoint(c, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = new() { cbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(mon, &mi);
        // taille en « pixels 96 dpi » puis mise à l'échelle de l'écran
        double k = App.DpiScale;
        int S(double v) => (int)Math.Round(v * k);
        int w = S(400), h = S(170);
        int x = Math.Clamp(c.X - w / 2, mi.rcWork.Left, mi.rcWork.Right - w);
        int y = Math.Clamp(c.Y - h - S(20), mi.rcWork.Top, mi.rcWork.Bottom - h);

        done = false;
        result = null;
        nint dlg = CreateWindowExW(WS_EX_DLGMODALFRAME | WS_EX_TOPMOST, Class, title, WS_POPUP | WS_CAPTION | WS_SYSMENU,
                                   x, y, w, h, 0, 0, inst, 0);
        nint font = CreateFontW(-S(15), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        nint label = CreateWindowExW(0, "STATIC", prompt, WS_CHILD | WS_VISIBLE, S(14), S(12), S(360), S(22), dlg, 0, inst, 0);
        edit = CreateWindowExW(WS_EX_CLIENTEDGE, "EDIT", initial, WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL,
                               S(14), S(40), S(360), S(28), dlg, IDEDIT, inst, 0);
        nint ok = CreateWindowExW(0, "BUTTON", okText, WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON,
                                  S(186), S(82), S(92), S(30), dlg, IDOK, inst, 0);
        nint cancel = CreateWindowExW(0, "BUTTON", "Annuler", WS_CHILD | WS_VISIBLE | WS_TABSTOP,
                                      S(284), S(82), S(90), S(30), dlg, IDCANCEL, inst, 0);
        foreach (var ctl in (ReadOnlySpan<nint>)[label, edit, ok, cancel]) SendMessageW(ctl, WM_SETFONT, font, 1);
        SendMessageW(edit, EM_LIMITTEXT, maxLen, 0);
        SendMessageW(edit, EM_SETSEL, 0, -1);

        ShowWindow(dlg, 5);                                   // SW_SHOW
        SetForegroundWindow(dlg);
        SetFocus(edit);

        MSG msg;
        while (!done)
        {
            int r = GetMessageW(&msg, 0, 0, 0);
            if (r <= 0) { PostQuitMessage(0); break; }        // on quitte pendant la saisie : on relaie
            if (IsDialogMessageW(dlg, &msg) != 0) continue;   // Entrée = OK, Échap = Annuler, Tab
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        DestroyWindow(dlg);
        DeleteObject(font);
        return result;
    }

    [UnmanagedCallersOnly]
    static nint Proc(nint h, uint m, nint w, nint l)
    {
        switch (m)
        {
            case WM_COMMAND:
                int id = LoWord(w);
                if (id == IDOK)
                {
                    char* buf = stackalloc char[256];
                    int n = GetWindowTextW(edit, buf, 256);
                    result = new string(buf, 0, n).Trim();
                    done = true;
                    return 0;
                }
                if (id == IDCANCEL) { result = null; done = true; return 0; }
                break;
            case WM_CLOSE:
                result = null;
                done = true;
                return 0;
        }
        return DefWindowProcW(h, m, w, l);
    }
}
