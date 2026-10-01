using static Pepin.PixelCanvas;
using static Pepin.ArtKit;

namespace Pepin;

/// <summary>
/// La grenouille : corps rond assis, gros yeux en bosses sur le dessus, grande bouche, cuisses repliées.
/// En l'air (<see cref="Visual.ShadowZ"/>) elle étire les pattes ; repli = glissade à plat ventre ;
/// <see cref="Visual.Puffed"/> = gorge gonflée.
/// </summary>
public static class GrenouilleArt
{
    static readonly uint G = Rgb(112, 200, 92), GD = Rgb(70, 152, 62), GL = Rgb(206, 238, 164), Throat = Rgb(248, 244, 214);

    static uint g, gd, gl;

    public static void Draw(PixelCanvas canvas, Visual vis)
    {
        bool slide = vis.InShell;
        int tuck = slide ? 3 : Math.Clamp(vis.LegsTuck, 0, 3);
        Begin(canvas, vis, vis.BodyDy + tuck / 2 + (slide ? 2 : 0));
        g = Tinted(G); gd = Tinted(GD); gl = Tinted(GL);
        bool air = !slide && vis.ShadowZ > 1;

        float o = slide ? 1 : v.HeadOut;
        int hx = 26 + v.HeadDx + (slide ? 3 : 0), hy = 19 + v.HeadDy + (int)MathF.Round((1 - o) * 3) + (slide ? 4 : 0);

        if (slide) { SlideBody(); }
        else
        {
            BackLeg(air);
            Body();
            FrontLeg(air);
        }
        Head(hx, hy);
        if (v.Puffed && !slide) Oval(hx + 4.5f, hy + 5.5f, 4.2f, 3.6f, Throat, Layer.Head);
        Finish(true, hx, hy, Face);
    }

    static void Body()
    {
        Oval(16.5f, 25.5f, 11f, 7f, g, Layer.Shell);
        // ventre clair et taches du dos
        Oval(22f, 27.5f, 6f, 4f, gl, Layer.Shell);
        foreach (var (x, y) in new[] { (9, 21), (13, 20), (11, 24), (16, 22), (7, 25) }) B(x, y, gd, Layer.Shell);
    }

    static void SlideBody()
    {
        Oval(15f, 28.5f, 12f, 4.5f, g, Layer.Shell);
        Oval(18f, 30f, 8f, 2f, gl, Layer.Shell);
        // pattes étirées vers l'arrière, qui battent un peu
        int w = (v.SpinFrame & 1);
        Rect(-3, 29 + w, 4, 30 + w, gd, Layer.Leg);
        Rect(-5, 31 - w, 2, 31 - w, gd, Layer.Leg);
        Rect(26, 31, 30, 32, g, Layer.Leg);
    }

    static void BackLeg(bool air)
    {
        if (air)
        {
            // cuisse et pied tendus vers l'arrière-bas
            Oval(8f, 28f, 4f, 3f, gd, Layer.Leg);                       // cuisse
            for (int i = 0; i < 5; i++) { B(5 - i, 29 + i / 2, gd, Layer.Leg); B(5 - i, 30 + i / 2, gd, Layer.Leg); }
            Rect(-3, 32, 1, 32, gd, Layer.Leg); B(-3, 31, gd, Layer.Leg);   // pied palmé
            return;
        }
        if (v.LegsDangle)
        {
            Rect(8, 28, 10, 37, gd, Layer.Leg);
            Rect(7, 38, 11, 38, gd, Layer.Leg);
            return;
        }
        Oval(10f, 27f, 5.5f, 4.5f, gd, Layer.Leg);   // cuisse repliée
        Oval(10.5f, 26.5f, 3.5f, 2.5f, g, Layer.Leg);
        int lift = (v.LegPhase & 3) == 1 ? 1 : 0;
        Rect(4, 32 - lift, 13, 32 - lift, gd, Layer.Leg);       // long pied à plat
    }

    static void FrontLeg(bool air)
    {
        if (air) { Rect(27, 27, 28, 30, g, Layer.Leg); Rect(28, 31, 31, 31, g, Layer.Leg); return; }
        int bot = v.LegsDangle ? 35 : 31;
        int lift = (v.LegPhase & 3) == 3 ? 1 : 0;
        Rect(25, 26, 26, bot - lift, g, Layer.Leg);
        Rect(24, bot + 1 - lift, 28, bot + 1 - lift, g, Layer.Leg);
    }

    static void Head(int hx, int hy)
    {
        int flushLine = v.Flush > 0 ? hy - 9 + (int)MathF.Round(16 * v.Flush) : int.MinValue;
        uint Col(int y) => y < flushLine ? FlushCol : g;
        for (int y = hy - 10; y <= hy + 7; y++)
            for (int x = hx - 9; x <= hx + 10; x++)
            {
                float ex = (x + .5f - (hx + .5f)) / 8.6f, ey = (y + .5f - (hy + .5f)) / 6.4f;
                bool skull = ex * ex + ey * ey <= 1;
                // deux bosses pour les yeux, sur le dessus
                float b1x = x + .5f - (hx - 2.5f), b2x = x + .5f - (hx + 4.5f), by = y + .5f - (hy - 6.5f);
                bool bump = b1x * b1x + by * by <= 3.6f * 3.6f || b2x * b2x + by * by <= 3.6f * 3.6f;
                if (skull || bump) B(x, y, Col(y), Layer.Head);
            }
        // menton clair
        for (int x = hx - 2; x <= hx + 7; x++) { B(x, hy + 5, gl, Layer.Head); if (x > hx) B(x, hy + 4, gl, Layer.Head); }
    }

    /// <summary>Yeux dans les bosses (blanc + pupille), grande bouche qui traverse la tête.</summary>
    static void Face(int hx, int hy)
    {
        int lx = v.LookX, ly = v.LookY;
        bool open = v.Eyes is Eyes.Normal or Eyes.Wide or Eyes.Determined or Eyes.Angry && !v.Blink;
        for (int side = 0; side < 2; side++)
        {
            int cx = side == 0 ? hx - 3 : hx + 4, cy = hy - 7;
            if (open)
                for (int y = cy - 1; y <= cy + 2; y++)
                    for (int x = cx - 1; x <= cx + 2; x++)
                        if (!((x == cx - 1 || x == cx + 2) && (y == cy - 1 || y == cy + 2))) F(x, y, White);
            if (open && v.Eyes is Eyes.Normal or Eyes.Determined)
            {
                FRect(cx + lx, cy + ly + (ly < 0 ? 1 : 0), cx + 1 + lx, cy + 1 + ly + (ly < 0 ? 1 : 0), K);
                if (v.Eyes == Eyes.Determined) FRect(cx - 1, cy - 1, cx + 2, cy - 1, K);
            }
            else if (open && v.Eyes == Eyes.Wide) { F(cx + lx, cy + ly, K); F(cx + 1 + lx, cy + ly, K); F(cx + lx, cy + 1 + ly, K); F(cx + 1 + lx, cy + 1 + ly, K); }
            else if (open && v.Eyes == Eyes.Angry) { FRect(cx, cy + 1, cx + 1, cy + 2, K); F(side == 0 ? cx - 1 : cx + 2, cy - 1, K); F(cx + (side == 0 ? 0 : 1), cy, K); }
            else if (v.Blink && v.Eyes is Eyes.Normal or Eyes.Wide or Eyes.Determined or Eyes.Angry) FRect(cx - 1, cy + 1, cx + 2, cy + 1, K);
            else OneEye(cx, cy - 1, side);
        }
        // joues
        F(hx - 5, hy + 1, Pink); F(hx + 8, hy + 1, Pink);
        if (v.Blush) { F(hx - 4, hy + 1, Pink); F(hx + 7, hy + 1, Pink); }
        int my = hy + 2;
        switch (v.Mouth)
        {
            case Mouth.Smile:
                F(hx - 3, my, K); FRect(hx - 2, my + 1, hx + 6, my + 1, K); F(hx + 7, my, K);
                break;
            case Mouth.Grin:
                F(hx - 3, my, K); FRect(hx - 2, my + 1, hx + 6, my + 1, K); F(hx + 7, my, K);
                FRect(hx - 1, my + 2, hx + 5, my + 2, Red); FRect(hx, my + 3, hx + 4, my + 3, K);
                break;
            default:
                MouthAt(hx + 2, my);
                break;
        }
    }

    public static void DrawIcon(PixelCanvas ic)
    {
        ic.Clear();
        IconDisc(ic, 8, 9.5f, 6.4f, G);
        IconDisc(ic, 4.5f, 4.5f, 3f, G);
        IconDisc(ic, 11.5f, 4.5f, 3f, G);
        ic.Outline(K);
        foreach (int cx in new[] { 4, 11 })
        {
            ic.Set(cx - 1, 4, White, Layer.Head); ic.Set(cx, 3, White, Layer.Head); ic.Set(cx + 1, 4, White, Layer.Head);
            ic.Set(cx, 4, K, Layer.Head); ic.Set(cx, 5, K, Layer.Head); ic.Set(cx + 1, 5, White, Layer.Head);
        }
        ic.Set(2, 10, Pink, Layer.Head); ic.Set(13, 10, Pink, Layer.Head);
        for (int x = 4; x <= 11; x++) ic.Set(x, 11, K, Layer.Head);
        ic.Set(3, 10, K, Layer.Head); ic.Set(12, 10, K, Layer.Head);
    }
}
