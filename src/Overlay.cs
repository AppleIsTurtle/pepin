using static Pepin.Native;

namespace Pepin;

/// <summary>
/// Petite fenêtre layered qui affiche une <see cref="PixelCanvas"/> en gros pixels (décor des mini-jeux, objets du
/// monde, cartes). Par défaut elle ne prend jamais la souris ; en mode <c>clickable</c> seuls ses pixels opaques
/// reçoivent le clic (les transparents laissent passer). Placée juste derrière la tortue pour qu'elle passe devant.
/// </summary>
public sealed unsafe class Overlay
{
    static readonly Dictionary<nint, Overlay> byHwnd = [];
    public static Overlay? From(nint hwnd) => byHwnd.GetValueOrDefault(hwnd);

    readonly bool clickable, activatable;
    public Action? Click;                    // clic gauche sur un pixel opaque (mode clickable)
    public Action<uint, int, int>? Mouse;    // message souris + position en pixels de la toile (remplace Click)
    nint hwnd, memDc, dib, oldBmp;
    uint* bits;
    int bmpW, bmpH, curScale;
    uint[]? shown;
    bool visible;
    int lastX = int.MinValue, lastY;

    /// <param name="activatable">Peut prendre le focus (menu : se ferme au clic dehors, Échap).</param>
    public Overlay(bool clickable = false, bool activatable = false)
    {
        this.clickable = clickable || activatable;
        this.activatable = activatable;
    }

    public bool Created => hwnd != 0;
    public bool Activatable => activatable;
    public nint Hwnd => hwnd;
    public int X => lastX;
    public int Y => lastY;
    public int PixelScale => curScale;
    public bool Visible => visible;

    public void Create(nint inst)
    {
        uint ex = WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | (activatable ? 0 : WS_EX_NOACTIVATE) | (clickable ? 0 : WS_EX_TRANSPARENT);
        hwnd = CreateWindowExW(ex, App.WindowClass, "Pépin (décor)", WS_POPUP, 0, 0, 1, 1, 0, 0, inst, 0);
        byHwnd[hwnd] = this;
        memDc = CreateCompatibleDC(App.ScreenDc);
    }

    public void Destroy()
    {
        if (dib != 0) { SelectObject(memDc, oldBmp); DeleteObject(dib); dib = 0; }
        if (memDc != 0) { DeleteDC(memDc); memDc = 0; }
        if (hwnd != 0) { byHwnd.Remove(hwnd); DestroyWindow(hwnd); hwnd = 0; }
    }

    /// <summary>Message souris reçu par la fenêtre (routé par App). Vrai si traité.</summary>
    public bool HandleMouse(uint m, nint l)
    {
        if (Mouse is not null && curScale > 0)
        {
            Mouse(m, LoWord(l) / curScale, HiWord(l) / curScale);
            return true;
        }
        if (m == WM_LBUTTONDOWN) { Click?.Invoke(); return true; }
        return m is WM_LBUTTONUP or WM_MOUSEMOVE or WM_CAPTURECHANGED;
    }

    public void Hide()
    {
        if (!visible) return;
        visible = false;
        ShowWindow(hwnd, SW_HIDE);
    }

    /// <summary>Se range juste sous la fenêtre `above` (la tortue passe devant le décor).</summary>
    public void PlaceBelow(nint above)
    {
        if (hwnd == 0 || !visible) return;
        SetWindowPos(hwnd, above, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Affiche `c` agrandie `scale` fois, coin haut-gauche en (x, y) à l'écran. Ne renvoie l'image que si elle change.</summary>
    public void Present(PixelCanvas c, int scale, int x, int y, nint below = 0)
    {
        if (hwnd == 0) return;
        if (dib == 0 || scale != curScale || bmpW != c.W * scale || bmpH != c.H * scale) Resize(c, scale);

        if (shown is null || !c.Px.AsSpan().SequenceEqual(shown))
        {
            c.Px.CopyTo(shown!, 0);
            Blit(c);
            POINT dst = new() { X = x, Y = y }, src = default;
            SIZE size = new() { cx = bmpW, cy = bmpH };
            BLENDFUNCTION bf = new() { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(hwnd, App.ScreenDc, &dst, &size, memDc, &src, 0, &bf, ULW_ALPHA);
            lastX = x; lastY = y;
        }
        else if (x != lastX || y != lastY)
        {
            SetWindowPos(hwnd, 0, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            lastX = x; lastY = y;
        }

        if (!visible)
        {
            visible = true;
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            if (below != 0) PlaceBelow(below);
        }
    }

    void Resize(PixelCanvas c, int scale)
    {
        curScale = scale;
        bmpW = c.W * scale;
        bmpH = c.H * scale;
        if (dib != 0) { SelectObject(memDc, oldBmp); DeleteObject(dib); }
        var bi = new BITMAPINFOHEADER { biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = bmpW, biHeight = -bmpH, biPlanes = 1, biBitCount = 32 };
        void* b;
        dib = CreateDIBSection(memDc, &bi, 0, &b, 0, 0);
        bits = (uint*)b;
        oldBmp = SelectObject(memDc, dib);
        shown = new uint[c.W * c.H];
        lastX = int.MinValue;
    }

    void Blit(PixelCanvas c)
    {
        uint* dst = bits;
        for (int y = 0; y < c.H; y++)
        {
            uint* row = dst;
            for (int x = 0; x < c.W; x++)
            {
                uint px = c.Px[y * c.W + x];
                for (int k = 0; k < curScale; k++) *dst++ = px;
            }
            for (int r = 1; r < curScale; r++)
            {
                Buffer.MemoryCopy(row, dst, bmpW * 4, bmpW * 4);
                dst += bmpW;
            }
        }
    }
}
