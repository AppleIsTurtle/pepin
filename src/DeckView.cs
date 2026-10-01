namespace Pepin;

/// <summary>
/// La collection d'une tortue (la nôtre ou celle d'une amie de la bande) : une carte en pixel-art, 16 cases,
/// les objets qu'elle n'a pas encore en silhouette. Un petit ★ signale ce qu'elle a et que nous n'avons pas.
/// Un clic ferme la carte.
/// </summary>
public sealed class DeckView
{
    const int Cols = 4, CellW = 28, CellH = 26, Pad = 6, HeaderH = 28;
    static readonly int Rows = (Enum.GetValues<Item>().Length + Cols - 1) / Cols;
    static readonly int CW = Cols * CellW + Pad * 2, CH = HeaderH + Rows * CellH + 22;
    const double AutoClose = 60;

    readonly Overlay ov = new(clickable: true);
    PixelCanvas? canvas;
    double shownAt;

    public bool Visible => ov.Visible;

    public void Create(nint inst)
    {
        ov.Create(inst);
        ov.Click = Hide;
    }

    public void Hide() => ov.Hide();

    /// <summary>Se ferme tout seul au bout d'une minute.</summary>
    public void Tick(double now)
    {
        if (ov.Visible && now - shownAt > AutoClose) Hide();
    }

    /// <param name="mine">Notre collection, pour repérer ce que l'autre a et pas nous (null = c'est la nôtre).</param>
    public void Show(string name, string subtitle, IReadOnlyDictionary<string, int> collection,
                     IReadOnlyDictionary<string, int>? mine, RECT work, int scale)
    {
        canvas = Render(name, subtitle, collection, mine);
        int x = work.Left + (work.Right - work.Left - CW * scale) / 2;
        int y = work.Top + (work.Bottom - work.Top - CH * scale) / 2;
        ov.Present(canvas, scale, x, y);
        shownAt = App.Now;
    }

    /// <summary>Dessine la carte (aussi utilisé par l'outil de dev --preview).</summary>
    public static PixelCanvas Render(string name, string subtitle, IReadOnlyDictionary<string, int> col, IReadOnlyDictionary<string, int>? mine)
    {
        var c = new PixelCanvas(CW, CH);
        uint ink = Glyphs.K, paper = PixelCanvas.Rgb(250, 246, 235), cell = PixelCanvas.Rgb(236, 228, 208);
        uint ghost = PixelCanvas.Rgb(208, 200, 180), dim = PixelCanvas.Rgb(120, 112, 100);

        // carte : fond crème, bord sombre, coins coupés
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
            {
                bool corner = (x == 0 || x == CW - 1) && (y == 0 || y == CH - 1);
                if (corner) continue;
                bool edge = x == 0 || y == 0 || x == CW - 1 || y == CH - 1;
                c.Set(x, y, edge ? ink : paper, Layer.Fx);
            }

        PixelFont.DrawCentered(c, name, CW / 2, 9, ink);
        PixelFont.DrawCentered(c, subtitle, CW / 2, 19, dim);

        var items = Enum.GetValues<Item>();
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            int n = col.GetValueOrDefault(Items.Id(it));
            int cx = Pad + (i % Cols) * CellW, cy = HeaderH + (i / Cols) * CellH;
            for (int y = 1; y < CellH - 1; y++)
                for (int x = 1; x < CellW - 1; x++) c.Set(cx + x, cy + y, cell, Layer.Fx);

            var g = Glyphs.ForItem(it);
            if (n > 0)
            {
                Glyphs.Draw(c, g, cx + CellW / 2, cy + 9);
                PixelFont.DrawCentered(c, "×" + n, cx + CellW / 2, cy + 17, ink);
                if (mine is not null && mine.GetValueOrDefault(Items.Id(it)) == 0)
                    Glyphs.Draw(c, Glyphs.SmallStar, cx + CellW - 5, cy + 4);
            }
            else
            {
                // silhouette : on devine ce qu'il reste à trouver
                int gw = g[0].Length, gh = g.Length, x0 = cx + CellW / 2 - gw / 2, y0 = cy + 9 - gh / 2;
                for (int y = 0; y < gh; y++)
                    for (int x = 0; x < gw; x++)
                        if (g[y][x] != '.') c.Set(x0 + x, y0 + y, ghost, Layer.Fx);
                PixelFont.DrawCentered(c, "-", cx + CellW / 2, cy + 17, ghost);
            }
        }

        int have = 0;
        foreach (var it in items) if (col.GetValueOrDefault(Items.Id(it)) > 0) have++;
        PixelFont.DrawCentered(c, $"{have}/{items.Length} DECOUVERTS", CW / 2, CH - 19, ink);
        PixelFont.DrawCentered(c, "CLIC POUR FERMER", CW / 2, CH - 10, dim);
        return c;
    }
}
