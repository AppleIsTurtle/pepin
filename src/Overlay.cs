using static Pepin.Native;

namespace Pepin;

/// <summary>
/// Petite fenêtre layered qui affiche une <see cref="PixelCanvas"/> en gros pixels, sans jamais attraper la souris
/// (décor des mini-jeux, objets du monde). Placée juste derrière la fenêtre de la tortue pour qu'elle passe devant.
/// </summary>
public sealed unsafe class Overlay
{
    nint hwnd, memDc, dib, oldBmp;
    uint* bits;
    int bmpW, bmpH, curScale;
    uint[]? shown;
    bool visible;
    int lastX = int.MinValue, lastY;

    public bool Created => hwnd != 0;
    public bool Visible => visible;

    public void Create(nint inst)
    {
        hwnd = CreateWindowExW(WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
                               App.WindowClass, "Pépin (décor)", WS_POPUP, 0, 0, 1, 1, 0, 0, inst, 0);
        memDc = CreateCompatibleDC(App.ScreenDc);
    }

    public void Destroy()
    {
        if (dib != 0) { SelectObject(memDc, oldBmp); DeleteObject(dib); dib = 0; }
        if (memDc != 0) { DeleteDC(memDc); memDc = 0; }
        if (hwnd != 0) { DestroyWindow(hwnd); hwnd = 0; }
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
