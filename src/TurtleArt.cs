using static Pepin.PixelCanvas;
using static Pepin.ArtKit;

namespace Pepin;

/// <summary>
/// Dessine la tortue dans une toile d'après un <see cref="Visual"/> (repère et outils partagés : <see cref="ArtKit"/>).
/// </summary>
public static class TurtleArt
{
    static readonly uint SH = Rgb(160, 108, 50), SHD = Rgb(110, 72, 30), SHL = Rgb(196, 140, 72);
    static readonly uint Belly = Rgb(226, 196, 128);
    static readonly uint G = Rgb(130, 214, 80), GD = Rgb(80, 168, 58);

    static uint g, gd;

    public static void Draw(PixelCanvas canvas, Visual vis)
    {
        int tuck = vis.InShell ? 4 : Math.Clamp(vis.LegsTuck, 0, 3);
        Begin(canvas, vis, vis.BodyDy + tuck);
        g = Tinted(G);
        gd = Tinted(GD);

        float o = v.InShell ? 0 : v.HeadOut;
        int hx = 33 + v.HeadDx - (int)MathF.Round((1 - o) * 7);
        int hy = 19 + v.HeadDy + (int)MathF.Round((1 - o) * 3);
        bool head = o > 0.15f;

        if (!v.InShell) Legs(tuck);
        if (head) Head(hx, hy);
        Shell();
        if (!head) { hx = 24; hy = 22; } // les effets se placent au-dessus de la carapace
        Finish(head, hx, hy);
    }

    // ---------------------------------------------------------------- corps

    static readonly int[] BackStep = [-1, 0, 1, 0], FrontStep = [1, 0, -1, 0];

    static void Legs(int tuck)
    {
        int p = v.LegPhase & 3;
        int bBot = 32 - tuck - (p == 1 ? 1 : 0);
        int fBot = 32 - tuck - (p == 3 ? 1 : 0);
        if (v.LegsDangle) { bBot = 33; fBot = 33; }
        Rect(8 + BackStep[p], 27, 11 + BackStep[p], bBot, g, Layer.Leg);
        Rect(20 + FrontStep[p], 27, 23 + FrontStep[p], fBot, g, Layer.Leg);
    }

    static void Head(int hx, int hy)
    {
        int flushLine = v.Flush > 0 ? hy - 7 + (int)MathF.Round(15 * v.Flush) : int.MinValue;
        for (int y = hy - 8; y <= hy + 8; y++)
            for (int x = hx - 8; x <= hx + 8; x++)
            {
                float ddx = x + .5f - hx, ddy = y + .5f - hy;
                if (ddx * ddx + ddy * ddy <= 7.2f * 7.2f) B(x, y, y < flushLine ? FlushCol : g, Layer.Head);
            }
        void Spot(int x, int y) => B(x, y, y < flushLine ? FlushDark : gd, Layer.Head);
        Spot(hx - 2, hy - 6); Spot(hx, hy - 6); Spot(hx + 2, hy - 6);
        Spot(hx - 1, hy - 5); Spot(hx + 1, hy - 5); Spot(hx + 3, hy - 4);
    }

    static void Shell()
    {
        bool narrow = v.InShell && (v.SpinFrame & 1) == 1;
        bool upside = v.InShell && v.SpinFrame == 2;
        for (int sy = 12; sy <= 29; sy++)
            for (int sx = 2; sx <= 30; sx++)
            {
                int ry = upside ? 42 - sy : sy;
                uint col = narrow ? NarrowShellPix(sx, ry) : ShellPix(sx, ry, upside);
                if (col != 0) B(sx, sy, col, Layer.Shell);
            }
    }

    static uint ShellPix(int sx, int sy, bool belly)
    {
        float ex = (sx + .5f - 16) / 12.5f, ey = (sy + .5f - 25) / 11f;
        bool inE = ex * ex + ey * ey <= 1 && sy <= 27;
        bool rim = sx >= 4 && sx <= 28 && sy >= 26 && sy <= 28;
        if (!inE && !rim) return 0;
        if (rim) return belly ? Belly : SHD;
        if (sy == 20 && sx >= 5 && sx <= 27) return SHD;
        if (sx == 16 && sy >= 15 && sy <= 25) return SHD;
        if ((sx == 10 || sx == 22) && sy >= 20 && sy <= 25) return SHD;
        if ((sx, sy) is (8, 17) or (9, 16) or (10, 16) or (7, 18) or (12, 15) or (13, 15)) return SHL;
        return SH;
    }

    static uint NarrowShellPix(int sx, int sy)
    {
        float ex = (sx + .5f - 16) / 6.5f, ey = (sy + .5f - 25) / 11f;
        bool inE = ex * ex + ey * ey <= 1 && sy <= 27;
        bool rim = sx >= 10 && sx <= 22 && sy >= 26 && sy <= 28;
        if (!inE && !rim) return 0;
        if (rim || sy == 20 || sx == 16) return SHD;
        if ((sx, sy) is (12, 17) or (13, 16)) return SHL;
        return SH;
    }

    // ---------------------------------------------------------------- icône

    /// <summary>Tête seule, pour l'icône de la zone de notification (16×16).</summary>
    public static void DrawIcon(PixelCanvas ic)
    {
        ic.Clear();
        IconDisc(ic, 8, 8.5f, 6.6f, G);
        ic.Set(6, 3, GD, Layer.Head); ic.Set(8, 3, GD, Layer.Head); ic.Set(7, 4, GD, Layer.Head); ic.Set(9, 4, GD, Layer.Head);
        ic.Outline(K);
        RoundIconFace(ic);
    }
}
