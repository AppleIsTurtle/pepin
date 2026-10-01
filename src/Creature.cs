using static Pepin.Native;

namespace Pepin;

/// <summary>
/// Une tortue affichée : son <see cref="Pet"/> + sa petite fenêtre layered toujours au premier plan.
/// La tortue de la maison et les visiteurs sont chacun une Creature.
/// </summary>
public sealed unsafe class Creature
{
    public readonly Pet Pet;
    public readonly Senses Senses;
    public nint Hwnd { get; private set; }
    public int Scale { get; private set; } = 3;
    public bool Hidden { get; private set; }

    readonly PixelCanvas canvas = new(TurtleArt.CW, TurtleArt.CH);
    readonly uint[] shown = new uint[TurtleArt.CW * TurtleArt.CH];
    nint memDc, dib, oldBmp;
    uint* bits;
    int bmpW, bmpH;
    bool hasShown, windowShown;
    public int WinX { get; private set; } = int.MinValue;
    public int WinY { get; private set; }

    // souris
    bool mouseDown, dragging;
    POINT downPt;
    double lastT;

    static readonly Dictionary<nint, Creature> byHwnd = [];
    public static Creature? From(nint hwnd) => byHwnd.GetValueOrDefault(hwnd);

    public Creature(Pet pet, Senses senses)
    {
        Pet = pet;
        Senses = senses;
    }

    public void Create(nint inst, nint screenDc, string title)
    {
        Hwnd = CreateWindowExW(WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                               App.WindowClass, title, WS_POPUP, (int)Pet.X, (int)Pet.Y, 1, 1, 0, 0, inst, 0);
        byHwnd[Hwnd] = this;
        memDc = CreateCompatibleDC(screenDc);
    }

    public void Destroy()
    {
        byHwnd.Remove(Hwnd);
        if (dib != 0) { SelectObject(memDc, oldBmp); DeleteObject(dib); dib = 0; }
        if (memDc != 0) { DeleteDC(memDc); memDc = 0; }
        if (Hwnd != 0) { DestroyWindow(Hwnd); Hwnd = 0; }
    }

    public void SetHidden(bool hidden)
    {
        if (hidden == Hidden) return;
        Hidden = hidden;
        ShowWindow(Hwnd, hidden ? SW_HIDE : SW_SHOWNOACTIVATE);
        windowShown = !hidden;
        if (hidden) CancelMouse();
    }

    // ------------------------------------------------------------------ boucle

    /// <summary>Un tick : sens, drag, tortue, rendu.</summary>
    public void Tick(double now, double dt, uint dpi, int sizeLevel)
    {
        lastT = now;
        int s = Math.Max(1, (int)Math.Round(sizeLevel * dpi / 96.0));
        if (s != Scale || dib == 0) SetScale(s);

        Senses.Update(now, dt, Pet.X, Pet.Y);
        if (Pet.Dragging) Pet.DragFollow();
        Pet.CursorOnMe = !Hidden && HitTest((int)Senses.CX, (int)Senses.CY);
        Pet.Scale = Scale;
        Pet.Update(dt);
        if (!Hidden) Render();
    }

    void SetScale(int s)
    {
        Scale = s;
        Pet.Scale = s;
        if (dib != 0) { SelectObject(memDc, oldBmp); DeleteObject(dib); }
        bmpW = TurtleArt.CW * s;
        bmpH = TurtleArt.CH * s;
        var bi = new BITMAPINFOHEADER { biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = bmpW, biHeight = -bmpH, biPlanes = 1, biBitCount = 32 };
        void* b;
        dib = CreateDIBSection(memDc, &bi, 0, &b, 0, 0);
        bits = (uint*)b;
        oldBmp = SelectObject(memDc, dib);
        hasShown = false;
    }

    public void Render()
    {
        TurtleArt.Draw(canvas, Pet.V);
        // hors drag, la fenêtre avance par pixels logiques entiers : plus "pixel art" et bien moins de mises à jour
        double gx = Pet.X, gy = Pet.Y - Pet.Z;
        if (!Pet.Dragging) { gx = Math.Round(gx / Scale) * Scale; gy = Math.Round(gy / Scale) * Scale; }
        int nx = (int)Math.Round(gx) - TurtleArt.AX * Scale;
        int ny = (int)Math.Round(gy) - TurtleArt.AY * Scale;
        if (Pet.Occluder is RECT occ) Occlude(occ, nx, ny);

        if (!hasShown || !canvas.Px.AsSpan().SequenceEqual(shown))
        {
            canvas.Px.CopyTo(shown, 0);
            hasShown = true;
            Blit();
            POINT dst = new() { X = nx, Y = ny }, src = default;
            SIZE size = new() { cx = bmpW, cy = bmpH };
            BLENDFUNCTION bf = new() { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(Hwnd, App.ScreenDc, &dst, &size, memDc, &src, 0, &bf, ULW_ALPHA);
            WinX = nx; WinY = ny;
            if (!windowShown) { windowShown = true; ShowWindow(Hwnd, SW_SHOWNOACTIVATE); }   // première image : on montre la fenêtre
        }
        else if (nx != WinX || ny != WinY)
        {
            SetWindowPos(Hwnd, 0, nx, ny, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            WinX = nx; WinY = ny;
        }
    }

    /// <summary>« Derrière une fenêtre » : on gomme les pixels dont le centre tombe dans son rectangle.</summary>
    void Occlude(RECT r, int nx, int ny)
    {
        for (int y = 0; y < TurtleArt.CH; y++)
        {
            int sy = ny + y * Scale + Scale / 2;
            if (sy < r.Top || sy >= r.Bottom) continue;
            for (int x = 0; x < TurtleArt.CW; x++)
            {
                int sx = nx + x * Scale + Scale / 2;
                if (sx >= r.Left && sx < r.Right) canvas.Set(x, y, 0, Layer.None);
            }
        }
    }

    void Blit()
    {
        uint* dst = bits;
        var px = canvas.Px;
        for (int y = 0; y < TurtleArt.CH; y++)
        {
            uint* row = dst;
            for (int x = 0; x < TurtleArt.CW; x++)
            {
                uint c = px[y * TurtleArt.CW + x];
                for (int k = 0; k < Scale; k++) *dst++ = c;
            }
            for (int r = 1; r < Scale; r++)
            {
                Buffer.MemoryCopy(row, dst, bmpW * 4, bmpW * 4);
                dst += bmpW;
            }
        }
    }

    public bool HitTest(int sx, int sy)
    {
        if (WinX == int.MinValue) return false;
        int cx = (int)Math.Floor((sx - WinX) / (double)Scale), cy = (int)Math.Floor((sy - WinY) / (double)Scale);
        return canvas.IsSolid(cx, cy);
    }

    /// <summary>Centre approximatif de la carapace à l'écran (pour placer une bulle).</summary>
    public (int x, int y) Top => ((int)Pet.X, (int)(Pet.Y - Pet.Z - 30 * Scale));

    // ------------------------------------------------------------------ souris

    /// <returns>true si le message a été traité.</returns>
    public bool HandleMouse(uint m)
    {
        switch (m)
        {
            case WM_LBUTTONDOWN:
            {
                POINT p;
                GetCursorPos(&p);
                if (HitTest(p.X, p.Y))
                {
                    mouseDown = true;
                    Pet.MouseDown = true;
                    downPt = p;
                    SetCapture(Hwnd);
                }
                return true;
            }
            case WM_MOUSEMOVE:
                if (mouseDown && !dragging)
                {
                    POINT p;
                    GetCursorPos(&p);
                    if (Math.Abs(p.X - downPt.X) + Math.Abs(p.Y - downPt.Y) > 5)
                    {
                        dragging = true;
                        Senses.Update(lastT, 0, Pet.X, Pet.Y);
                        Pet.OnGrab();
                    }
                }
                return true;
            case WM_LBUTTONUP:
                EndMouse(released: true);
                return true;
            case WM_CAPTURECHANGED:
                if (mouseDown) EndMouse(released: false);
                return true;
        }
        return false;
    }

    void EndMouse(bool released)
    {
        bool wasDrag = dragging;
        mouseDown = dragging = false;
        Pet.MouseDown = false;
        ReleaseCapture();
        if (wasDrag)
        {
            double now = App.Now;
            Senses.Update(now, Math.Max(0.001, now - lastT), Pet.X, Pet.Y);
            var (vx, vy) = Senses.RecentVelocity(now);
            Pet.OnRelease(vx, vy);
        }
        else if (released) Pet.OnClick();
    }

    void CancelMouse()
    {
        if (!mouseDown) return;
        mouseDown = dragging = false;
        Pet.MouseDown = false;
        Pet.Dragging = false;
        ReleaseCapture();
    }
}
