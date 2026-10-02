using static Pepin.PixelCanvas;

namespace Pepin;

/// <summary>
/// Outils de dessin partagés par toutes les espèces : repère sprite, visage, effets, ombre, nourriture tenue.
/// Espace "sprite" : animal tourné vers la droite, pieds au pixel (22, 33), placés en (AX, AY) dans la toile.
/// Le dessin d'une espèce appelle <see cref="Begin"/>, dessine son corps (la tête sur <see cref="Layer.Head"/>,
/// pour que le visage s'y pose), puis <see cref="Finish"/>.
/// </summary>
public static class ArtKit
{
    public const int CW = 56, CH = 54;   // taille de la toile
    public const int AX = 28, AY = 40;   // position des pieds dans la toile
    public const int SX = 22, SY = 33;   // position des pieds dans l'espace sprite

    public static readonly uint ShadowCol = Argb(70, 20, 20, 30);
    public static readonly uint K = Glyphs.K, White = Glyphs.White, Red = Glyphs.Red, Pink = Glyphs.Pink;
    public static readonly uint FlushCol = Rgb(238, 92, 80), FlushDark = Rgb(196, 58, 56);

    // état du dessin en cours (un seul thread)
    public static PixelCanvas c = null!;
    public static Visual v = null!;
    public static int dy;

    public static void Begin(PixelCanvas canvas, Visual vis, int bodyDy)
    {
        c = canvas; v = vis; dy = bodyDy;
        c.Clear();
    }

    /// <summary>Contour, ombre, visage et effets. Sans tête visible, (hx, hy) est le point d'ancrage des effets.</summary>
    public static void Finish(bool head, int hx, int hy, Action<int, int>? face = null, int shadowCx = 18, float shadowRx = 13f)
    {
        c.Outline(K);
        if (!v.NoShadow) Shadow(shadowCx, shadowRx);
        if (head) (face ?? Face)(hx, hy);
        Effects(hx, hy);
    }

    /// <summary>Couleur de peau teintée par l'humeur (pâle en dormant…).</summary>
    public static uint Tinted(uint col) => Lerp(col, v.Tint, v.TintAmount);

    public static int MX(int sx) => v.FacingRight ? AX + (sx - SX) + v.BodyDx : AX - (sx - SX) - v.BodyDx;
    public static int MY(int sy) => AY + (sy - SY) + dy;
    public static void B(int sx, int sy, uint col, Layer l) => c.Set(MX(sx), MY(sy), col, l);

    public static void Rect(int x0, int y0, int x1, int y1, uint col, Layer l)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) B(x, y, col, l);
    }

    /// <summary>Disque plein (centre au milieu du pixel), utile pour les têtes rondes.</summary>
    public static void Disc(float cx, float cy, float r, uint col, Layer l)
    {
        for (int y = (int)MathF.Floor(cy - r); y <= (int)MathF.Ceiling(cy + r); y++)
            for (int x = (int)MathF.Floor(cx - r); x <= (int)MathF.Ceiling(cx + r); x++)
            {
                float ddx = x + .5f - cx, ddy = y + .5f - cy;
                if (ddx * ddx + ddy * ddy <= r * r) B(x, y, col, l);
            }
    }

    /// <summary>Ellipse pleine (sx, sy en espace sprite).</summary>
    public static void Oval(float cx, float cy, float rx, float ry, uint col, Layer l)
    {
        for (int y = (int)MathF.Floor(cy - ry); y <= (int)MathF.Ceiling(cy + ry); y++)
            for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++)
            {
                float ex = (x + .5f - cx) / rx, ey = (y + .5f - cy) / ry;
                if (ex * ex + ey * ey <= 1) B(x, y, col, l);
            }
    }

    /// <summary>Pixel du visage : seulement là où la tête est visible.</summary>
    public static void F(int sx, int sy, uint col)
    {
        int x = MX(sx), y = MY(sy);
        if (c.LayerAt(x, y) == Layer.Head) c.Set(x, y, col, Layer.Head);
    }

    public static void FRect(int x0, int y0, int x1, int y1, uint col)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) F(x, y, col);
    }

    public static void Shadow(int centerSx = 18, float baseRx = 13f)
    {
        int z = v.ShadowZ;
        if (z > CH - AY - 3) return;
        float rx = baseRx - z / 3f, ry = 1.6f;
        float cx = MX(centerSx) + .5f, cy = AY + z + .5f;
        for (int y = (int)(cy - 3); y <= (int)(cy + 3); y++)
            for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
            {
                float ex = (x + .5f - cx) / rx, ey = (y + .5f - cy) / ry;
                if (ex * ex + ey * ey <= 1 && c.LayerAt(x, y) == Layer.None && c.In(x, y))
                    c.Set(x, y, ShadowCol, Layer.Shadow);
            }
    }

    // ---------------------------------------------------------------- visage

    /// <summary>Visage standard : yeux en (hx-2, hy-1) et (hx+3, hy-1), bouche en (hx+1, hy+3).</summary>
    public static void Face(int hx, int hy) => Face(hx, hy, 5, 4, true);

    /// <summary>Visage paramétré : écart entre les yeux, distance yeux-bouche, joues ou non.</summary>
    public static void Face(int hx, int hy, int eyeGap, int mouthDy, bool cheeks)
    {
        int ex = hx - 2 + v.LookX, ey = hy - 1 + v.LookY;
        int mx = hx + 1 + v.LookX, my = hy - 1 + mouthDy;

        if (cheeks)
        {
            F(ex - 2, ey + 3, Pink); F(ex + eyeGap + 2, ey + 3, Pink);
            if (v.Blush) { F(ex - 1, ey + 3, Pink); F(ex + eyeGap + 3, ey + 3, Pink); F(ex - 2, ey + 4, Pink); F(ex + eyeGap + 2, ey + 4, Pink); }
        }

        Eyes2(ex, ey, eyeGap);
        MouthAt(mx, my);
    }

    /// <summary>Les deux yeux, le gauche en (ex, ey), le droit à ex + gap.</summary>
    public static void Eyes2(int ex, int ey, int gap)
    {
        var eyes = v.Eyes;
        if (v.Blink && eyes is Eyes.Normal or Eyes.Wide or Eyes.Determined or Eyes.HalfLid or Eyes.Angry)
        {
            FRect(ex, ey + 2, ex + 1, ey + 2, K); FRect(ex + gap, ey + 2, ex + gap + 1, ey + 2, K);
            if (eyes == Eyes.Angry) AngryBrows(ex, ey, gap);
            return;
        }
        for (int side = 0; side < 2; side++) OneEye(ex + side * gap, ey, side);
        if (eyes == Eyes.Angry) AngryBrows(ex, ey, gap);
    }

    public static void OneEye(int x, int ey, int side)
    {
        switch (v.Eyes)
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

    public static void MouthAt(int mx, int my)
    {
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

    public static void AngryBrows(int ex, int ey, int gap = 5)
    {
        F(ex - 1, ey - 2, K); F(ex, ey - 2, K); F(ex + 1, ey - 1, K);
        F(ex + gap + 2, ey - 2, K); F(ex + gap + 1, ey - 2, K); F(ex + gap, ey - 1, K);
    }

    // ---------------------------------------------------------------- effets

    public static void G1(string[] glyph, int sx, int sy) => Glyphs.Draw(c, glyph, MX(sx), MY(sy));

    public static void Effects(int hx, int hy)
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
                case FxKind.Impact:
                    Impact(hx + 5, hy - 2, p, e.Value);
                    break;
                case FxKind.Tear:
                    G1(Glyphs.Tear, hx - 1, hy + 3 + (int)(p * 4));
                    break;
                case FxKind.ShellHat:
                {
                    float k = e.Value;
                    int ox = (int)MathF.Round(k * 12), oy = (int)MathF.Round(-k * 10 + k * k * 22);
                    G1(Glyphs.ShellHat, hx + ox, hy - 9 + oy);
                    break;
                }
                case FxKind.Fly:
                {
                    var (fx, fy) = FlyAt(hx, hy, p);
                    c.Set(MX(fx - 1), MY(fy - 1), (int)(p * 40) % 2 == 0 ? Glyphs.White : Rgb(200, 220, 235), Layer.Fx);
                    c.Set(MX(fx + 1), MY(fy - 1), (int)(p * 40) % 2 == 0 ? Rgb(200, 220, 235) : Glyphs.White, Layer.Fx);
                    c.Set(MX(fx), MY(fy), K, Layer.Fx);
                    c.Set(MX(fx + 1), MY(fy), K, Layer.Fx);
                    break;
                }
                case FxKind.Tongue:
                {
                    var (fx, fy) = FlyAt(hx, hy, p);
                    int x0 = hx + 3, y0 = hy + 3;
                    float k = Math.Clamp(e.Value, 0, 1);
                    int x1 = x0 + (int)MathF.Round((fx - x0) * k), y1 = y0 + (int)MathF.Round((fy - y0) * k);
                    int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                    for (int j = 0; j <= steps; j++)
                    {
                        int x = x0 + (steps == 0 ? 0 : (x1 - x0) * j / steps), y = y0 + (steps == 0 ? 0 : (y1 - y0) * j / steps);
                        c.Set(MX(x), MY(y), Pink, Layer.Fx);
                    }
                    c.Set(MX(x1), MY(y1 - 1), Pink, Layer.Fx); c.Set(MX(x1 + 1), MY(y1), Pink, Layer.Fx);
                    break;
                }
                case FxKind.Speed:
                    uint lc = Rgb(175, 175, 188);
                    int off = (int)(p * 3);
                    for (int k = 0; k < 3; k++)
                        for (int x = 0; x < 4; x++)
                            c.Set(MX(-2 - x - off - k % 2 * 2), MY(18 + k * 4), lc, Layer.Fx);
                    break;
            }
        }
    }

    /// <summary>Où tourne la mouche (phase 0..1) autour de la tête.</summary>
    static (int x, int y) FlyAt(int hx, int hy, float p)
    {
        float a = p * 6.283f;
        return (hx + 11 + (int)MathF.Round(MathF.Cos(a) * 6), hy - 11 + (int)MathF.Round(MathF.Sin(a * 2) * 3));
    }

    static readonly uint Orange = Rgb(250, 140, 30), Fire = Rgb(255, 214, 64), Ember = Rgb(190, 40, 40);

    /// <summary>
    /// Choc entre deux compagnons : une explosion en étoile qui gonfle et se dissipe (blanc au cœur, jaune, orange, rouge au bord),
    /// puis des étincelles qui filent en tous sens. `p` : 0..1 pour tout le choc, `power` : taille (1 = normal).
    /// </summary>
    public static void Impact(int cx, int cy, float p, float power)
    {
        if (p >= 1f) return;
        if (p < 0.6f)
        {
            float k = p < 0.2f ? p / 0.2f : 1 - (p - 0.2f) / 0.4f;           // gonfle vite, puis se résorbe
            float big = (3f + 8f * Math.Clamp(k, 0f, 1f)) * Math.Clamp(power, 0.4f, 1.6f);
            int r = (int)MathF.Ceiling(big) + 1;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float edge = big * (0.55f + 0.45f * MathF.Abs(MathF.Cos(MathF.Atan2(dy, dx) * 4 + p * 2)));   // huit pointes
                    if (d > edge) continue;
                    float t = d / edge;
                    uint col = t < 0.3f ? Glyphs.White : t < 0.6f ? Fire : t < 0.85f ? Orange : Ember;
                    B(cx + dx, cy + dy, col, Layer.Fx);
                }
        }
        if (p > 0.15f)
        {
            float q = (p - 0.15f) / 0.85f;
            for (int j = 0; j < 8; j++)
            {
                if ((j + (int)(q * 14)) % 3 == 0) continue;                  // elles scintillent
                float a = j * 0.785f + 0.39f, r = (5 + q * 11) * Math.Clamp(power, 0.6f, 1.4f);
                int sx = cx + (int)MathF.Round(MathF.Cos(a) * r), sy = cy + (int)MathF.Round(MathF.Sin(a) * r * 0.8f);
                B(sx, sy, j % 2 == 0 ? Glyphs.White : Fire, Layer.Fx);
                if (q < 0.7f) B(sx - (int)MathF.Round(MathF.Cos(a) * 2), sy - (int)MathF.Round(MathF.Sin(a) * 1.6f), Orange, Layer.Fx);
            }
        }
    }

    public static void Crumb(int sx, int sy) => c.Set(MX(sx), MY(sy), Rgb(205, 160, 90), Layer.Fx);

    public static void Bubble(int cx, int cy, float r)
    {
        uint fill = Rgb(200, 235, 252), rim = Rgb(110, 180, 230);
        int ir = (int)MathF.Ceiling(r);
        for (int y = -ir; y <= ir; y++)
            for (int x = -ir; x <= ir; x++)
            {
                float d = MathF.Sqrt((x + .5f) * (x + .5f) + (y + .5f) * (y + .5f));
                if (d <= r) c.Set(cx + x, cy + y, d > r - 1 ? rim : fill, Layer.Fx);
            }
        if (r > 1.5f) c.Set(cx - 1, cy - 1, Glyphs.White, Layer.Fx);
    }

    public static void FoodHeld(int hx, int hy, float remaining)
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

    /// <summary>Icône 16×16 : yeux, joues et sourire posés sur une tête ronde déjà dessinée.</summary>
    public static void RoundIconFace(PixelCanvas ic)
    {
        for (int y = 7; y <= 9; y++) { ic.Set(5, y, K, Layer.Head); ic.Set(10, y, K, Layer.Head); }
        ic.Set(5, 7, White, Layer.Head); ic.Set(10, 7, White, Layer.Head);
        ic.Set(3, 10, Pink, Layer.Head); ic.Set(12, 10, Pink, Layer.Head);
        ic.Set(7, 11, K, Layer.Head); ic.Set(8, 12, K, Layer.Head); ic.Set(9, 11, K, Layer.Head);
    }

    public static void IconDisc(PixelCanvas ic, float cx, float cy, float r, uint col)
    {
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float ddx = x + .5f - cx, ddy = y + .5f - cy;
                if (ddx * ddx + ddy * ddy <= r * r) ic.Set(x, y, col, Layer.Head);
            }
    }
}
