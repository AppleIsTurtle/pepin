using static Pepin.PixelCanvas;
using static Pepin.ArtKit;

namespace Pepin;

/// <summary>
/// Le hérisson : dôme de piquants, museau pointu, petites pattes. Repli = boule de piquants (qui roule avec
/// <see cref="Visual.SpinFrame"/>) ; <see cref="Visual.Puffed"/> = piquants hérissés.
/// </summary>
public static class HerissonArt
{
    static readonly uint Sp = Rgb(118, 86, 64), SpD = Rgb(82, 58, 44), SpL = Rgb(150, 116, 88), Tip = Rgb(222, 204, 172);
    static readonly uint Skin = Rgb(236, 204, 160), SkinD = Rgb(206, 166, 120), Foot = Rgb(176, 128, 92);

    static uint skin, skinD, foot;

    public static void Draw(PixelCanvas canvas, Visual vis)
    {
        int tuck = vis.InShell ? 3 : Math.Clamp(vis.LegsTuck, 0, 3);
        Begin(canvas, vis, vis.BodyDy + tuck);
        skin = Tinted(Skin); skinD = Tinted(SkinD); foot = Tinted(Foot);

        if (v.InShell) { Ball(); Finish(false, 22, 18); return; }

        float o = v.HeadOut;
        int hx = 30 + v.HeadDx - (int)MathF.Round((1 - o) * 6);
        int hy = 21 + v.HeadDy + (int)MathF.Round((1 - o) * 2);
        bool head = o > 0.15f;

        Legs(tuck);
        Back(v.Puffed);
        if (head) Head(hx, hy);
        if (!head) { hx = 22; hy = 18; }
        Finish(head, hx, hy);
    }

    static readonly int[] BackStep = [-1, 0, 1, 0], FrontStep = [1, 0, -1, 0];

    static void Legs(int tuck)
    {
        int p = v.LegPhase & 3;
        int bBot = 32 - tuck - (p == 1 ? 1 : 0), fBot = 32 - tuck - (p == 3 ? 1 : 0);
        if (v.LegsDangle) { bBot = 33; fBot = 33; }
        Rect(9 + BackStep[p], 28, 11 + BackStep[p], bBot, foot, Layer.Leg);
        Rect(21 + FrontStep[p], 28, 23 + FrontStep[p], fBot, foot, Layer.Leg);
    }

    static void Head(int hx, int hy)
    {
        int flushLine = v.Flush > 0 ? hy - 6 + (int)MathF.Round(13 * v.Flush) : int.MinValue;
        uint Col(int y, uint c) => y < flushLine ? FlushCol : c;
        for (int y = hy - 7; y <= hy + 7; y++)
            for (int x = hx - 7; x <= hx + 10; x++)
            {
                float ddx = x + .5f - hx, ddy = y + .5f - hy;
                bool round = ddx * ddx + ddy * ddy <= 6.4f * 6.4f;
                // museau : un cône vers l'avant, un peu bas
                bool snout = x > hx && x <= hx + 9 && y >= hy + 1 + (x - hx) / 5 && y <= hy + 5 - (x - hx) / 4;
                if (round || snout) B(x, y, Col(y, y > hy + 3 ? skinD : skin), Layer.Head);
            }
        B(hx + 9, hy + 2, K, Layer.Head); B(hx + 9, hy + 3, K, Layer.Head); B(hx + 8, hy + 2, K, Layer.Head);   // truffe
        // petite oreille ronde
        B(hx - 3, hy - 7, skinD, Layer.Head); B(hx - 2, hy - 7, skinD, Layer.Head); B(hx - 3, hy - 8, skinD, Layer.Head);
    }

    /// <summary>Le dôme de piquants : texture en chevrons, bord dentelé (plus long quand il se hérisse).</summary>
    static void Back(bool puffed)
    {
        const float cx = 15.5f, cy = 25f, rx = 13.5f, ry = 12f;
        int spike = puffed ? 3 : 1;
        for (int sy = 8; sy <= 29; sy++)
            for (int sx = -1; sx <= 31; sx++)
            {
                float ex = (sx + .5f - cx) / rx, ey = (sy + .5f - cy) / ry;
                float d = ex * ex + ey * ey;
                if (sy > 27) { if (sx < 2 || sx > 28 || d > 1.25f) continue; }
                else if (d > 1) continue;
                B(sx, sy, SpinePix(sx, sy, 0), Layer.Shell);
            }
        // dents : des pointes qui dépassent du bord, orientées vers l'extérieur
        for (int k = 0; k < 15; k++)
        {
            float a = MathF.PI * (1.03f + k * 0.075f);              // de l'arrière vers le haut-avant
            float bx = cx + MathF.Cos(a) * rx, by = cy + MathF.Sin(a) * ry;
            if (by > 26) continue;
            for (int l = 1; l <= spike + (k % 2); l++)
                B((int)MathF.Round(bx + MathF.Cos(a) * l), (int)MathF.Round(by + MathF.Sin(a) * l * 1.1f), l == spike + (k % 2) ? Tip : Sp, Layer.Shell);
        }
    }

    /// <summary>Mèches de piquants en diagonale (vers l'arrière), pointes claires éparses ; `phase` fait tourner la boule.</summary>
    static uint SpinePix(int sx, int sy, int phase)
    {
        int u = sx + phase;
        int band = (((u - sy + 300) / 2) % 3 + 3) % 3;
        if (((u * 7 + sy * 13) % 19 + 19) % 19 == 0) return Tip;
        return band switch { 0 => SpD, 1 => Sp, _ => SpL };
    }

    /// <summary>En boule : un rond de piquants qui tourne (texture décalée), museau caché.</summary>
    static void Ball()
    {
        const float cx = 16f, cy = 22f, r = 10.5f;
        int phase = (v.SpinFrame & 3) * 2;
        for (int sy = 10; sy <= 34; sy++)
            for (int sx = 4; sx <= 28; sx++)
            {
                float ddx = sx + .5f - cx, ddy = sy + .5f - cy;
                if (ddx * ddx + ddy * ddy > r * r) continue;
                B(sx, sy, SpinePix(sx, sy, phase), Layer.Shell);
            }
        for (int k = 0; k < 16; k++)
        {
            float a = k * MathF.PI / 8 + (v.SpinFrame & 3) * 0.2f;
            float bx = cx + MathF.Cos(a) * r, by = cy + MathF.Sin(a) * r;
            if (by > cy + r - 1.5f) continue;
            int len = v.Puffed ? 3 : 1 + k % 2;
            for (int l = 1; l <= len; l++)
                B((int)MathF.Round(bx + MathF.Cos(a) * l), (int)MathF.Round(by + MathF.Sin(a) * l), l == len ? Tip : Sp, Layer.Shell);
        }
        // un bout de museau qui dépasse
        B(26, 27, skin, Layer.Head); B(27, 27, skinD, Layer.Head); B(28, 27, K, Layer.Head);
    }

    public static void DrawIcon(PixelCanvas ic)
    {
        ic.Clear();
        // piquants autour, tête crème au centre
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float ddx = x + .5f - 8, ddy = y + .5f - 8.5f, d = ddx * ddx + ddy * ddy;
                if (d <= 7.6f * 7.6f) ic.Set(x, y, ((x + y) % 3 == 0) ? SpD : Sp, Layer.Shell);
            }
        IconDisc(ic, 8, 9.5f, 5.6f, Skin);
        ic.Set(7, 15, K, Layer.Head); ic.Set(8, 15, K, Layer.Head);
        ic.Outline(K);
        RoundIconFace(ic);
    }
}
