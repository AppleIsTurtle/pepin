using static Pepin.PixelCanvas;
using static Pepin.ArtKit;

namespace Pepin;

/// <summary>
/// L'escargot : coquille en spirale sur le dos, pied qui ondule au sol, yeux au bout des antennes (plus courtes
/// quand il est contrarié ou endormi, qui frétillent quand il est content). Repli = rentré dans sa coquille,
/// qui tourne avec <see cref="Visual.SpinFrame"/>.
/// </summary>
public static class EscargotArt
{
    static readonly uint Sh = Rgb(214, 148, 82), ShD = Rgb(146, 88, 46), ShL = Rgb(240, 192, 120), Rim = Rgb(170, 108, 58);
    static readonly uint Body = Rgb(234, 204, 184), BodyD = Rgb(206, 170, 150), Sole = Rgb(196, 156, 140);

    static uint body, bodyD, sole;
    static int fhx, fhy, flen;
    static readonly Action<int, int> faceAt = (_, _) => Face(fhx, fhy, flen);

    public static void Draw(PixelCanvas canvas, Visual vis)
    {
        float o = vis.InShell ? 0 : vis.HeadOut;
        bool hidden = vis.InShell || o < 0.15f;
        Begin(canvas, vis, vis.BodyDy + (hidden ? 2 : 0));
        body = Tinted(Body); bodyD = Tinted(BodyD); sole = Tinted(Sole);

        if (hidden)
        {
            Shell(16, 21, 10.5f, (v.SpinFrame & 3) * 1.57f, grounded: true);
            Finish(false, 22, 17);
            return;
        }

        int back = (int)MathF.Round((1 - o) * 8);                // le corps rentre dans la coquille
        int hx = 31 + v.HeadDx - back, hy = 21 + v.HeadDy + v.LegsTuck / 2;
        Foot(back);
        Neck(hx, hy);
        Shell(14, 19, 10f, 0, grounded: false);
        int stalk = StalkLength(o);
        Stalks(hx, hy, stalk);
        int eyeY = hy - 5 - stalk;
        fhx = hx; fhy = hy; flen = stalk;
        Finish(true, hx, eyeY + 2, faceAt);
    }

    /// <summary>Longueur des antennes : rentrées à moitié quand il est contrarié, surpris ou fatigué.</summary>
    static int StalkLength(float o)
    {
        int len = 7;
        if (v.Eyes is Eyes.Angry or Eyes.Squint || v.Mouth is Mouth.Frown or Mouth.Pout or Mouth.Zigzag) len = 4;
        if (v.Eyes is Eyes.Closed or Eyes.HalfLid) len = 4;
        if (v.Eyes == Eyes.Wide) len = 8;
        return (int)MathF.Round(len * Math.Clamp(o, 0.4f, 1));
    }

    static void Foot(int back)
    {
        int front = 34 - back;
        for (int x = 0; x <= front; x++)
        {
            int top = x < 5 ? 31 - x / 2 : 28;                      // la queue s'amincit
            for (int y = top; y <= 32; y++) B(x, y, y == 32 ? sole : body, Layer.Leg);
        }
        // ondulation : une vague claire qui parcourt la sole
        int w = (v.LegPhase & 3) * 2;
        for (int x = 2 + w; x < front; x += 8) { B(x, 32, bodyD, Layer.Leg); B(x + 1, 32, bodyD, Layer.Leg); }
        if (v.LegsDangle) for (int x = 0; x <= 6; x++) B(x, 33 + (6 - x) / 3, body, Layer.Leg);
    }

    static void Neck(int hx, int hy)
    {
        int flushLine = v.Flush > 0 ? hy - 5 + (int)MathF.Round(12 * v.Flush) : int.MinValue;
        uint Col(int y) => y < flushLine ? FlushCol : body;
        // cou qui monte du pied jusqu'à la tête
        for (int y = hy; y <= 31; y++)
            for (int x = hx - 4; x <= hx + 3; x++) B(x, y, Col(y), Layer.Head);
        Disc(hx + .5f, hy + .5f, 5.2f, body, Layer.Head);
        for (int y = hy - 5; y <= hy + 5; y++)
            for (int x = hx - 5; x <= hx + 6; x++)
                if (c.LayerAt(MX(x), MY(y)) == Layer.Head && y < flushLine) B(x, y, FlushCol, Layer.Head);
        // petits tentacules du bas
        B(hx + 5, hy + 3, bodyD, Layer.Head); B(hx + 6, hy + 3, bodyD, Layer.Head);
    }

    static void Stalks(int hx, int hy, int len)
    {
        int wig = v.Eyes is Eyes.Happy or Eyes.Hearts ? ((v.AnimFrame / 2) % 2 == 0 ? 1 : -1) : 0;
        for (int side = 0; side < 2; side++)
        {
            int bx = side == 0 ? hx - 2 : hx + 2, top = hy - 4 - len;
            int tipX = bx + (side == 0 ? -2 : 1) + wig * (side == 0 ? 1 : -1);
            for (int y = hy - 4; y > top; y--)
            {
                int x = bx + (tipX - bx) * (hy - 4 - y) / Math.Max(1, len);
                B(x, y, bodyD, Layer.Head);
            }
            Disc(tipX + 1f, top - .5f, 2.9f, body, Layer.Head);
        }
    }

    /// <summary>Yeux au bout des antennes, bouche et joues sur la tête.</summary>
    static void Face(int hx, int hy, int len)
    {
        int wig = v.Eyes is Eyes.Happy or Eyes.Hearts ? ((v.AnimFrame / 2) % 2 == 0 ? 1 : -1) : 0;
        for (int side = 0; side < 2; side++)
        {
            int bx = side == 0 ? hx - 2 : hx + 2, top = hy - 4 - len;
            int tipX = bx + (side == 0 ? -2 : 1) + wig * (side == 0 ? 1 : -1);
            if (v.Blink && v.Eyes is Eyes.Normal or Eyes.Wide or Eyes.Determined or Eyes.HalfLid or Eyes.Angry)
                FRect(tipX, top, tipX + 1, top, K);
            else OneEye(tipX + (v.LookX > 0 ? 1 : 0), top - 2 + (v.LookY > 0 ? 1 : 0), side);
        }
        F(hx - 3, hy + 2, Pink); F(hx + 5, hy + 1, Pink);
        if (v.Blush) { F(hx - 2, hy + 2, Pink); F(hx + 4, hy + 2, Pink); }
        MouthAt(hx + 2, hy + 1);
    }

    /// <summary>Coquille en spirale (centre et rayon en espace sprite) ; `turn` fait tourner la spirale.</summary>
    static void Shell(float cx, float cy, float r, float turn, bool grounded)
    {
        for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
            for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
            {
                float dx = x + .5f - cx, dy = y + .5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > r) continue;
                if (!grounded && y > 28) continue;                  // posée sur le pied
                float ang = MathF.Atan2(dy, dx) + turn;
                float a01 = (ang / 6.2832f) % 1f; if (a01 < 0) a01 += 1;
                float f = (d / r * 2.6f + a01) % 1f;
                uint col = d > r - 1.2f ? Rim : f < 0.2f ? ShD : f < 0.38f ? ShL : Sh;
                if (dx < -r * 0.25f && dy < -r * 0.3f && d > r * 0.55f && d < r * 0.8f) col = ShL;   // reflet
                B(x, y, col, Layer.Shell);
            }
    }

    public static void DrawIcon(PixelCanvas ic)
    {
        ic.Clear();
        // coquille à gauche, tête et antennes à droite
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = x + .5f - 6.5f, dy = y + .5f - 8.5f, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > 6f) continue;
                float a01 = ((MathF.Atan2(dy, dx) / 6.2832f) % 1f + 1) % 1f;
                float f = (d / 6f * 2.2f + a01) % 1f;
                ic.Set(x, y, f < 0.25f ? ShD : Sh, Layer.Shell);
            }
        IconDisc(ic, 12.5f, 10.5f, 3.2f, Body);
        for (int y = 12; y < 15; y++) for (int x = 5; x < 16; x++) ic.Set(x, y, Body, Layer.Leg);
        ic.Set(11, 6, BodyD, Layer.Head); ic.Set(11, 5, BodyD, Layer.Head); ic.Set(14, 6, BodyD, Layer.Head); ic.Set(14, 5, BodyD, Layer.Head);
        ic.Set(11, 3, Body, Layer.Head); ic.Set(14, 3, Body, Layer.Head); ic.Set(11, 4, Body, Layer.Head); ic.Set(14, 4, Body, Layer.Head);
        ic.Outline(K);
        ic.Set(11, 4, K, Layer.Head); ic.Set(14, 4, K, Layer.Head);
        ic.Set(12, 11, K, Layer.Head); ic.Set(13, 12, K, Layer.Head); ic.Set(14, 11, K, Layer.Head);
    }
}
