namespace Pepin;

/// <summary>Palette et petits motifs pixel art décrits en texte ('.' = transparent).</summary>
public static class Glyphs
{
    public static readonly uint K = PixelCanvas.Rgb(17, 17, 20);
    public static readonly uint White = PixelCanvas.Rgb(250, 250, 250);
    public static readonly uint Red = PixelCanvas.Rgb(200, 50, 70);
    public static readonly uint Pink = PixelCanvas.Rgb(242, 120, 150);

    static uint Color(char ch) => ch switch
    {
        'k' => K,
        'w' => White,
        'r' => Red,
        'p' => Pink,
        'y' => PixelCanvas.Rgb(245, 180, 40),
        'z' => PixelCanvas.Rgb(130, 120, 220),
        'b' => PixelCanvas.Rgb(200, 235, 252),
        'B' => PixelCanvas.Rgb(110, 180, 230),
        'g' => PixelCanvas.Rgb(95, 180, 60),
        'G' => PixelCanvas.Rgb(55, 125, 45),
        'L' => PixelCanvas.Rgb(175, 232, 115),
        'o' => PixelCanvas.Rgb(232, 62, 72),
        'O' => PixelCanvas.Rgb(165, 30, 50),
        's' => PixelCanvas.Rgb(236, 236, 240),
        'S' => PixelCanvas.Rgb(185, 185, 196),
        'd' => PixelCanvas.Rgb(214, 198, 170),
        'n' => PixelCanvas.Rgb(80, 78, 104),
        'c' => PixelCanvas.Rgb(205, 160, 90),
        '-' => PixelCanvas.Rgb(175, 175, 188),
        'Y' => PixelCanvas.Rgb(196, 138, 22),
        'P' => PixelCanvas.Rgb(214, 92, 124),
        'C' => PixelCanvas.Rgb(110, 72, 30),
        _ => 0,
    };

    public static readonly string[] Heart = [".p.p.", "ppppp", "ppppp", ".ppp.", "..p.."];
    public static readonly string[] SmallHeart = ["p.p", "ppp", ".p."];
    public static readonly string[] Anger = [".r.r.", "rr.rr", ".....", "rr.rr", ".r.r."];
    public static readonly string[] Exclaim = ["yy", "yy", "yy", "yy", "..", "yy"];
    public static readonly string[] Question = [".zz.", "z..z", "...z", "..z.", "....", "..z."];
    public static readonly string[] BigZ = ["zzzzz", "...z.", "..z..", ".z...", "zzzzz"];
    public static readonly string[] SmallZ = ["zzzz", "..z.", ".z..", "zzzz"];
    public static readonly string[] Note = ["..nn.", "..n.n", "..n..", "nnn..", "nnn.."];
    public static readonly string[] Star = ["..y..", ".yyy.", "yyyyy", ".yyy.", "y...y"];
    public static readonly string[] SmallStar = [".y.", "yyy", ".y."];
    public static readonly string[] Sweat = [".B.", "BbB", "BBB", ".B."];
    public static readonly string[] Lettuce = ["..gg..", ".gLLg.", "gLLgLg", "gLgLLg", ".gGGg."];
    public static readonly string[] Strawberry = [".GgG.", "ooyoo", "oyooo", "oooyo", ".ooo.", "..o.."];
    public static readonly string[] Cloud = ["..SS...", ".SssSS.", "SsssssS", ".SSSSS."];
    public static readonly string[] Puff = [".s.", "sss", ".s."];
    public static readonly string[] Dust = [".d.", "ddd"];
    public static readonly string[] ThoughtBubble =
    [
        "..kkkkkkk..",
        ".kwwwwwwwk.",
        "kwwwwwwwwwk",
        "kwwwwwwwwwk",
        "kwwwwwwwwwk",
        "kwwwwwwwwwk",
        "kwwwwwwwwwk",
        ".kwwwwwwwk.",
        "..kkkkkkk..",
    ];
    public static readonly string[] TinyBubble = [".k.", "kwk", ".k."];
    public static readonly string[] HeartEye = ["r.r", "rrr", ".r."];
    public static readonly string[] GoldBerry = [".GgG.", "yywyy", "ywyyy", "yyyYy", ".yyy.", "..y.."];
    public static readonly string[] Seashell = [".pppp.", "pPpPpP", "pPpPpP", ".pPpP.", "..pp.."];
    public static readonly string[] Pebble = ["..SSS.", ".SsssS", "SsssSS", ".SSSS."];
    public static readonly string[] Flower = [".p.p.", "ppypp", ".p.p.", "..g..", ".gg..", "..g.."];
    public static readonly string[] Feather = ["....B", "...Bb", "..Bb.", ".Bb..", "Bb...", "k...."];
    public static readonly string[] Clover = [".g.g.", "ggGgg", ".gGg.", "..G..", "..G.."];
    public static readonly string[] Acorn = [".CCC.", "CCCCC", ".ccc.", ".ccc.", "..c.."];
    public static readonly string[] Button = [".ooo.", "ooooo", "oOoOo", "ooooo", ".ooo."];
    public static readonly string[] Tear = [".b.", "bBb", ".B."];
    public static readonly string[] Mushroom = ["..oooo..", ".owooow.", "oooowooo", ".dddddd.", "...dd...", "...dd..."];
    public static readonly string[] Dandelion = [".yyy.", "yyYyy", ".yyy.", "..g..", ".gg..", "..g.."];
    public static readonly string[] Ladybug = [".kkk.", "rrkrr", "rkrkr", ".rrr."];
    public static readonly string[] Snail = ["..ccc..d.", ".cCCcc.d.", ".cCcCcdd.", "ddddddd.."];
    public static readonly string[] Frog = ["gkg.gkg", ".ggggg.", "gLLLLLg", "g.g.g.g"];
    public static readonly string[] Butterfly = ["zz...zz", "zpz.zpz", ".zzkzz.", "zpzkzpz", "zz...zz"];
    public static readonly string[] Tuft =
    [
        "...L....L...",
        ".L.gL..gL.L.",
        ".gLgg.LgggLg",
        "gLggGgggGgLg",
        "gggGGgGGGggg",
        ".GGGGGGGGGG.",
    ];

    public static string[] ForItem(Item i) => i switch
    {
        Item.Fraise => Strawberry,
        Item.FraiseDoree => GoldBerry,
        Item.Salade => Lettuce,
        Item.Coquillage => Seashell,
        Item.Caillou => Pebble,
        Item.Fleur => Flower,
        Item.Plume => Feather,
        Item.Trefle => Clover,
        Item.Gland => Acorn,
        Item.Champignon => Mushroom,
        Item.Pissenlit => Dandelion,
        Item.Coccinelle => Ladybug,
        Item.Escargot => Snail,
        Item.Grenouille => Frog,
        Item.Papillon => Butterfly,
        _ => Button,
    };
    public static readonly string[] SpiralA = ["kkk", "k.k", "k.."];
    public static readonly string[] SpiralB = ["kk.", "k.k", "kkk"];

    /// <summary>Dessine un motif centré sur (cx, cy). Jamais mis en miroir.</summary>
    public static void Draw(PixelCanvas c, string[] g, int cx, int cy, Layer layer = Layer.Fx)
    {
        int h = g.Length, w = g[0].Length;
        DrawAt(c, g, cx - w / 2, cy - h / 2, layer);
    }

    public static void DrawAt(PixelCanvas c, string[] g, int x0, int y0, Layer layer = Layer.Fx,
                              int colFrom = 0, int colTo = int.MaxValue)
    {
        for (int y = 0; y < g.Length; y++)
            for (int x = Math.Max(0, colFrom); x < Math.Min(g[y].Length, colTo); x++)
            {
                uint col = Color(g[y][x]);
                if (col != 0) c.Set(x0 + x, y0 + y, col, layer);
            }
    }
}
