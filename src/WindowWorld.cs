using System.Runtime.InteropServices;
using static Pepin.Native;

namespace Pepin;

/// <summary>Une fenêtre "réelle" du bureau. R = cadre visible, en pixels physiques.</summary>
public struct WinInfo { public nint Hwnd; public RECT R; public bool Maximized, Fullscreen; public uint Pid; }

/// <summary>Segment libre du bord supérieur d'une fenêtre, où la tortue peut se poser : pixels [X0, X1) à la hauteur Y.</summary>
public struct PerchSpot { public nint Hwnd; public int X0, X1, Y; }

/// <summary>
/// Carte des fenêtres du bureau, de la plus haute à la plus basse (ordre Z).
/// Pensé pour être rafraîchi ~2 fois par seconde : aucune allocation en régime établi.
/// </summary>
public sealed unsafe class WindowWorld
{
    public readonly List<WinInfo> Windows = new(64);

    // EnumWindows est synchrone sur le thread appelant : le callback statique écrit dans l'instance courante
    static WindowWorld? current;
    nint[] found = new nint[256];
    int foundCount;

    readonly List<PerchSpot> perches = new(32);
    int[] segA = new int[32], segB = new int[32];   // intervalles [a, b) à plat : a0, b0, a1, b1…

    /// <summary>Énumère les fenêtres ; celles de <paramref name="own"/> (le programme lui-même) sont ignorées.</summary>
    public void Refresh(ReadOnlySpan<nint> own)
    {
        foundCount = 0;
        current = this;
        EnumWindows(&Collect, 0);
        current = null;

        Windows.Clear();
        for (int i = 0; i < foundCount; i++)
        {
            nint h = found[i];
            if (own.Contains(h)) continue;
            if (TryDescribe(h, out var w)) Windows.Add(w);
        }
    }

    [UnmanagedCallersOnly]
    static int Collect(nint h, nint _)
    {
        var w = current!;
        if (IsWindowVisible(h) == 0) return 1;
        if (w.foundCount == w.found.Length) Array.Resize(ref w.found, w.found.Length * 2);
        w.found[w.foundCount++] = h;
        return 1;
    }

    static bool TryDescribe(nint h, out WinInfo w)
    {
        w = default;
        if (IsIconic(h) != 0) return false;
        uint ex = (uint)GetWindowLongPtrW(h, GWL_EXSTYLE);
        if ((ex & (WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)) != 0) return false;
        if (IsShellWindow(h) || IsCloaked(h)) return false;
        if (!VisibleBounds(h, out var r)) return false;
        if (r.Right - r.Left < 160 || r.Bottom - r.Top < 100) return false;

        bool fullscreen = CoversMonitor(h, r);
        uint style = (uint)GetWindowLongPtrW(h, GWL_STYLE);
        bool framed = (style & WS_CAPTION) == WS_CAPTION || (style & WS_THICKFRAME) != 0;
        // exception : le plein écran sans cadre (vidéo, jeu, F11) compte, sauf surcouche "traversable" par la souris
        if (!framed && !(fullscreen && (ex & WS_EX_TRANSPARENT) == 0)) return false;

        uint pid;
        GetWindowThreadProcessId(h, &pid);
        w = new WinInfo { Hwnd = h, R = r, Maximized = IsZoomed(h) != 0, Fullscreen = fullscreen, Pid = pid };
        return true;
    }

    static bool IsShellWindow(nint h)
    {
        char* buf = stackalloc char[32];
        var cls = new ReadOnlySpan<char>(buf, Math.Max(0, GetClassNameW(h, buf, 32)));
        return cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    /// <summary>Requête directe pour une fenêtre ; false si elle a disparu, est invisible, réduite ou masquée par DWM.</summary>
    public bool TryGetRect(nint hwnd, out RECT r, out bool maximized, out bool fullscreen)
    {
        r = default;
        maximized = fullscreen = false;
        if (hwnd == 0 || IsWindow(hwnd) == 0 || IsWindowVisible(hwnd) == 0 || IsIconic(hwnd) != 0 || IsCloaked(hwnd)) return false;
        if (!VisibleBounds(hwnd, out r)) return false;
        maximized = IsZoomed(hwnd) != 0;
        fullscreen = CoversMonitor(hwnd, r);
        return true;
    }

    /// <summary>
    /// Vrai si (x, y) est recouvert par une fenêtre située au-dessus de <paramref name="below"/> dans la dernière liste.
    /// Si <paramref name="below"/> n'y figure pas (0, fenêtre disparue), toutes les fenêtres comptent.
    /// </summary>
    public bool IsCovered(int x, int y, nint below)
    {
        foreach (ref readonly var w in CollectionsMarshal.AsSpan(Windows))
        {
            if (w.Hwnd == below) return false;
            if (Contains(w.R, x, y)) return true;
        }
        return false;
    }

    /// <summary>Fenêtre réelle la plus haute contenant le point (0 si aucune).</summary>
    public nint TopmostAt(int x, int y)
    {
        foreach (ref readonly var w in CollectionsMarshal.AsSpan(Windows))
            if (Contains(w.R, x, y)) return w.Hwnd;
        return 0;
    }

    static bool Contains(in RECT r, int x, int y) => x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;

    /// <summary>
    /// Segments visibles des bords supérieurs (fenêtres ni maximisées ni plein écran) dans la zone de travail.
    /// La liste renvoyée est réutilisée : elle est vidée au prochain appel.
    /// </summary>
    public List<PerchSpot> Perches(RECT work, int minWidth)
    {
        perches.Clear();
        var ws = CollectionsMarshal.AsSpan(Windows);
        for (int i = 0; i < ws.Length; i++)
        {
            ref readonly var w = ref ws[i];
            if (w.Maximized || w.Fullscreen) continue;
            int y = w.R.Top;
            if (y < work.Top + 30 || y > work.Bottom - 40) continue;
            int x0 = Math.Max(w.R.Left + 8, work.Left), x1 = Math.Min(w.R.Right - 8, work.Right);
            if (x1 - x0 < minWidth) continue;

            // la tortue se tient juste au-dessus du bord : une fenêtre plus haute gêne si elle occupe la ligne y-1 ou y
            int n = 1;
            segA[0] = x0; segA[1] = x1;
            for (int j = 0; j < i && n > 0; j++)
            {
                ref readonly var o = ref ws[j].R;
                if (o.Top > y || o.Bottom < y) continue;          // Top ≤ y et Bottom > y-1
                if (o.Right <= x0 || o.Left >= x1) continue;
                n = Subtract(n, o.Left, o.Right);
            }
            for (int k = 0; k < n; k++)
            {
                int a = segA[2 * k], b = segA[2 * k + 1];
                if (b - a >= minWidth) perches.Add(new PerchSpot { Hwnd = w.Hwnd, X0 = a, X1 = b, Y = y });
            }
        }
        return perches;
    }

    /// <summary>Retire [l, r) des n intervalles de segA ; renvoie le nouveau nombre d'intervalles.</summary>
    int Subtract(int n, int l, int r)
    {
        if (segB.Length < 2 * (n + 1)) segB = new int[4 * (n + 1)];
        int m = 0;
        for (int k = 0; k < n; k++)
        {
            int a = segA[2 * k], b = segA[2 * k + 1];
            if (r <= a || l >= b) { segB[m++] = a; segB[m++] = b; continue; }
            if (l > a) { segB[m++] = a; segB[m++] = l; }
            if (r < b) { segB[m++] = r; segB[m++] = b; }
        }
        (segA, segB) = (segB, segA);
        return m / 2;
    }

    /// <summary>Déplace une fenêtre ; refuse si elle est maximisée, plein écran, réduite, invisible ou masquée.</summary>
    public static bool MoveBy(nint hwnd, int dx, int dy)
    {
        if (IsWindow(hwnd) == 0 || IsWindowVisible(hwnd) == 0 || IsIconic(hwnd) != 0 || IsZoomed(hwnd) != 0 || IsCloaked(hwnd)) return false;
        if (VisibleBounds(hwnd, out var vb) && CoversMonitor(hwnd, vb)) return false;
        // position issue de GetWindowRect (et non du cadre DWM) : sinon la fenêtre glisserait de l'épaisseur des bordures invisibles
        RECT r;
        if (GetWindowRect(hwnd, &r) == 0) return false;
        return SetWindowPos(hwnd, 0, r.Left + dx, r.Top + dy, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS) != 0;
    }
}
