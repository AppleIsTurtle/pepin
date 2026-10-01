using static Pepin.PixelCanvas;

namespace Pepin;

/// <summary>
/// Dessine la tortue dans une toile d'après un <see cref="Visual"/>.
/// Espace "sprite" : tortue tournée vers la droite, pieds au pixel (22, 33).
/// </summary>
public static class TurtleArt
{
    public const int CW = 56, CH = 54;   // taille de la toile
    public const int AX = 28, AY = 40;   // position des pieds dans la toile
    const int SX = 22, SY = 33;          // position des pieds dans l'espace sprite

    static readonly uint SH = Rgb(160, 108, 50), SHD = Rgb(110, 72, 30), SHL = Rgb(196, 140, 72);
    static readonly uint Belly = Rgb(226, 196, 128);
    static readonly uint G = Rgb(130, 214, 80), GD = Rgb(80, 168, 58);
    static readonly uint ShadowCol = Argb(70, 20, 20, 30);
    static readonly uint K = Glyphs.K, White = Glyphs.White, Red = Glyphs.Red, Pink = Glyphs.Pink;

    // état du dessin en cours (un seul thread)
    static PixelCanvas c = null!;
    static Visual v = null!;
    static uint g, gd;
    static int dy;

    static int MX(int sx) => v.FacingRight ? AX + (sx - SX) + v.BodyDx : AX - (sx - SX) - v.BodyDx;
    static int MY(int sy) => AY + (sy - SY) + dy;
    static void B(int sx, int sy, uint col, Layer l) => c.Set(MX(sx), MY(sy), col, l);

    static void Rect(int x0, int y0, int x1, int y1, uint col, Layer l)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) B(x, y, col, l);
    }

    /// <summary>Pixel du visage : seulement là où la tête est visible.</summary>
    static void F(int sx, int sy, uint col)
    {
        int x = MX(sx), y = MY(sy);
        if (c.LayerAt(x, y) == Layer.Head) c.Set(x, y, col, Layer.Head);
    }

    static void FRect(int x0, int y0, int x1, int y1, uint col)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) F(x, y, col);
    }

    public static void Draw(PixelCanvas canvas, Visual vis)
    {
        c = canvas; v = vis;
        c.Clear();
        g = Lerp(G, v.Tint, v.TintAmount);
        gd = Lerp(GD, v.Tint, v.TintAmount);
        int tuck = v.InShell ? 4 : Math.Clamp(v.LegsTuck, 0, 3);
        dy = v.BodyDy + tuck;

        float o = v.InShell ? 0 : v.HeadOut;
        int hx = 33 + v.HeadDx - (int)MathF.Round((1 - o) * 7);
        int hy = 19 + v.HeadDy + (int)MathF.Round((1 - o) * 3);
        bool head = o > 0.15f;

        if (!v.InShell) Legs(tuck);
        if (head) Head(hx, hy);
        Shell();
        c.Outline(K);
        if (!v.NoShadow) Shadow();
        if (head) Face(hx, hy);
        if (!head) { hx = 24; hy = 22; } // les effets se placent au-dessus de la carapace
        Effects(hx, hy);
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

    static readonly uint FlushCol = Rgb(238, 92, 80), FlushDark = Rgb(196, 58, 56);

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

    static void Shadow()
    {
        int z = v.ShadowZ;
        if (z > CH - AY - 3) return;
        float rx = 13f - z / 3f, ry = 1.6f;
        float cx = MX(18) + .5f, cy = AY + z + .5f;
        for (int y = (int)(cy - 3); y <= (int)(cy + 3); y++)
            for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
            {
                float ex = (x + .5f - cx) / rx, ey = (y + .5f - cy) / ry;
                if (ex * ex + ey * ey <= 1 && c.LayerAt(x, y) == Layer.None && c.In(x, y))
                    c.Set(x, y, ShadowCol, Layer.Shadow);
            }
    }

    // ---------------------------------------------------------------- visage

    static void Face(int hx, int hy)
    {
        int ex = hx - 2 + v.LookX, ey = hy - 1 + v.LookY;
        int mx = hx + 1 + v.LookX, my = hy + 3;

        // joues
        F(ex - 2, ey + 3, Pink); F(ex + 7, ey + 3, Pink);
        if (v.Blush) { F(ex - 1, ey + 3, Pink); F(ex + 8, ey + 3, Pink); F(ex - 2, ey + 4, Pink); F(ex + 7, ey + 4, Pink); }

        var eyes = v.Eyes;
        if (v.Blink && eyes is Eyes.Normal or Eyes.Wide or Eyes.Determined or Eyes.HalfLid or Eyes.Angry)
        {
            FRect(ex, ey + 2, ex + 1, ey + 2, K); FRect(ex + 5, ey + 2, ex + 6, ey + 2, K);
            if (eyes == Eyes.Angry) AngryBrows(ex, ey);
        }
        else
        {
            for (int side = 0; side < 2; side++)
            {
                int x = ex + side * 5;
                switch (eyes)
                {
                    case Eyes.Normal:
                        FRect(x, ey, x + 1, ey + 2, K); F(x, ey, White);
                        break;
                    case Eyes.Closed:
                        F(x - 1, ey + 1, K); F(x, ey + 2, K); F(x + 1, ey + 2, K); F(x + 2, ey + 1, K);
                        break;
                    case Eyes.Happy:
                        F(x - 1, ey + 1, K); F(x, ey, K); F(x + 1, ey, K); F(x + 2, ey + 1, K);
                        break;
                    case Eyes.HalfLid:
                        FRect(x - 1, ey + 1, x + 2, ey + 1, K); FRect(x, ey + 2, x + 1, ey + 2, K);
                        break;
                    case Eyes.Angry:
                        FRect(x, ey, x + 1, ey + 1, K);
                        break;
                    case Eyes.Wide:
                        FRect(x - 1, ey - 1, x + 1, ey + 2, K); F(x - 1, ey - 1, White);
                        break;
                    case Eyes.Spiral:
                        var sp = ((v.AnimFrame + side) & 1) == 0 ? Glyphs.SpiralA : Glyphs.SpiralB;
                        for (int yy = 0; yy < 3; yy++)
                            for (int xx = 0; xx < 3; xx++)
                                if (sp[yy][xx] == 'k') F(x - 1 + xx, ey + yy, K);
                        break;
                    case Eyes.Determined:
                        FRect(x - 1, ey, x + 2, ey, K); FRect(x, ey + 1, x + 1, ey + 2, K); F(x, ey + 1, White);
                        break;
                    case Eyes.Squint:
                        // '>' à gauche, '<' à droite : pointent vers le centre du visage
                        int d = side == 0 ? 1 : -1, bx = side == 0 ? x : x + 1;
                        F(bx, ey, K); F(bx + d, ey + 1, K); F(bx, ey + 2, K);
                        break;
                    case Eyes.Hearts:
                        for (int yy = 0; yy < 3; yy++)
                            for (int xx = 0; xx < 3; xx++)
                                if (Glyphs.HeartEye[yy][xx] == 'r') F(x - 1 + xx, ey + yy, Red);
                        break;
                }
            }
            if (eyes == Eyes.Angry) AngryBrows(ex, ey);
        }

        switch (v.Mouth)
        {
            case Mouth.Smile: F(mx - 1, my, K); F(mx, my + 1, K); F(mx + 1, my, K); break;
            case Mouth.Grin: FRect(mx - 1, my, mx + 1, my, Red); F(mx, my + 1, Red); break;
            case Mouth.Yawn: FRect(mx - 1, my, mx + 1, my + 3, K); FRect(mx, my + 1, mx, my + 2, Red); break;
            case Mouth.Oh: F(mx, my, K); F(mx - 1, my + 1, K); F(mx + 1, my + 1, K); F(mx, my + 2, K); F(mx, my + 1, Red); break;
            case Mouth.Zigzag:
                F(mx - 2, my + 1, K); F(mx - 1, my, K); F(mx, my + 1, K); F(mx + 1, my, K); F(mx + 2, my + 1, K);
                break;
            case Mouth.Frown: F(mx - 1, my + 1, K); F(mx, my, K); F(mx + 1, my + 1, K); break;
            case Mouth.Pout: FRect(mx - 1, my, mx + 1, my, K); F(mx, my + 1, Pink); break;
            case Mouth.ChewA: FRect(mx - 1, my, mx + 1, my, K); break;
            case Mouth.ChewB: F(mx - 1, my, K); F(mx, my, Red); F(mx + 1, my, K); F(mx, my + 1, K); break;
            case Mouth.Bite:
                FRect(mx - 2, my - 1, mx + 2, my + 2, K);
                F(mx - 1, my, White); F(mx + 1, my, White); FRect(mx - 1, my + 1, mx + 1, my + 1, Red);
                break;
            case Mouth.Tongue: FRect(mx - 1, my, mx + 1, my, K); F(mx, my + 1, Pink); F(mx + 1, my + 1, Pink); F(mx, my + 2, Pink); break;
        }
    }

    static void AngryBrows(int ex, int ey)
    {
        F(ex - 1, ey - 2, K); F(ex, ey - 2, K); F(ex + 1, ey - 1, K);
        F(ex + 7, ey - 2, K); F(ex + 6, ey - 2, K); F(ex + 5, ey - 1, K);
    }

    // ---------------------------------------------------------------- effets

    static void G1(string[] glyph, int sx, int sy) => Glyphs.Draw(c, glyph, MX(sx), MY(sy));

    static void Effects(int hx, int hy)
    {
        for (int i = 0; i < v.FxCount; i++)
        {
            var e = v.Fx[i];
            float p = e.Phase;
            switch (e.Kind)
            {
                case FxKind.Zzz:
                    int rise = (int)(p * 4);
                    G1(Glyphs.SmallZ, hx + 5, hy - 9 - rise);
                    if (p > 0.25f) G1(Glyphs.BigZ, hx + 10, hy - 14 - rise);
                    break;
                case FxKind.Bubble:
                    Bubble(MX(hx + 7), MY(hy + 2), 0.8f + e.Value * 2.4f);
                    break;
                case FxKind.Hearts:
                    int n = Math.Max(1, (int)e.Value);
                    for (int k = 0; k < n; k++)
                    {
                        float ph = (p + k / (float)n) % 1f;
                        G1(k == 0 ? Glyphs.Heart : Glyphs.SmallHeart, hx + 6 + k * 4, hy - 8 - (int)(ph * 9));
                    }
                    break;
                case FxKind.Anger:
                    G1(Glyphs.Anger, hx + 6, hy - 10 - (p > 0.5f ? 1 : 0));
                    break;
                case FxKind.Steam:
                    if (p < 0.85f)
                    {
                        G1(Glyphs.Puff, hx - 7, hy - 6 - (int)(p * 5));
                        G1(Glyphs.Puff, hx + 8, hy - 6 - (int)(p * 5));
                    }
                    break;
                case FxKind.Exclaim:
                    G1(Glyphs.Exclaim, hx + 5, hy - 13 - (p < 0.3f ? 1 : 0));
                    break;
                case FxKind.Question:
                    G1(Glyphs.Question, hx + 5, hy - 14);
                    break;
                case FxKind.Notes:
                    int nn = Math.Max(1, (int)e.Value);
                    for (int k = 0; k < nn; k++)
                    {
                        float ph = (p + k / (float)nn) % 1f;
                        G1(Glyphs.Note, hx + 7 + (int)MathF.Round(MathF.Sin(ph * 6.28f) * 2) + k * 3, hy - 8 - (int)(ph * 9));
                    }
                    break;
                case FxKind.Sweat:
                    G1(Glyphs.Sweat, hx - 6, hy - 7 + (int)(p * 3));
                    break;
                case FxKind.Stars:
                    for (int k = 0; k < 3; k++)
                    {
                        float a = p * 6.283f + k * 2.094f;
                        G1(Glyphs.SmallStar, hx + (int)MathF.Round(MathF.Cos(a) * 8), hy - 9 + (int)MathF.Round(MathF.Sin(a) * 2.5f));
                    }
                    break;
                case FxKind.Food:
                    FoodHeld(hx, hy, e.Value);
                    break;
                case FxKind.Crumbs:
                    Crumb(hx + 5, hy + 5 + (int)(p * 5));
                    Crumb(hx + 8, hy + 6 + (int)(((p + .4f) % 1f) * 4));
                    Crumb(hx + 6, hy + 7 + (int)(((p + .7f) % 1f) * 3));
                    break;
                case FxKind.Dream:
                    G1(Glyphs.TinyBubble, hx + 4, hy - 9);
                    c.Set(MX(hx + 6), MY(hy - 12), K, Layer.Fx);
                    G1(Glyphs.ThoughtBubble, hx + 9, hy - 18);
                    G1(v.DreamOf switch { Snack.Strawberry => Glyphs.Strawberry, Snack.Heart => Glyphs.Heart, _ => Glyphs.Lettuce },
                       hx + 9, hy - 18);
                    break;
                case FxKind.Grumble:
                    G1(Glyphs.Cloud, hx + 8 + (p < 0.5f ? 0 : 1), hy - 10);
                    break;
                case FxKind.Dust:
                    int spread = (int)(p * 4);
                    G1(Glyphs.Dust, 3 - spread, 31);
                    G1(Glyphs.Dust, 30 + spread, 31);
                    break;
                case FxKind.Sparkles:
                    for (int k = 0; k < 5; k++)
                    {
                        // étincelles qui s'allument tour à tour autour du corps
                        float ph = (p + k * 0.2f) % 1f;
                        if (ph > 0.6f) continue;
                        int sx = (int)(18 + MathF.Cos(k * 1.9f) * 20), sy = (int)(18 + MathF.Sin(k * 1.9f) * 12);
                        G1(Glyphs.SmallStar, sx, sy - (int)(ph * 3));
                    }
                    break;
                case FxKind.Tear:
                    G1(Glyphs.Tear, hx - 1, hy + 3 + (int)(p * 4));
                    break;
                case FxKind.Speed:
                    uint lc = PixelCanvas.Rgb(175, 175, 188);
                    int off = (int)(p * 3);
                    for (int k = 0; k < 3; k++)
                        for (int x = 0; x < 4; x++)
                            c.Set(MX(-2 - x - off - k % 2 * 2), MY(18 + k * 4), lc, Layer.Fx);
                    break;
            }
        }
    }

    static void Crumb(int sx, int sy) => c.Set(MX(sx), MY(sy), PixelCanvas.Rgb(205, 160, 90), Layer.Fx);

    static void Bubble(int cx, int cy, float r)
    {
        uint fill = PixelCanvas.Rgb(200, 235, 252), rim = PixelCanvas.Rgb(110, 180, 230);
        int ir = (int)MathF.Ceiling(r);
        for (int y = -ir; y <= ir; y++)
            for (int x = -ir; x <= ir; x++)
            {
                float d = MathF.Sqrt((x + .5f) * (x + .5f) + (y + .5f) * (y + .5f));
                if (d <= r) c.Set(cx + x, cy + y, d > r - 1 ? rim : fill, Layer.Fx);
            }
        if (r > 1.5f) c.Set(cx - 1, cy - 1, Glyphs.White, Layer.Fx);
    }

    static void FoodHeld(int hx, int hy, float remaining)
    {
        var glyph = Glyphs.ForItem(v.Food);
        int w = glyph[0].Length;
        int edge = MX(hx + 7);                   // bord de la bouche
        int x0 = v.FacingRight ? edge : edge - w + 1;
        int keep = (int)MathF.Ceiling(w * Math.Clamp(remaining, 0, 1));
        // on croque du côté de la bouche
        if (v.FacingRight) Glyphs.DrawAt(c, glyph, x0, MY(hy + 1), Layer.Fx, colFrom: w - keep);
        else Glyphs.DrawAt(c, glyph, x0, MY(hy + 1), Layer.Fx, colTo: keep);
    }

    // ---------------------------------------------------------------- icône

    /// <summary>Tête seule, pour l'icône de la zone de notification (16×16).</summary>
    public static void DrawIcon(PixelCanvas ic)
    {
        ic.Clear();
        const float cx = 8, cy = 8.5f, r = 6.6f;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float ddx = x + .5f - cx, ddy = y + .5f - cy;
                if (ddx * ddx + ddy * ddy <= r * r) ic.Set(x, y, G, Layer.Head);
            }
        ic.Set(6, 3, GD, Layer.Head); ic.Set(8, 3, GD, Layer.Head); ic.Set(7, 4, GD, Layer.Head); ic.Set(9, 4, GD, Layer.Head);
        ic.Outline(K);
        for (int y = 7; y <= 9; y++) { ic.Set(5, y, K, Layer.Head); ic.Set(10, y, K, Layer.Head); }
        ic.Set(5, 7, White, Layer.Head); ic.Set(10, 7, White, Layer.Head);
        ic.Set(3, 10, Pink, Layer.Head); ic.Set(12, 10, Pink, Layer.Head);
        ic.Set(7, 11, K, Layer.Head); ic.Set(8, 12, K, Layer.Head); ic.Set(9, 11, K, Layer.Head);
    }
}
