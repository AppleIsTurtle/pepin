using static Pepin.PixelCanvas;
using static Pepin.ArtKit;

namespace Pepin;

/// <summary>
/// L'axolotl : corps rose allongé, grosse tête ronde au grand sourire, trois branchies frangées de chaque côté
/// (dressées et rapides quand il est content, tombantes quand il est triste, plaquées quand il est fâché).
/// Éveillé, il flotte un peu au-dessus du sol en ondulant ; repli = enroulé sur lui-même.
/// </summary>
public static class AxolotlArt
{
    static readonly uint Pinkb = Rgb(250, 184, 204), PinkD = Rgb(226, 140, 170), Belly = Rgb(255, 222, 230),
        Gill = Rgb(236, 86, 140), GillD = Rgb(192, 54, 104), Fin = Rgb(255, 214, 226);

    static uint skin, skinD, belly, fin;
    static int fhx, fhy;
    static readonly Action<int, int> faceAt = (_, _) => Face(fhx, fhy);

    public static void Draw(PixelCanvas canvas, Visual vis)
    {
        bool curled = vis.InShell;
        int tuck = curled ? 2 : Math.Clamp(vis.LegsTuck, 0, 3);
        // éveillé et pas tenu : il flotte, avec une lente ondulation
        bool floating = !curled && tuck < 3 && !vis.LegsDangle;
        int lift = floating ? 3 + (int)MathF.Round(MathF.Sin(vis.AnimFrame * 0.45f)) : 0;
        Begin(canvas, vis, vis.BodyDy + tuck - lift);
        skin = Tinted(Pinkb); skinD = Tinted(PinkD); belly = Tinted(Belly); fin = Tinted(Fin);

        if (curled) { Curl(); Finish(false, 22, 18); return; }

        float o = v.HeadOut;
        int hx = 28 + v.HeadDx - (int)MathF.Round((1 - o) * 3), hy = 22 + v.HeadDy + (int)MathF.Round((1 - o) * 2);
        Tail();
        Legs(tuck, floating);
        Oval(15f, 27f, 11.5f, 4.2f, skin, Layer.Shell);
        for (int x = 6; x <= 24; x++) B(x, 30, belly, Layer.Shell);
        for (int x = 6; x <= 19; x++) B(x, 23, fin, Layer.Shell);          // crête dorsale
        Gills(hx, hy, near: false);
        Head(hx, hy);
        Gills(hx, hy, near: true);
        fhx = hx; fhy = hy;
        Finish(o > 0.15f, hx, hy, faceAt);
    }

    static void Tail()
    {
        int sway = (v.AnimFrame / 3) % 2;
        for (int x = -3; x <= 5; x++)
        {
            float k = (x + 3) / 8f;                       // 0 au bout, 1 à la base
            int half = (int)MathF.Round(1 + k * 3);
            int cy = 26 + (x < 1 ? sway : 0);
            for (int y = cy - half; y <= cy + half; y++) B(x, y, y == cy - half || y == cy + half ? fin : skin, Layer.Shell);
        }
    }

    static void Legs(int tuck, bool floating)
    {
        int p = v.LegPhase & 3;
        int bot = floating ? 31 : 32 - tuck;
        int a = p == 1 ? 1 : 0, b = p == 3 ? 1 : 0;
        if (v.LegsDangle) bot = 34;
        Rect(5 - a, 28, 7 - a, bot, skinD, Layer.Leg); B(4 - a, bot, skinD, Layer.Leg);
        Rect(19 + b, 28, 21 + b, bot, skinD, Layer.Leg); B(22 + b, bot, skinD, Layer.Leg);
    }

    static void Head(int hx, int hy)
    {
        int flushLine = v.Flush > 0 ? hy - 6 + (int)MathF.Round(13 * v.Flush) : int.MinValue;
        for (int y = hy - 7; y <= hy + 7; y++)
            for (int x = hx - 9; x <= hx + 9; x++)
            {
                float ex = (x + .5f - (hx + .5f)) / 8.4f, ey = (y + .5f - (hy + .5f)) / 6.6f;
                if (ex * ex + ey * ey <= 1) B(x, y, y < flushLine ? FlushCol : y > hy + 4 ? belly : skin, Layer.Head);
            }
    }

    /// <summary>
    /// Trois branchies de chaque côté de la tête : celles du côté proche partent de l'arrière de la tête (dessinées
    /// par-dessus), celles du côté loin dépassent du sommet. Leur angle et leur battement disent l'humeur.
    /// </summary>
    static void Gills(int hx, int hy, bool near)
    {
        // angles en degrés : 180 = vers l'arrière, 270 = vers le haut
        float baseDeg, spreadDeg, ampDeg, speed;
        int len = 7;
        if (v.Eyes is Eyes.Angry || v.Flush > 0.2f) { baseDeg = 185; spreadDeg = 6; ampDeg = 2; speed = 1.5f; len = 5; }
        else if (v.Eyes is Eyes.Happy or Eyes.Hearts || v.Mouth == Mouth.Grin) { baseDeg = 232; spreadDeg = 22; ampDeg = 12; speed = 1.4f; }
        else if (v.Mouth is Mouth.Frown or Mouth.Pout || v.Has(FxKind.Tear) || v.Has(FxKind.Grumble)) { baseDeg = 172; spreadDeg = 18; ampDeg = 3; speed = 0.3f; }
        else if (v.Eyes == Eyes.Closed) { baseDeg = 190; spreadDeg = 20; ampDeg = 3; speed = 0.3f; }
        else { baseDeg = 212; spreadDeg = 22; ampDeg = 7; speed = 0.7f; }

        uint main = Tinted(near ? Gill : GillD), frill = Tinted(near ? GillD : Gill);
        for (int i = 0; i < 3; i++)
        {
            float deg = near ? baseDeg + (i - 1) * spreadDeg : 250 + (i - 1) * 16 + (baseDeg - 212) * 0.6f;
            deg += MathF.Sin(v.AnimFrame * speed + i * 1.3f + (near ? 0 : 2)) * ampDeg;
            float a = deg * MathF.PI / 180, dx = MathF.Cos(a), dy = MathF.Sin(a);
            int ox = near ? hx - 8 : hx - 3 + i * 3, oy = near ? hy - 3 + i * 3 : hy - 5;
            int n = near ? len : len - 2;
            for (int l = 1; l <= n; l++)
            {
                int x = ox + (int)MathF.Round(dx * l), y = oy + (int)MathF.Round(dy * l);
                B(x, y, main, Layer.Shell);
                // franges de part et d'autre, une sur deux
                if (near && l >= 2 && l % 2 == 0)
                {
                    int px = (int)MathF.Round(-dy), py = (int)MathF.Round(dx);
                    B(x - px, y - py, frill, Layer.Shell);
                }
            }
        }
    }

    /// <summary>Petits yeux noirs, grand sourire, joues roses.</summary>
    static void Face(int hx, int hy)
    {
        int ex = hx - 3 + v.LookX, ey = hy - 2 + v.LookY;
        if (v.Blush) { F(ex - 2, ey + 4, Pink); F(ex - 1, ey + 4, Pink); F(ex + 9, ey + 4, Pink); F(ex + 10, ey + 4, Pink); }
        else { F(ex - 2, ey + 4, Pink); F(ex + 10, ey + 4, Pink); }
        Eyes2(ex, ey, 8);
        int mx = hx + 1 + v.LookX, my = hy + 3;
        if (v.Mouth == Mouth.Smile)
        {
            // le grand sourire de l'axolotl
            F(mx - 4, my - 1, K); FRect(mx - 3, my, mx - 2, my, K); FRect(mx - 1, my + 1, mx + 1, my + 1, K);
            FRect(mx + 2, my, mx + 3, my, K); F(mx + 4, my - 1, K);
        }
        else MouthAt(mx, my);
    }

    static void Curl()
    {
        float a0 = (v.SpinFrame & 3) * 1.5708f;
        Disc(16f, 24f, 8.5f, skin, Layer.Shell);
        // la queue qui s'enroule (ligne plus claire en spirale)
        for (int i = 0; i < 30; i++)
        {
            float a = a0 + i * 0.2f, r = 7.5f - i * 0.17f;
            B((int)MathF.Round(16 + MathF.Cos(a) * r), (int)MathF.Round(24 + MathF.Sin(a) * r), fin, Layer.Shell);
        }
        // branchies qui dépassent
        float ga = a0 - 1.2f;
        for (int i = 0; i < 3; i++)
        {
            float a = ga + (i - 1) * 0.35f;
            for (int l = 7; l <= 11; l++) B((int)MathF.Round(16 + MathF.Cos(a) * l), (int)MathF.Round(24 + MathF.Sin(a) * l), Tinted(Gill), Layer.Shell);
        }
    }

    public static void DrawIcon(PixelCanvas ic)
    {
        ic.Clear();
        foreach (var (x0, y0, dx, dy) in new[] { (3, 6, -1, -1), (3, 9, -1, 0), (3, 12, -1, 1), (12, 6, 1, -1), (12, 9, 1, 0), (12, 12, 1, 1) })
            for (int l = 0; l < 3; l++) ic.Set(x0 + dx * l, y0 + dy * l, Gill, Layer.Shell);
        IconDisc(ic, 8, 9.5f, 5.8f, Pinkb);
        ic.Outline(K);
        ic.Set(5, 8, K, Layer.Head); ic.Set(5, 9, K, Layer.Head); ic.Set(10, 8, K, Layer.Head); ic.Set(10, 9, K, Layer.Head);
        ic.Set(4, 11, Pink, Layer.Head); ic.Set(11, 11, Pink, Layer.Head);
        ic.Set(5, 11, K, Layer.Head); ic.Set(6, 12, K, Layer.Head); ic.Set(7, 12, K, Layer.Head); ic.Set(8, 12, K, Layer.Head);
        ic.Set(9, 12, K, Layer.Head); ic.Set(10, 11, K, Layer.Head);
    }
}
