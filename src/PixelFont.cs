namespace Pepin;

/// <summary>
/// Petite police pixel-art en capitales (hauteur 7, accents au-dessus et cédille en dessous), pour les cartes
/// dessinées dans une <see cref="PixelCanvas"/> : on reste en gros pixels, sans texte GDI flou.
/// </summary>
public static class PixelFont
{
    public const int Height = 7;

    static readonly Dictionary<char, string[]> Glyph = Build();

    static Dictionary<char, string[]> Build()
    {
        var g = new Dictionary<char, string[]>();
        void G(char c, params string[] rows) => g[c] = rows;
        G('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        G('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
        G('C', ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.");
        G('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
        G('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
        G('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
        G('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###.");
        G('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        G('I', ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###.");
        G('J', "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##..");
        G('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
        G('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
        G('M', "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
        G('N', "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#");
        G('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        G('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
        G('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
        G('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
        G('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
        G('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
        G('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        G('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
        G('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
        G('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
        G('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
        G('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");
        G('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
        G('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
        G('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####");
        G('3', ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###.");
        G('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#.");
        G('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
        G('6', ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###.");
        G('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
        G('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
        G('9', ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###.");
        G('.', ".", ".", ".", ".", ".", ".", "#");
        G(',', "..", "..", "..", "..", "..", ".#", "#.");
        G(':', ".", "#", ".", ".", ".", "#", ".");
        G('!', "#", "#", "#", "#", "#", ".", "#");
        G('\'', "#", "#", ".", ".", ".", ".", ".");
        G('-', "...", "...", "...", "###", "...", "...", "...");
        G('/', "....#", "....#", "...#.", "..#..", ".#...", "#....", "#....");
        G('?', ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..");
        G('×', ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", ".....");
        G(' ', "...", "...", "...", "...", "...", "...", "...");
        G('(', ".#", "#.", "#.", "#.", "#.", "#.", ".#");
        G(')', "#.", ".#", ".#", ".#", ".#", ".#", "#.");
        G('+', ".....", ".....", "..#..", ".###.", "..#..", ".....", ".....");
        G('%', "##..#", "##.#.", "...#.", "..#..", ".#...", ".#.##", "#..##");
        G('&', ".##..", "#..#.", ".##..", ".#...", "#.#.#", "#..#.", ".##.#");
        G('"', "#.#", "#.#", "...", "...", "...", "...", "...");
        G('…', ".....", ".....", ".....", ".....", ".....", ".....", "#.#.#");
        G('«', "......", "......", "..#..#", ".#..#.", "..#..#", "......", "......");
        G('»', "......", "......", "#..#..", ".#..#.", "#..#..", "......", "......");
        G('·', ".", ".", ".", "#", ".", ".", ".");
        G('_', ".....", ".....", ".....", ".....", ".....", ".....", "#####");
        G('#', ".#.#.", "#####", ".#.#.", ".#.#.", "#####", ".#.#.", ".....");
        G('=', "...", "...", "###", "...", "###", "...", "...");
        G('>', "#..", ".#.", "..#", ".#.", "#..", "...", "...");
        G('<', "..#", ".#.", "#..", ".#.", "..#", "...", "...");
        return g;
    }

    /// <summary>Lettre accentuée → (lettre de base, accent). 'a' aigu, 'g' grave, 'c' circonflexe, 'd' tréma, 'u' cédille.</summary>
    static (char b, char accent)? Accented(char ch) => char.ToLowerInvariant(ch) switch
    {
        'é' => ('E', 'a'), 'è' => ('E', 'g'), 'ê' => ('E', 'c'), 'ë' => ('E', 'd'),
        'à' => ('A', 'g'), 'â' => ('A', 'c'), 'ä' => ('A', 'd'),
        'î' => ('I', 'c'), 'ï' => ('I', 'd'),
        'ô' => ('O', 'c'), 'ö' => ('O', 'd'),
        'ù' => ('U', 'g'), 'û' => ('U', 'c'), 'ü' => ('U', 'd'),
        'ç' => ('C', 'u'),
        _ => null,
    };

    static (string[] rows, char accent) Lookup(char ch)
    {
        if (Accented(ch) is (char b, char a)) return (Glyph[b], a);
        char up = char.ToUpperInvariant(ch);
        return (Glyph.TryGetValue(up, out var rows) ? rows : Glyph['?'], ' ');
    }

    public static int Measure(string text)
    {
        int w = 0;
        foreach (char ch in text.Replace("œ", "oe").Replace("Œ", "OE")) w += Lookup(ch).rows[0].Length + 1;
        return Math.Max(0, w - 1);
    }

    /// <summary>Dessine `text` (coin haut-gauche des capitales en (x, y)). Renvoie la largeur utilisée.</summary>
    public static int Draw(PixelCanvas c, string text, int x, int y, uint color)
    {
        int x0 = x;
        foreach (char ch in text.Replace("œ", "oe").Replace("Œ", "OE"))
        {
            var (rows, accent) = Lookup(ch);
            int w = rows[0].Length;
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < w; k++)
                    if (rows[r][k] == '#') c.Set(x + k, y + r, color, Layer.Fx);
            int cx = x + w / 2;
            switch (accent)
            {
                case 'a': c.Set(cx + 1, y - 2, color, Layer.Fx); c.Set(cx, y - 1, color, Layer.Fx); break;
                case 'g': c.Set(cx - 1, y - 2, color, Layer.Fx); c.Set(cx, y - 1, color, Layer.Fx); break;
                case 'c': c.Set(cx, y - 2, color, Layer.Fx); c.Set(cx - 1, y - 1, color, Layer.Fx); c.Set(cx + 1, y - 1, color, Layer.Fx); break;
                case 'd': c.Set(cx - 1, y - 2, color, Layer.Fx); c.Set(cx + 1, y - 2, color, Layer.Fx); break;
                case 'u': c.Set(cx, y + 7, color, Layer.Fx); break;
            }
            x += w + 1;
        }
        return x - x0 - 1;
    }

    /// <summary>Coupe `text` en lignes d'au plus `maxWidth` pixels (aux espaces ; un mot trop long est tronqué).</summary>
    public static List<string> Wrap(string text, int maxWidth)
    {
        var lines = new List<string>();
        string cur = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string w = word;
            while (Measure(w) > maxWidth && w.Length > 1) w = w[..^1];
            string tryLine = cur.Length == 0 ? w : cur + " " + w;
            if (Measure(tryLine) <= maxWidth) cur = tryLine;
            else { lines.Add(cur); cur = w; }
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }

    /// <summary>Raccourcit `text` avec « … » pour tenir dans `maxWidth` pixels.</summary>
    public static string Fit(string text, int maxWidth)
    {
        if (Measure(text) <= maxWidth) return text;
        while (text.Length > 1 && Measure(text + "…") > maxWidth) text = text[..^1];
        return text.TrimEnd() + "…";
    }

    /// <summary>Texte centré sur `cx`.</summary>
    public static void DrawCentered(PixelCanvas c, string text, int cx, int y, uint color) =>
        Draw(c, text, cx - Measure(text) / 2, y, color);
}
