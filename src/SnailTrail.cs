namespace Pepin;

/// <summary>
/// Escargot : traînée argentée laissée au sol quand il avance, qui s'efface en quelques secondes. Une seule petite
/// fenêtre traversable sous lui, redessinée à faible cadence, cachée dès qu'il n'y a plus rien à montrer.
/// </summary>
public sealed class SnailTrail
{
    const double Life = 7, Every = 0.2, Redraw = 0.15;

    readonly Overlay ov = new();
    readonly List<(double x, double y, double t)> pts = [];
    PixelCanvas? canvas;
    double lastPt = -10, lastDraw = -10;

    public void Create(nint inst) => ov.Create(inst);

    public void Clear()
    {
        pts.Clear();
        ov.Hide();
    }

    public void Tick(double now, Pet p, int scale, nint below, bool visible)
    {
        if (p.Species != Species.Escargot || !visible) { if (pts.Count > 0 || ov.Visible) Clear(); return; }

        // il laisse une trace seulement quand il glisse sur son pied, au sol
        bool crawling = p.Z <= 0 && !p.V.InShell && p.V.HeadOut > 0.15f && !p.Dragging && p.PerchHwnd == 0 && !p.OffScreen;
        if (crawling && now - lastPt >= Every)
        {
            // le bout de la queue : 20 pixels logiques derrière les pieds
            double tx = p.X + (p.V.FacingRight ? -20 : 20) * scale, ty = p.Y - 1 * scale;
            if (pts.Count == 0 || Math.Abs(pts[^1].x - tx) + Math.Abs(pts[^1].y - ty) >= scale) pts.Add((tx, ty, now));
            lastPt = now;
        }
        pts.RemoveAll(q => now - q.t > Life);
        if (pts.Count < 2) { if (ov.Visible) ov.Hide(); return; }
        if (now - lastDraw < Redraw) return;
        lastDraw = now;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var q in pts) { minX = Math.Min(minX, q.x); minY = Math.Min(minY, q.y); maxX = Math.Max(maxX, q.x); maxY = Math.Max(maxY, q.y); }
        int w = (int)Math.Ceiling((maxX - minX) / scale) + 5, h = (int)Math.Ceiling((maxY - minY) / scale) + 5;
        if (canvas is null || canvas.W != w || canvas.H != h) canvas = new PixelCanvas(w, h);
        canvas.Clear();
        int ox = (int)Math.Floor(minX / scale) - 2, oy = (int)Math.Floor(minY / scale) - 2;
        for (int i = 1; i < pts.Count; i++)
        {
            var (x0, y0, t0) = pts[i - 1];
            var (x1, y1, _) = pts[i];
            double age = (now - t0) / Life;
            int a = (int)(150 * (1 - age));
            if (a <= 8) continue;
            uint col = PixelCanvas.Argb(a, 205, 222, 238);
            int ax = (int)Math.Round(x0 / scale) - ox, ay = (int)Math.Round(y0 / scale) - oy;
            int bx = (int)Math.Round(x1 / scale) - ox, by = (int)Math.Round(y1 / scale) - oy;
            int n = Math.Max(1, Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)));
            for (int k = 0; k <= n; k++)
            {
                int x = ax + (bx - ax) * k / n, y = ay + (by - ay) * k / n;
                canvas.Set(x, y, col, Layer.Fx);
                canvas.Set(x, y + 1, col, Layer.Fx);
                // petites étincelles qui s'allument tour à tour
                if (((x * 7 + y * 3 + (int)(now * 4)) % 23) == 0) canvas.Set(x, y, PixelCanvas.Argb(Math.Min(255, a + 90), 255, 255, 255), Layer.Fx);
            }
        }
        ov.Present(canvas, scale, ox * scale, oy * scale, below);
    }
}
