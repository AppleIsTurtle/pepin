namespace Pepin;

public enum EggPhase : byte { Fall, Idle, Hatch, Done }

/// <summary>
/// Première installation : un œuf tombe sur le bureau et tremble. On le tapote pour le fissurer (3 à 5 fois) ;
/// à la dernière tape il gonfle, flash, éclats de coquille, rayons de lumière, et l'animal tiré au sort apparaît
/// (<see cref="Hatched"/>), puis une bannière annonce l'espèce. Jamais d'éclosion sans clic.
/// </summary>
public sealed class Egg
{
    public const int CW = 128, CH = 100;          // toile en pixels logiques
    public const int Cx = 64, Ground = 88;        // pied de l'œuf dans la toile
    public const int EW = 30, EH = 36;            // toile de l'œuf seul
    const double Swell = 0.55, BannerFrom = 1.1, BannerTo = 5.2, End = 5.6;

    readonly Overlay ov = new(clickable: true);
    readonly PixelCanvas canvas = new(CW, CH), shell = new(EW, EH);
    readonly Random r;
    int hitsNeeded, hits;
    double t, fallY, fallV, hitAt = -10, nextWobble, lastClick = -10;
    bool hatchFired;
    readonly double[] sx = new double[12], sy = new double[12], svx = new double[12], svy = new double[12];
    readonly int[] skind = new int[12];

    public EggPhase Phase { get; private set; } = EggPhase.Fall;
    public Species Species { get; }
    /// <summary>Le moment où l'animal apparaît (flash) : App montre la créature à la place de l'œuf.</summary>
    public Action<Species>? Hatched;
    public double X, Y;                           // pied de l'œuf à l'écran
    public int Hits => hits;

    internal PixelCanvas Canvas => canvas;        // outils de dev (--preview)

    public Egg(Species species, Random? rnd = null)
    {
        r = rnd ?? Random.Shared;
        Species = species;
        hitsNeeded = r.Next(3, 6);
        nextWobble = 1.5;
    }

    public int Fps => Phase switch
    {
        EggPhase.Done => 0,
        EggPhase.Idle => t - hitAt < 0.6 || Wobbling ? 30 : 8,
        _ => 30,
    };

    bool Wobbling => t >= nextWobble && t < nextWobble + 0.7;

    public void Create(nint inst)
    {
        ov.Create(inst);
        ov.Click = OnClick;
    }

    public void Destroy() => ov.Destroy();

    /// <summary>L'œuf tombe du haut de la zone de travail jusqu'à (x, y).</summary>
    public void Drop(double x, double y, RECT work, int scale)
    {
        X = x; Y = y;
        fallY = -(y - work.Top) / Math.Max(1, scale);     // en pixels logiques au-dessus du sol
        fallV = 0;
        Phase = EggPhase.Fall;
        t = 0;
    }

    void OnClick()
    {
        if (Phase != EggPhase.Idle || t - lastClick < 0.15) return;
        lastClick = t;
        hits++;
        hitAt = t;
        if (hits >= hitsNeeded) StartHatch();
    }

    /// <summary>Force la tape suivante (outils de dev).</summary>
    internal void DebugTap() { if (Phase == EggPhase.Fall) { Phase = EggPhase.Idle; fallY = 0; } lastClick = -10; OnClick(); }

    void StartHatch()
    {
        Phase = EggPhase.Hatch;
        t = 0;
        for (int i = 0; i < sx.Length; i++)
        {
            double a = -Math.PI * (0.1 + 0.8 * r.NextDouble());
            double sp = 40 + r.NextDouble() * 70;
            sx[i] = Cx + r.Next(-6, 7); sy[i] = Ground - 14 + r.Next(-8, 8);
            svx[i] = Math.Cos(a) * sp; svy[i] = Math.Sin(a) * sp - 20;
            skind[i] = r.Next(3);
        }
    }

    public void Tick(double dt, int scale, nint below)
    {
        if (Phase == EggPhase.Done) { if (ov.Visible) ov.Hide(); return; }
        t += dt;
        switch (Phase)
        {
            case EggPhase.Fall:
                fallV += 520 * dt;
                fallY += fallV * dt;
                if (fallY >= 0)
                {
                    fallY = 0;
                    if (fallV > 90) fallV = -fallV * 0.35;    // un petit rebond
                    else { fallV = 0; Phase = EggPhase.Idle; t = 0; nextWobble = 1.2; }
                }
                break;
            case EggPhase.Idle:
                // il tremble de plus en plus souvent tant qu'on ne s'en occupe pas
                if (t > nextWobble + 0.7) nextWobble = t + Math.Max(1.2, 5 - t / 20) + r.NextDouble() * 2;
                break;
            case EggPhase.Hatch:
                if (!hatchFired && t >= Swell) { hatchFired = true; Hatched?.Invoke(Species); }
                for (int i = 0; i < sx.Length; i++)
                {
                    svy[i] += 260 * dt;
                    sx[i] += svx[i] * dt; sy[i] += svy[i] * dt;
                }
                if (t > End) { Phase = EggPhase.Done; ov.Hide(); return; }
                break;
        }
        Render();
        int left = (int)Math.Round(X - Cx * scale), top = (int)Math.Round(Y - Ground * scale);
        ov.Present(canvas, scale, left, top, below);
    }

    // ================================================================== dessin

    static readonly uint Cream = PixelCanvas.Rgb(252, 244, 224), CreamShade = PixelCanvas.Rgb(228, 212, 182),
        Shine = PixelCanvas.Rgb(255, 255, 252), SpotBlue = PixelCanvas.Rgb(140, 196, 240), SpotPink = PixelCanvas.Rgb(245, 156, 188),
        SpotGreen = PixelCanvas.Rgb(150, 212, 128);

    void Render()
    {
        var c = canvas;
        c.Clear();
        if (Phase == EggPhase.Hatch)
        {
            if (t > Swell - 0.05) Rays(c, t - Swell);
            if (t < Swell + 0.05)
            {
                // gonfle et tremble violemment
                double k = t / Swell;
                int shake = (int)Math.Round(Math.Sin(t * 70) * (1 + 2 * k));
                DrawEgg(c, Cx + shake, Ground, 4, squash: -0.12 * k, tilt: 0, glow: k);
            }
            Shards(c);
            Flash(c, t - Swell);
            if (t > BannerFrom && t < BannerTo) Banner(c, t - BannerFrom);
            return;
        }

        int y = Ground + (int)Math.Round(Math.Min(0, fallY));
        double squash = 0, tilt = 0;
        int hop = 0;
        double sinceHit = t - hitAt;
        if (Phase == EggPhase.Idle && sinceHit < 0.5)
        {
            // tape : écrasé, petit saut, secousse
            squash = sinceHit < 0.08 ? 0.18 : 0;
            hop = sinceHit is > 0.08 and < 0.3 ? (int)Math.Round(Math.Sin((sinceHit - 0.08) / 0.22 * Math.PI) * 4) : 0;
            tilt = Math.Sin(sinceHit * 45) * 3 * (1 - sinceHit / 0.5);
        }
        else if (Phase == EggPhase.Idle && Wobbling)
        {
            double w = t - nextWobble;
            tilt = Math.Sin(w * 28) * 2.2 * Math.Sin(w / 0.7 * Math.PI) * (1 + hits * 0.4);
        }
        Shadow(c, Cx, Ground, 9 - Math.Min(5, -Math.Min(0, fallY) / 8));
        DrawEgg(c, Cx, y - hop, Phase == EggPhase.Idle ? hits * 4 / Math.Max(1, hitsNeeded) : 0, squash, tilt, 0);
        if (Phase == EggPhase.Idle && sinceHit < 0.35)
            for (int k = 0; k < 4; k++)
            {
                double a = k * 1.57 + 0.5, d = 13 + sinceHit * 30;
                Glyphs.Draw(c, Glyphs.SmallStar, Cx + (int)(Math.Cos(a) * d), y - 14 + (int)(Math.Sin(a) * d * 0.8));
            }
    }

    /// <summary>L'œuf (dessiné à part puis cerné) posé en (cx, ground) ; `crack` 0..4 ; `tilt` en pixels au sommet.</summary>
    void DrawEgg(PixelCanvas c, int cx, int ground, int crack, double squash, double tilt, double glow)
    {
        DrawShell(shell, crack, squash, glow);
        for (int yy = 0; yy < EH; yy++)
        {
            int shift = (int)Math.Round(tilt * (EH - 1 - yy) / (EH - 1));
            for (int xx = 0; xx < EW; xx++)
            {
                uint p = shell.Px[yy * EW + xx];
                if (p >> 24 != 0) c.Set(cx - EW / 2 + xx + shift, ground - EH + 1 + yy, p, Layer.Shell);
            }
        }
    }

    /// <summary>L'œuf seul dans une toile EW×EH (aussi pour le portrait du menu et l'icône).</summary>
    public static void DrawShell(PixelCanvas e, int crack, double squash = 0, double glow = 0)
    {
        e.Clear();
        double ry = 16 * (1 - squash), rx = 12 * (1 + squash * 0.6);
        double cy = EH - 2 - ry, ccx = EW / 2.0;
        for (int y = 0; y < EH; y++)
            for (int x = 0; x < EW; x++)
            {
                double dy = (y + .5 - cy) / ry;
                if (dy < -1 || dy > 1) continue;
                double w = rx * Math.Sqrt(1 - dy * dy) * (dy < 0 ? 0.82 + 0.18 * (1 + dy) : 1);   // pointu en haut
                double dx = x + .5 - ccx;
                if (Math.Abs(dx) > w) continue;
                uint col = dx > w * 0.35 && dy > -0.6 ? CreamShade : Cream;
                if (dx < -w * 0.2 && dx > -w * 0.7 && dy < -0.25 && dy > -0.75) col = Shine;
                // taches pastel
                int ix = (int)Math.Floor(dx), iy = (int)Math.Floor(dy * 16);
                if (ix is -6 or -5 && iy is 2 or 3) col = SpotBlue;
                if (ix is -5 && iy is 4) col = SpotBlue;
                if (ix is 4 or 5 && iy is -5 or -4) col = SpotPink;
                if (ix is 1 or 2 && iy is 9 or 10) col = SpotGreen;
                if (ix is -2 && iy is -11 or -10) col = SpotGreen;
                if (glow > 0) col = PixelCanvas.Lerp(col, PixelCanvas.Rgb(255, 250, 200), (float)(glow * 0.7));
                e.Set(x, y, col, Layer.Shell);
            }
        e.Outline(Glyphs.K);
        if (crack <= 0) return;
        // fissure en zigzag au milieu, qui s'allonge à chaque tape
        (int x, int y)[] path = [(15, 13), (12, 16), (15, 19), (11, 20), (8, 17), (5, 20), (17, 17), (20, 20), (23, 17), (25, 20)];
        int n = Math.Min(path.Length, 2 + crack * 2);
        for (int i = 1; i < n; i++)
        {
            var (x0, y0) = i == 6 ? path[0] : path[i - 1];
            Line(e, x0, y0, path[i].x, path[i].y);
        }
        if (crack >= 4) { Line(e, 15, 13, 16, 9); Line(e, 16, 9, 13, 6); }
    }

    static void Line(PixelCanvas e, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            if (e.LayerAt(x0, y0) == Layer.Shell) e.Set(x0, y0, Glyphs.K, Layer.Shell);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    static void Shadow(PixelCanvas c, int cx, int ground, double rx)
    {
        uint col = PixelCanvas.Argb(70, 20, 20, 30);
        for (int y = ground - 1; y <= ground + 1; y++)
            for (int x = (int)(cx - rx - 1); x <= cx + rx + 1; x++)
            {
                double ex = (x + .5 - cx) / rx, ey = (y + .5 - ground - .5) / 1.6;
                if (ex * ex + ey * ey <= 1 && c.LayerAt(x, y) == Layer.None) c.Set(x, y, col, Layer.Shadow);
            }
    }

    /// <summary>Pose une couleur prémultipliée par-dessus le pixel existant.</summary>
    static void Blend(PixelCanvas c, int x, int y, uint src)
    {
        if (!c.In(x, y)) return;
        uint dst = c.Px[y * c.W + x], a = src >> 24, inv = 255 - a;
        uint A = a + (dst >> 24) * inv / 255;
        uint R = ((src >> 16) & 255) + ((dst >> 16) & 255) * inv / 255;
        uint G = ((src >> 8) & 255) + ((dst >> 8) & 255) * inv / 255;
        uint B = (src & 255) + (dst & 255) * inv / 255;
        var l = c.LayerAt(x, y);
        c.Set(x, y, (A << 24) | (R << 16) | (G << 8) | B, l == Layer.None ? Layer.Fx : l);
    }

    void Rays(PixelCanvas c, double tt)
    {
        if (tt > BannerTo - Swell) return;
        double fade = tt < 0.3 ? tt / 0.3 : Math.Clamp(1 - (tt - 3.2) / 1.2, 0, 1);
        if (fade <= 0) return;
        int a = (int)(110 * fade);
        uint col = PixelCanvas.Argb(a, 255, 226, 110);
        double rot = tt * 0.9, cy = Ground - 14;
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
            {
                double dx = x + .5 - Cx, dy = (y + .5 - cy) * 1.3, d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 6 || d > 58 || y > Ground + 6) continue;
                double ang = Math.Atan2(dy, dx) + rot;
                double s = Math.Sin(ang * 5);                 // 10 rayons
                if (s > 0.55) Blend(c, x, y, col);
            }
    }

    void Flash(PixelCanvas c, double tt)
    {
        if (tt < -0.05 || tt > 0.45) return;
        double k = Math.Clamp(1 - tt / 0.45, 0, 1);
        double rad = 8 + 26 * Math.Clamp((tt + 0.05) / 0.25, 0, 1);
        int a = (int)(235 * k);
        uint col = PixelCanvas.Argb(a, 255, 255, 245);
        double cy = Ground - 14;
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
            {
                double dx = x + .5 - Cx, dy = (y + .5 - cy) / 0.75;
                if (dx * dx + dy * dy <= rad * rad) Blend(c, x, y, col);
            }
    }

    static readonly string[][] ShardShapes =
    [
        ["kkk.", "kwck", ".kk."],
        [".kk", "kwk", "kck", ".k."],
        ["kk..", "kwkk", ".kck"],
    ];

    void Shards(PixelCanvas c)
    {
        if (t < Swell) return;
        for (int i = 0; i < sx.Length; i++)
        {
            if (sy[i] > CH + 4) continue;
            var g = ShardShapes[(skind[i] + (int)(t * 8)) % ShardShapes.Length];
            for (int y = 0; y < g.Length; y++)
                for (int x = 0; x < g[y].Length; x++)
                {
                    uint col = g[y][x] switch { 'k' => Glyphs.K, 'w' => Cream, 'c' => CreamShade, _ => 0 };
                    if (col != 0) c.Set((int)sx[i] + x, (int)sy[i] + y, col, Layer.Fx);
                }
        }
    }

    void Banner(PixelCanvas c, double tt)
    {
        var tr = SpeciesInfo.Of(Species);
        string text = $"C'EST {tr.A.ToUpperInvariant()} !";
        int tw = PixelFont.Measure(text), w = tw + 16, h = 15;
        // arrive en descendant avec un petit rebond, repart en remontant
        double inK = Math.Clamp(tt / 0.3, 0, 1), outK = Math.Clamp((tt - (BannerTo - BannerFrom - 0.3)) / 0.3, 0, 1);
        int y = (int)Math.Round(-h + (h + 3) * (1 + 2.7 * Math.Pow(inK - 1, 3) + 1.7 * Math.Pow(inK - 1, 2)) - (h + 4) * outK);
        int x = (CW - w) / 2;
        uint body = PixelCanvas.Rgb(250, 196, 72), dark = PixelCanvas.Rgb(196, 138, 22);
        for (int yy = 0; yy < h + 2; yy++)
            for (int xx = 0; xx < w; xx++)
            {
                bool corner = (xx == 0 || xx == w - 1) && (yy == 0 || yy == h + 1);
                if (corner) continue;
                bool edge = xx == 0 || xx == w - 1 || yy == 0 || yy == h + 1;
                c.Set(x + xx, y + yy, edge ? Glyphs.K : yy >= h - 1 ? dark : body, Layer.Fx);
            }
        PixelFont.Draw(c, text, x + 9, y + 5, dark);
        PixelFont.Draw(c, text, x + 8, y + 4, Glyphs.White);
        if (tt > 0.3)
            for (int k = 0; k < 2; k++)
            {
                int sxp = k == 0 ? x - 4 : x + w + 3;
                if (((int)(tt * 6) + k) % 2 == 0) Glyphs.Draw(c, Glyphs.SmallStar, sxp, y + 8);
            }
    }

    /// <summary>Icône 16×16 de l'œuf (zone de notification pendant la couvaison).</summary>
    public static void DrawIcon(PixelCanvas ic)
    {
        var e = new PixelCanvas(EW, EH);
        DrawShell(e, 0);
        ic.Clear();
        // l'œuf fait 24×32 : réduit de moitié par échantillonnage
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int ex = 3 + (x - 2) * 2, ey = 2 + y * 2;
                if (ex < 0 || ex >= EW || ey >= EH) continue;
                uint p = e.Px[ey * EW + ex];
                if (p >> 24 != 0) ic.Set(x, y, p, Layer.Head);
            }
    }
}
