using static Pepin.PixelCanvas;
using static Pepin.ArtKit;

namespace Pepin;

/// <summary>
/// Le panda roux : roux, masque blanc, oreilles pointues, pattes sombres, grosse queue annelée relevée derrière.
/// <see cref="Visual.Stand"/> = dressé bras levés ; endormi (yeux fermés, couché) = enroulé, la queue sur le museau ;
/// repli = boule rousse entourée de sa queue.
/// </summary>
public static class PandaRouxArt
{
    static readonly uint Fur = Rgb(206, 94, 46), FurD = Rgb(164, 64, 32), Cream = Rgb(252, 244, 230), Dark = Rgb(84, 46, 34),
        RingL = Rgb(236, 164, 96), RingD = Rgb(178, 84, 42), Tip = Rgb(96, 50, 32);

    static uint fur, furD, cream, dark;
    static int fhx, fhy;
    static readonly Action<int, int> sleepFace = (_, _) => { Face(fhx, fhy); SleepTail(); };

    public static void Draw(PixelCanvas canvas, Visual vis)
    {
        bool curled = vis.InShell, stand = vis.Stand && !curled;
        bool sleeping = !curled && !stand && vis.Eyes == Eyes.Closed && vis.LegsTuck >= 3;
        int tuck = curled ? 2 : Math.Clamp(vis.LegsTuck, 0, 3);
        Begin(canvas, vis, vis.BodyDy + (stand ? 0 : tuck));
        fur = Tinted(Fur); furD = Tinted(FurD); cream = Tinted(Cream); dark = Tinted(Dark);

        if (curled) { Ball(); Finish(false, 22, 18); return; }
        if (stand) { Standing(); return; }

        float o = v.HeadOut;
        int hx = 29 + v.HeadDx - (int)MathF.Round((1 - o) * 4), hy = 19 + v.HeadDy + (int)MathF.Round((1 - o) * 3);
        if (sleeping)
        {
            hx = 27 + v.HeadDx; hy = 23 + v.HeadDy;
            Oval(15f, 27f, 12f, 5f, fur, Layer.Shell);
            Head(hx, hy);
            fhx = hx; fhy = hy;
            Finish(true, hx, hy - 2, sleepFace);
            return;
        }
        Tail(5, 23, false);
        Legs(tuck, far: true);
        Oval(15f, 24f, 11f, 6f, fur, Layer.Shell);
        Oval(17f, 28f, 8f, 2.5f, dark, Layer.Shell);                   // ventre sombre
        Legs(tuck, far: false);
        Head(hx, hy);
        Finish(o > 0.15f, hx, hy);
    }

    static readonly int[] BackStep = [-1, 0, 1, 0], FrontStep = [1, 0, -1, 0];

    static void Legs(int tuck, bool far)
    {
        int p = v.LegPhase & 3;
        int bBot = 32 - tuck - (p == 1 ? 1 : 0), fBot = 32 - tuck - (p == 3 ? 1 : 0);
        if (v.LegsDangle) { bBot = 34; fBot = 34; }
        uint col = far ? PixelCanvas.Lerp(dark, K, 0.25f) : dark;
        int shift = far ? 2 : 0;
        Rect(6 + BackStep[p] + shift, 26, 8 + BackStep[p] + shift, bBot, col, Layer.Leg);
        Rect(19 + FrontStep[p] + shift, 26, 21 + FrontStep[p] + shift, fBot, col, Layer.Leg);
    }

    static void Head(int hx, int hy)
    {
        int flushLine = v.Flush > 0 ? hy - 7 + (int)MathF.Round(15 * v.Flush) : int.MinValue;
        // oreilles pointues bordées de blanc
        for (int side = 0; side < 2; side++)
        {
            int ex = side == 0 ? hx - 5 : hx + 3;
            for (int r = 0; r < 4; r++)
                for (int x = ex - (3 - r) / 2; x <= ex + 2 - (3 - r + 1) / 2; x++)
                    B(x, hy - 9 + r, r == 0 ? cream : furD, Layer.Head);
            B(ex + 1, hy - 7, cream, Layer.Head);
        }
        Disc(hx + .5f, hy + .5f, 7.1f, fur, Layer.Head);
        for (int y = hy - 7; y <= hy + 7; y++)
            for (int x = hx - 7; x <= hx + 7; x++)
                if (y < flushLine && c.LayerAt(MX(x), MY(y)) == Layer.Head) B(x, y, FlushCol, Layer.Head);
        // masque : sourcils, joues et museau blancs, larmes sombres
        B(hx - 3, hy - 4, cream, Layer.Head); B(hx - 2, hy - 4, cream, Layer.Head);
        B(hx + 3, hy - 4, cream, Layer.Head); B(hx + 4, hy - 4, cream, Layer.Head);
        Oval(hx + 2f, hy + 3.5f, 4.6f, 2.6f, cream, Layer.Head);
        Oval(hx - 4f, hy + 2.5f, 2.2f, 2f, cream, Layer.Head);
        B(hx - 2, hy + 1, furD, Layer.Head); B(hx - 2, hy + 2, furD, Layer.Head);
        B(hx + 6, hy + 2, K, Layer.Head); B(hx + 6, hy + 3, K, Layer.Head); B(hx + 5, hy + 2, K, Layer.Head);   // truffe
    }

    static void Face(int hx, int hy) => ArtKit.Face(hx, hy, 5, 4, false);

    /// <summary>Queue annelée le long d'une courbe (Bézier quadratique), épaisse, bout sombre.</summary>
    static void TailCurve(float x0, float y0, float x1, float y1, float x2, float y2, float thick, int rings)
    {
        for (int i = 0; i <= 60; i++)
        {
            float t = i / 60f, u = 1 - t;
            float x = u * u * x0 + 2 * u * t * x1 + t * t * x2, y = u * u * y0 + 2 * u * t * y1 + t * t * y2;
            uint col = t > 0.9f ? Tip : ((int)(t * rings) % 2 == 0 ? RingL : RingD);
            float r = thick * (t < 0.15f ? 0.75f + t * 1.6f : t > 0.85f ? 1 - (t - 0.85f) * 2 : 1);
            Disc(x, y, r, Tinted(col), Layer.Shell);
        }
    }

    static void Tail(int bx, int by, bool happyWag)
    {
        int wag = v.Eyes is Eyes.Happy or Eyes.Hearts || v.Mouth == Mouth.Grin ? ((v.AnimFrame / 2) % 2 == 0 ? 1 : -1) : 0;
        TailCurve(bx, by, -2, 22, 1 + wag, 8, 3f, 7);
    }

    static void SleepTail()
    {
        // enroulée autour du corps, le bout posé sur le museau
        TailCurve(3, 29, 14, 36, fhx + 6, fhy + 3, 3f, 7);
    }

    static void Ball()
    {
        Oval(16f, 24f, 9.5f, 8.5f, fur, Layer.Shell);
        float a0 = (v.SpinFrame & 3) * 1.5708f;
        // la queue fait le tour de la boule
        for (int i = 0; i <= 40; i++)
        {
            float a = a0 + 0.6f + i / 40f * 3.6f;
            float x = 16 + MathF.Cos(a) * 9f, y = 24 + MathF.Sin(a) * 8f;
            uint col = i > 36 ? Tip : ((i / 6) % 2 == 0 ? RingL : RingD);
            Disc(x, y, 2.6f, Tinted(col), Layer.Shell);
        }
        if ((v.SpinFrame & 3) == 0)
        {
            B(21, 15, furD, Layer.Shell); B(22, 14, cream, Layer.Shell); B(24, 15, furD, Layer.Shell); B(25, 14, cream, Layer.Shell);
        }
    }

    /// <summary>Dressé sur les pattes arrière, bras levés, queue gonflée derrière.</summary>
    static void Standing()
    {
        TailCurve(15, 28, 3, 31, -1, 21, 3.4f, 6);
        Rect(15, 28, 18, 32, dark, Layer.Leg);
        Rect(21, 28, 24, 32, dark, Layer.Leg);
        Oval(19.5f, 22f, 6.5f, 8f, fur, Layer.Shell);
        Oval(20f, 25f, 4f, 5f, dark, Layer.Shell);
        // bras levés
        for (int i = 0; i < 7; i++)
        {
            Rect(12 - i * 2 / 3, 19 - i, 13 - i * 2 / 3, 19 - i, dark, Layer.Leg);
            Rect(27 + i * 2 / 3, 19 - i, 28 + i * 2 / 3, 19 - i, dark, Layer.Leg);
        }
        int hx = 20 + v.HeadDx, hy = 9 + v.HeadDy;
        Head(hx, hy);
        Finish(true, hx, hy);
    }

    public static void DrawIcon(PixelCanvas ic)
    {
        ic.Clear();
        // oreilles
        foreach (int ex in new[] { 2, 11 })
        {
            ic.Set(ex + 1, 1, Cream, Layer.Head); ic.Set(ex, 2, FurD, Layer.Head); ic.Set(ex + 1, 2, Cream, Layer.Head); ic.Set(ex + 2, 2, FurD, Layer.Head);
        }
        IconDisc(ic, 8, 9f, 6.6f, Fur);
        for (int y = 10; y <= 13; y++) for (int x = 5; x <= 10; x++) if ((x - 7.5f) * (x - 7.5f) / 9 + (y - 11.5f) * (y - 11.5f) / 4 <= 1) ic.Set(x, y, Cream, Layer.Head);
        ic.Set(4, 5, Cream, Layer.Head); ic.Set(11, 5, Cream, Layer.Head);
        ic.Outline(K);
        for (int y = 7; y <= 9; y++) { ic.Set(5, y, K, Layer.Head); ic.Set(10, y, K, Layer.Head); }
        ic.Set(5, 7, White, Layer.Head); ic.Set(10, 7, White, Layer.Head);
        ic.Set(7, 11, K, Layer.Head); ic.Set(8, 11, K, Layer.Head); ic.Set(7, 13, K, Layer.Head); ic.Set(8, 13, K, Layer.Head);
    }
}
