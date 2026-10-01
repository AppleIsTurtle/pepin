using static Pepin.Native;

namespace Pepin;

/// <summary>
/// Petite bulle de texte (nom d'un visiteur, petit mot, « en visite chez… »).
/// Fenêtre layered qui laisse passer les clics ; le cadre est dessiné en « gros pixels » comme la tortue.
/// </summary>
public sealed unsafe class Label
{
    nint hwnd, memDc, dib, oldBmp, font;
    uint* bits;
    int w, h, fontScale;
    string text = "";
    bool visible;
    int lastX = int.MinValue, lastY;

    public void Create(nint inst)
    {
        hwnd = CreateWindowExW(WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
                               App.WindowClass, "Pépin (bulle)", WS_POPUP, 0, 0, 1, 1, 0, 0, inst, 0);
        memDc = CreateCompatibleDC(App.ScreenDc);
    }

    public void Destroy()
    {
        if (dib != 0) { SelectObject(memDc, oldBmp); DeleteObject(dib); dib = 0; }
        if (font != 0) { DeleteObject(font); font = 0; }
        if (memDc != 0) { DeleteDC(memDc); memDc = 0; }
        if (hwnd != 0) { DestroyWindow(hwnd); hwnd = 0; }
    }

    public void Hide()
    {
        if (!visible) return;
        visible = false;
        ShowWindow(hwnd, SW_HIDE);
    }

    /// <summary>Affiche la bulle centrée au-dessus de (x, bottom). `px` = taille d'un gros pixel.</summary>
    public void Show(string newText, int x, int bottom, int px)
    {
        if (hwnd == 0) return;
        if (newText != text || px != fontScale || dib == 0) { text = newText; Render(px); lastX = int.MinValue; }
        int nx = x - w / 2, ny = bottom - h;
        if (nx != lastX || ny != lastY)
        {
            POINT dst = new() { X = nx, Y = ny }, src = default;
            SIZE size = new() { cx = w, cy = h };
            BLENDFUNCTION bf = new() { SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(hwnd, App.ScreenDc, &dst, &size, memDc, &src, 0, &bf, ULW_ALPHA);
            lastX = nx; lastY = ny;
        }
        if (!visible) { visible = true; ShowWindow(hwnd, SW_SHOWNOACTIVATE); }
    }

    void Render(int px)
    {
        fontScale = px;
        if (font != 0) DeleteObject(font);
        // CLEARTYPE_QUALITY sur fond blanc opaque : lisible à toutes les tailles
        font = CreateFontW(-(5 * px), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        var old = SelectObject(memDc, font);

        RECT measure = new() { Right = 60 * px };
        DrawTextW(memDc, text, -1, &measure, DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX);
        int pad = 3 * px, tail = 3 * px;
        w = Math.Max(measure.Right - measure.Left + pad * 2, 12 * px);
        h = measure.Bottom - measure.Top + pad * 2 + tail;
        // arrondi aux gros pixels
        w = (w + px - 1) / px * px;
        h = (h + px - 1) / px * px;

        if (dib != 0) { SelectObject(memDc, oldBmp); DeleteObject(dib); }
        var bi = new BITMAPINFOHEADER { biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        void* b;
        dib = CreateDIBSection(memDc, &bi, 0, &b, 0, 0);
        bits = (uint*)b;
        oldBmp = SelectObject(memDc, dib);
        SelectObject(memDc, font);

        // bulle en gros pixels : fond blanc, bord sombre, coins coupés, pointe en bas au milieu
        int gw = w / px, gh = (h - tail) / px, tw = tail / px;
        uint K = 0xFF111114, Wt = 0xFFFAFAFA;
        for (int gy = 0; gy < gh + tw; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                uint c = 0;
                if (gy < gh)
                {
                    bool corner = (gx == 0 || gx == gw - 1) && (gy == 0 || gy == gh - 1);
                    bool edge = gx == 0 || gy == 0 || gx == gw - 1 || gy == gh - 1;
                    bool inner = (gx == 1 || gx == gw - 2) && (gy == 1 || gy == gh - 2);
                    if (corner) c = 0;
                    else if (edge || inner) c = K;
                    else c = Wt;
                    if (inner && !edge) c = K;
                }
                else
                {
                    int k = gy - gh, mid = gw / 2;
                    if (gx >= mid - (tw - k) && gx <= mid + (tw - k) - 1) c = (gx == mid - (tw - k) || gx == mid + (tw - k) - 1) ? K : Wt;
                }
                for (int yy = 0; yy < px; yy++)
                    for (int xx = 0; xx < px; xx++)
                        bits[(gy * px + yy) * w + gx * px + xx] = c;
            }

        SetBkMode(memDc, 1);                 // TRANSPARENT
        SetTextColor(memDc, 0x00201A14);     // presque noir (BGR)
        RECT r = new() { Left = pad, Top = pad, Right = w - pad, Bottom = h - tail - pad };
        DrawTextW(memDc, text, -1, &r, DT_CENTER | DT_WORDBREAK | DT_NOPREFIX);
        // GDI laisse l'alpha à 0 sur le texte : l'intérieur de la bulle est opaque
        for (int y = pad / 2; y < h - tail - pad / 2; y++)
            for (int x = pad / 2; x < w - pad / 2; x++)
                bits[y * w + x] |= 0xFF000000;
        SelectObject(memDc, old);
    }
}
