using static Pepin.Native;

namespace Pepin;

public enum MenuPage : byte { Home, Band, Carnet, Settings }

public enum MenuAction : byte
{
    None, Go, Back,
    Bowling, Grass, Collection, SleepWake, CallHere, Pause, Quit,
    SendRandom, SendNote, SendTo, DeckOf, BlockGuest, BandPage, CarnetPage,
    ToggleAutostart, ToggleSpontaneous, ToggleMessages, ToggleMiniGames, Size, Rename, Discord,
}

/// <summary>Un membre de la bande tel que le menu l'affiche.</summary>
public sealed record BandEntry(string Id, string Name, bool Online, bool Available, Species Species);

/// <summary>Instantané de tout ce que le menu affiche (construit par App, ou à la main par --preview).</summary>
public sealed class MenuModel
{
    public string Name = "Pépin";
    public Species Species;
    public readonly Visual Portrait = new();
    public int Tier;
    public string Summary = "", Today = "";
    public double Affection, Energy, Belly;
    public bool Egg;                               // pas encore éclos : menu réduit
    public bool CanPlay, CanGrass, CanSend, Asleep, Away, Paused;
    public bool Autostart, Spontaneous, Messages, MiniGames, Registered;
    public int SizeLevel = 3;
    public string Version = "";
    public string BandStatus = "";
    public List<BandEntry> Band = [];
    public string? GuestName;
    public int Guests;                             // visiteurs chez nous en ce moment
    public List<(string stamp, string text)> Journal = [];
}

/// <summary>
/// Le menu de Pépin : un panneau pixel art façon écran d'accueil de jeu mobile (tuiles pour jouer, petites icônes
/// pour les réglages), au clic droit sur l'animal et sur l'icône de notification. Se ferme au clic dehors, sur
/// Échap ou après une action. Détruit à la fermeture.
/// </summary>
public sealed class MenuPanel
{
    public const int W = 140, H = 226;
    const double OpenAnim = 0.18;

    struct Btn
    {
        public int X, Y, Wd, Ht;
        public MenuAction A;
        public string? Arg;
        public bool On;
        public bool Contains(int x, int y) => x >= X && x < X + Wd && y >= Y && y < Y + Ht;
    }

    Overlay? ov;
    nint inst;
    readonly List<Btn> btns = [];
    MenuPage page;
    int hover = -1, pressed = -1;
    double openedAt;
    int baseX, baseY, scale;
    POINT lastAnchor;
    RECT lastWork;
    bool lastAbove;
    public double ClosedAt = -10;

    /// <summary>Fournit l'état à afficher (appelé à chaque rafraîchissement).</summary>
    public Func<MenuModel>? Model;
    /// <summary>Action choisie ; le menu se ferme avant l'appel sauf pour la navigation et les cases à cocher.</summary>
    public Action<MenuAction, string?>? Act;

    /// <summary>Où le panneau s'est ouvert en dernier (pixels écran) : la collection s'ouvre au même endroit.</summary>
    public (int X, int Y, int W, int H, int Scale) Bounds => (baseX, baseY, W * scale, H * scale, scale);

    public bool Visible => ov?.Visible == true;
    public nint Hwnd => ov?.Hwnd ?? 0;
    public bool Animating => Visible && App.Now - openedAt < OpenAnim;

    public void Init(nint instance) => inst = instance;

    /// <summary>Ouvre le menu près de `anchor` (pixels écran), dans la zone de travail `work`.</summary>
    public void Open(POINT anchor, RECT work, int pixelScale, bool aboveAnchor)
    {
        Close();
        lastAnchor = anchor; lastWork = work; lastAbove = aboveAnchor;
        ov = new Overlay(clickable: true, activatable: true);
        ov.Create(inst);
        ov.Mouse = OnMouse;
        page = MenuPage.Home;
        hover = pressed = -1;
        scale = pixelScale;
        while (scale > 1 && H * scale > work.Bottom - work.Top - 8) scale--;      // petit écran : on rétrécit plutôt que de couper
        int w = W * scale, h = H * scale;
        int x = anchor.X - w / 2;
        int y = aboveAnchor ? anchor.Y - h - 4 * scale : anchor.Y - h;
        baseX = Math.Clamp(x, work.Left + 4, Math.Max(work.Left + 4, work.Right - w - 4));
        baseY = Math.Clamp(y, work.Top + 4, Math.Max(work.Top + 4, work.Bottom - h - 4));
        openedAt = App.Now;
        Refresh();
        SetForegroundWindow(ov.Hwnd);
        SetTimer(ov.Hwnd, 2, 15, 0);
    }

    /// <summary>Rouvre le panneau là où il était (retour de la collection), sur la page demandée.</summary>
    public void Reopen(MenuPage p)
    {
        Open(lastAnchor, lastWork, lastScale(), lastAbove);
        if (p != MenuPage.Home) Go(p);
    }

    int lastScale() => scale;

    public void Close()
    {
        if (ov is null) return;
        KillTimer(ov.Hwnd, 2);
        var o = ov;
        ov = null;
        o.Destroy();
        ClosedAt = App.Now;
    }

    /// <summary>Messages propres au panneau (routés par App). Vrai si traité.</summary>
    public bool Handle(nint h, uint m, nint w, nint l)
    {
        if (ov is null || h != ov.Hwnd) return false;
        switch (m)
        {
            case WM_TIMER:
                Refresh();
                if (!Animating && ov is not null) KillTimer(ov.Hwnd, 2);
                return true;
            case WM_ACTIVATE:
                if (LoWord(w) == 0) Close();         // WA_INACTIVE : clic ailleurs
                return true;
            case WM_KEYDOWN:
                if ((int)w == VK_ESCAPE) { if (page != MenuPage.Home) Go(MenuPage.Home); else Close(); }
                return true;
        }
        return false;
    }

    /// <summary>Rafraîchit l'image (portrait, jauges) : appelé par la boucle de l'appli.</summary>
    public void Refresh()
    {
        if (ov is null || Model is null) return;
        var c = Render(Model(), page, hover, pressed, btns);
        double t = Math.Clamp((App.Now - openedAt) / OpenAnim, 0, 1);
        // petit « pop » d'ouverture : monte de 8 pixels en dépassant un peu (ease-out-back)
        double e = 1 + 2.7 * Math.Pow(t - 1, 3) + 1.7 * Math.Pow(t - 1, 2);
        int dy = (int)Math.Round((1 - e) * 8) * scale;
        ov.Present(c, scale, baseX, baseY + dy);
    }

    void Go(MenuPage p)
    {
        page = p;
        hover = pressed = -1;
        Refresh();
    }

    void OnMouse(uint m, int x, int y)
    {
        int idx = -1;
        for (int i = 0; i < btns.Count; i++)
            if (btns[i].On && btns[i].Contains(x, y)) { idx = i; break; }
        switch (m)
        {
            case WM_MOUSEMOVE:
                if (idx != hover) { hover = idx; Refresh(); }
                break;
            case WM_LBUTTONDOWN:
                pressed = idx;
                Refresh();
                break;
            case WM_LBUTTONUP:
                int was = pressed;
                pressed = -1;
                if (was >= 0 && was == idx) Fire(btns[idx]);
                else Refresh();
                break;
        }
    }

    void Fire(Btn b)
    {
        switch (b.A)
        {
            case MenuAction.Go:
                Go(b.Arg switch { "Band" => MenuPage.Band, "Carnet" => MenuPage.Carnet, "Settings" => MenuPage.Settings, _ => MenuPage.Home });
                return;
            case MenuAction.Back:
                Go(MenuPage.Home);
                return;
            case MenuAction.ToggleAutostart or MenuAction.ToggleSpontaneous or MenuAction.ToggleMessages
                 or MenuAction.ToggleMiniGames or MenuAction.Size:
                Act?.Invoke(b.A, b.Arg);
                Refresh();
                return;
        }
        Close();
        Act?.Invoke(b.A, b.Arg);
    }

    /// <summary>Le curseur a quitté le panneau : on éteint le survol.</summary>
    public void TrackHover(int sx, int sy)
    {
        if (ov is null || hover < 0) return;
        int x = (sx - ov.X) / Math.Max(1, scale), y = (sy - ov.Y) / Math.Max(1, scale);
        if (x < 0 || y < 0 || x >= W || y >= H) { hover = -1; Refresh(); }
    }

    // ================================================================== dessin

    static readonly uint Ink = Glyphs.K, Paper = PixelCanvas.Rgb(255, 248, 231), Sky = PixelCanvas.Rgb(138, 208, 236),
        SkyDark = PixelCanvas.Rgb(98, 168, 210), Dim = PixelCanvas.Rgb(128, 116, 100), Bar = PixelCanvas.Rgb(232, 221, 196),
        Track = PixelCanvas.Rgb(214, 202, 178), Grey = PixelCanvas.Rgb(186, 182, 176), White = Glyphs.White,
        Coral = PixelCanvas.Rgb(240, 112, 92), Leaf = PixelCanvas.Rgb(104, 186, 78), Blue = PixelCanvas.Rgb(86, 148, 230),
        Grape = PixelCanvas.Rgb(166, 108, 210), Amber = PixelCanvas.Rgb(250, 196, 72), Sand = PixelCanvas.Rgb(214, 198, 166),
        Cherry = PixelCanvas.Rgb(222, 74, 84), HeartCol = PixelCanvas.Rgb(242, 104, 140), BoltCol = PixelCanvas.Rgb(246, 186, 40),
        Berry = PixelCanvas.Rgb(232, 66, 72), Mint = PixelCanvas.Rgb(92, 196, 120), Blurple = PixelCanvas.Rgb(88, 101, 242);

    const int Footer = H - 19;               // bouton du bas des sous-pages

    static PixelCanvas? cv;
    static List<Btn> bl = null!;
    static int hov, prs;
    static readonly PixelCanvas portrait = new(ArtKit.CW, ArtKit.CH);

    /// <summary>Dessine une page du menu (aussi utilisé par --preview). `buttons` reçoit les zones cliquables.</summary>
    static PixelCanvas Render(MenuModel m, MenuPage page, int hover, int pressed, List<Btn> buttons)
    {
        cv ??= new PixelCanvas(W, H);
        var c = cv;
        c.Clear();
        bl = buttons; bl.Clear();
        hov = hover; prs = pressed;

        // carte : fond crème, bord sombre, coins arrondis
        RoundRect(0, 0, W, H, Paper, Ink, 3);
        switch (page)
        {
            case MenuPage.Home: Home(m); break;
            case MenuPage.Band: BandPage(m); break;
            case MenuPage.Carnet: CarnetPage(m); break;
            case MenuPage.Settings: SettingsPage(m); break;
        }
        return c;
    }

    public static PixelCanvas RenderForPreview(MenuModel m, MenuPage page, int hover = -1)
    {
        var c = Render(m, page, hover, -1, []);
        var copy = new PixelCanvas(W, H);
        Array.Copy(c.Px, copy.Px, c.Px.Length);
        return copy;
    }

    // ------------------------------------------------------------------ pages

    static void Home(MenuModel m)
    {
        Banner(m);

        // bandeau du haut : ciel + portrait
        const int o = 28;
        HLine(0, o, W, Ink);
        Fill(1, o + 1, W - 2, 52, Sky);
        Fill(1, o + 50, W - 2, 3, SkyDark);
        HLine(0, o + 53, W, Ink);
        RoundRect(5, o + 5, 44, 44, Paper, Ink, 2);
        Fill(6, o + 38, 42, 10, PixelCanvas.Rgb(178, 226, 132));   // un bout d'herbe sous ses pieds
        DrawPortrait(m, 6, o + 6, 42, 42);

        Text(PixelFont.Fit(m.Name.ToUpperInvariant(), W - 60), 54, o + 7, Ink, White);
        for (int i = 0; i < 4; i++)
            Glyphs.DrawTinted(cv!, Glyphs.StarShape, 54 + i * 7, o + 17, i < m.Tier ? BoltCol : PixelCanvas.Rgb(110, 150, 175));
        Gauge(54, o + 27, Glyphs.Heart, null, m.Affection, HeartCol);
        Gauge(54, o + 35, Glyphs.IcoBolt, BoltCol, m.Energy, BoltCol);
        Gauge(54, o + 43, Glyphs.Strawberry, null, m.Belly, Berry, iconDy: -1);

        if (m.Egg)
        {
            TextC("UN ŒUF MYSTÈRE...", W / 2, 110, Ink);
            TextC("TAPOTE-LE POUR", W / 2, 126, Dim);
            TextC("QU'IL ÉCLOSE !", W / 2, 136, Dim);
        }
        else
        {
            // de haut en bas : jouer, rendre visite, retrouver ses affaires, prendre soin
            Tile(4, 85, 64, 38, Coral, Glyphs.IcoPin, "BOWLING", MenuAction.Bowling, null, m.CanPlay);
            Tile(72, 85, 64, 38, Leaf, Glyphs.IcoSprout, "HERBE", MenuAction.Grass, null, m.CanGrass);
            Tile(4, 126, 64, 38, Blue, Glyphs.IcoDice, "AU HASARD", MenuAction.SendRandom, null, m.CanSend);
            Tile(72, 126, 64, 38, Blue, Glyphs.IcoFriends, "LES AMIS", MenuAction.Go, nameof(MenuPage.Band), !m.Away);

            Pill(4, 168, 76, 15, Glyphs.IcoSmallBook, "COLLECTION", MenuAction.Collection, true, Grape);
            Pill(84, 168, 52, 15, Glyphs.IcoNotebook, "CARNET", MenuAction.Go, true, Sand, nameof(MenuPage.Carnet));

            Pill(4, 186, 64, 15, m.Asleep ? Glyphs.IcoSun : Glyphs.IcoMoon, m.Asleep ? "RÉVEIL" : "DODO", MenuAction.SleepWake, !m.Away);
            Pill(72, 186, 64, 15, Glyphs.IcoHere, "VIENS ICI", MenuAction.CallHere, !m.Away);
        }

        BottomBar(m);
    }

    /// <summary>« Post » bleu en haut du menu : invite à rejoindre la bande sur Discord (ouvre l'invitation).</summary>
    static void Banner(MenuModel m)
    {
        ButtonBox(4, 3, W - 8, 24, Blurple, MenuAction.Discord, null, true, out int ox, out int oy);
        // reflet du haut, plus clair, pour que le bandeau « sorte » du fond
        Glyphs.DrawTinted(cv!, Glyphs.IcoDiscord, 9 + ox, 9 + oy, White);
        Text("REJOINS LA BANDE", 24 + ox, 5 + oy, White, Darken(Blurple));
        Text("SUR DISCORD", 24 + ox, 14 + oy, PixelCanvas.Rgb(214, 220, 255), null);
        Glyphs.DrawTinted(cv!, Glyphs.IcoArrow, W - 17 + ox, 12 + oy, White);
        // pastille de notification, comme un message non lu
        Fill(W - 12, 1, 9, 9, Ink);
        Fill(W - 11, 2, 7, 7, Cherry);
        Text("!", W - 9, 3, White, null);
    }

    static void BandPage(MenuModel m)
    {
        TopBar("LA BANDE");
        TextC(m.BandStatus, W / 2, 24, Dim);

        SmallTile(4, 35, 64, 20, Blue, Glyphs.IcoDice, "HASARD", MenuAction.SendRandom, m.CanSend);
        SmallTile(72, 35, 64, 20, Amber, Glyphs.IcoSmallLetter, "UN MOT", MenuAction.SendNote, m.CanSend);

        int y = 60;
        int rows = (Footer - (m.GuestName is null ? 2 : 20) - y) / 17;
        if (m.Band.Count == 0) TextC("PERSONNE POUR L'INSTANT", W / 2, y + 30, Dim);
        foreach (var b in m.Band.Take(rows))
        {
            Fill(4, y, W - 8, 16, PixelCanvas.Rgb(246, 236, 212));
            var ic = new PixelCanvas(16, 16);
            SpeciesArt.DrawIcon(ic, b.Species);
            Blit(ic, 5, y, b.Online ? 1f : 0.45f);
            Text(PixelFont.Fit(b.Name.ToUpperInvariant(), 72), 24, y + 5, b.Online ? Ink : Dim, null);
            if (b.Online) Dot(99, y + 7, Mint);
            IconButton(104, y + 1, 14, 14, Glyphs.IcoSmallLetter, Blue, MenuAction.SendTo, b.Id, m.CanSend && b.Available);
            IconButton(120, y + 1, 14, 14, Glyphs.IcoSmallBook, Grape, MenuAction.DeckOf, b.Id, true);
            y += 17;
        }
        if (m.GuestName is string g)
            Pill(4, Footer - 18, W - 8, 15, Glyphs.IcoNo, PixelFont.Fit("BLOQUER " + g.ToUpperInvariant(), W - 30), MenuAction.BlockGuest, true, Cherry);
        Pill(4, Footer, W - 8, 15, Glyphs.IcoGlobe, "LA BANDE EN LIGNE", MenuAction.BandPage, true, Sand);
    }

    static void CarnetPage(MenuModel m)
    {
        TopBar("CARNET");
        int y = 24;
        foreach (var line in PixelFont.Wrap(m.Summary, W - 12)) { Text(line, 6, y, Ink, null); y += 9; }
        foreach (var line in PixelFont.Wrap(m.Today, W - 12)) { Text(line, 6, y, Dim, null); y += 9; }
        HLine(6, y + 1, W - 12, Track);
        y += 6;
        foreach (var (stamp, text) in m.Journal)
        {
            var lines = PixelFont.Wrap(text, W - 44);
            if (y + Math.Min(2, lines.Count) * 9 > Footer - 4) break;
            Text(stamp, 6, y, Blue, null);
            for (int i = 0; i < Math.Min(2, lines.Count); i++)
            {
                string l = i == 1 && lines.Count > 2 ? PixelFont.Fit(lines[1] + "…", W - 44) : lines[i];
                Text(l, 38, y, Ink, null);
                y += 9;
            }
            y += 2;
        }
        Pill(4, Footer, W - 8, 15, Glyphs.IcoGlobe, "CARNET EN LIGNE", MenuAction.CarnetPage, m.Registered, Sand);
    }

    static void SettingsPage(MenuModel m)
    {
        TopBar("RÉGLAGES");
        int y = 24;
        Check(y, "DÉMARRAGE AUTO", m.Autostart, MenuAction.ToggleAutostart); y += 15;
        Check(y, "VISITES SPONTANÉES", m.Spontaneous, MenuAction.ToggleSpontaneous); y += 15;
        Check(y, "PETITS MOTS", m.Messages, MenuAction.ToggleMessages); y += 15;
        Check(y, "JEUX SPONTANÉS", m.MiniGames, MenuAction.ToggleMiniGames); y += 17;

        Text("TAILLE", 8, y + 4, Ink, null);
        string[] sizes = ["P", "M", "G"];
        for (int i = 0; i < 3; i++)
        {
            bool on = m.SizeLevel == i + 2;
            ButtonBox(70 + i * 22, y, 20, 15, on ? Blue : Sand, MenuAction.Size, (i + 2).ToString(), true, out int ox, out int oy);
            TextC(sizes[i], 70 + i * 22 + 10 + ox, y + 4 + oy, on ? White : Ink);
        }
        y += 22;
        Pill(4, y, W - 8, 15, Glyphs.IcoPencil, "RENOMMER", MenuAction.Rename, m.Registered, Amber);
        y += 19;
        Pill(4, y, W - 8, 15, Glyphs.IcoCross, "QUITTER PÉPIN", MenuAction.Quit, true, Cherry);
        TextC("VERSION " + m.Version, W / 2, Footer + 4, Dim);
    }

    // ------------------------------------------------------------------ éléments

    static void TopBar(string title)
    {
        Fill(1, 1, W - 2, 18, Sky);
        HLine(0, 19, W, Ink);
        IconButton(4, 3, 16, 14, Glyphs.IcoBack, Sand, MenuAction.Back, null, true);
        TextC(title, W / 2 + 6, 7, Ink);
    }

    static void BottomBar(MenuModel m)
    {
        HLine(0, H - 22, W, Ink);
        Fill(1, H - 21, W - 2, 20, Bar);
        IconButton(5, H - 18, 16, 15, Glyphs.IcoGear, Sand, MenuAction.Go, nameof(MenuPage.Settings), true);
        IconButton(24, H - 18, 16, 15, m.Paused ? Glyphs.IcoPlay : Glyphs.IcoPause, Sand, MenuAction.Pause, null, !m.Egg);
        IconButton(43, H - 18, 16, 15, Glyphs.IcoCross, Cherry, MenuAction.Quit, null, true);
        Text("V" + m.Version, W - 6 - PixelFont.Measure("V" + m.Version), H - 14, Dim, null);
    }

    static void DrawPortrait(MenuModel m, int x, int y, int w, int h)
    {
        if (m.Egg)
        {
            var e = new PixelCanvas(Egg.EW, Egg.EH);
            Egg.DrawShell(e, 0);
            for (int yy = 0; yy < Egg.EH; yy++)
                for (int xx = 0; xx < Egg.EW; xx++)
                {
                    uint p = e.Px[yy * Egg.EW + xx];
                    if (p >> 24 != 0) cv!.Set(x + (w - Egg.EW) / 2 + xx, y + h - Egg.EH - 2 + yy, p, Layer.Fx);
                }
            return;
        }
        var v = new Visual();
        m.Portrait.CopyTo(v);
        v.FacingRight = true;
        v.BodyDx = 0;
        v.NoShadow = false;
        v.ShadowZ = 0;
        SpeciesArt.Draw(portrait, v, m.Species);
        // le corps occupe à peu près x 6..50, y 6..46 de sa toile : on le centre dans la case
        int sx0 = 6, sy0 = 4;
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++)
            {
                uint p = portrait.Px[(sy0 + yy) * ArtKit.CW + sx0 + xx];
                if (p >> 24 != 0) cv!.Set(x + xx, y + yy, Over(cv.Px[(y + yy) * W + x + xx], p), Layer.Fx);
            }
    }

    static void Gauge(int x, int y, string[] icon, uint? ink, double value, uint fill, int iconDy = 0)
    {
        if (ink is uint k) Glyphs.DrawTinted(cv!, icon, x, y + iconDy, k);
        else Glyphs.DrawAt(cv!, icon, x, y + iconDy);
        int bx = x + 9, bw = W - 6 - bx;
        Fill(bx, y, bw, 6, Ink);
        Fill(bx + 1, y + 1, bw - 2, 4, Track);
        int fw = (int)Math.Round((bw - 2) * Math.Clamp(value, 0, 1));
        if (fw > 0)
        {
            Fill(bx + 1, y + 1, fw, 4, fill);
            Fill(bx + 1, y + 1, fw, 1, PixelCanvas.Lerp(fill, White, 0.45f));
        }
    }

    /// <summary>Grosse tuile de jeu : icône en haut, nom en dessous, relief en bas.</summary>
    static void Tile(int x, int y, int w, int h, uint col, string[] icon, string label, MenuAction a, string? arg, bool on)
    {
        ButtonBox(x, y, w, h, on ? col : Grey, a, arg, on, out int ox, out int oy);
        int iw = icon[0].Length * 2, ih = icon.Length * 2;              // icônes doublées : gros pixels « jeu mobile »
        int ix = x + (w - iw) / 2 + ox, iy = y + 3 + (20 - ih) / 2 + oy;
        Big(icon, ix + 1, iy + 1, Darken(on ? col : Grey), solid: true);
        Big(icon, ix, iy, on ? White : PixelCanvas.Rgb(225, 222, 218));
        TextC(label, x + w / 2 + ox, y + h - 13 + oy, on ? White : PixelCanvas.Rgb(232, 230, 226), Darken(on ? col : Grey));
        if (!on) Glyphs.DrawTinted(cv!, Glyphs.IcoLock, x + w - 9, y + 4, Dim);
    }

    static void SmallTile(int x, int y, int w, int h, uint col, string[] icon, string label, MenuAction a, bool on)
    {
        ButtonBox(x, y, w, h, on ? col : Grey, a, null, on, out int ox, out int oy);
        int lw = PixelFont.Measure(label), iw = icon[0].Length, total = iw + 4 + lw;
        int x0 = x + (w - total) / 2 + ox;
        Shadowed(icon, x0, y + (h - 2 - icon.Length) / 2 + oy, White, Darken(on ? col : Grey));
        Text(label, x0 + iw + 4, y + (h - 2 - 7) / 2 + oy, White, Darken(on ? col : Grey));
    }

    static void Pill(int x, int y, int w, int h, string[] icon, string label, MenuAction a, bool on, uint? col = null, string? arg = null)
    {
        uint body = on ? col ?? Amber : Grey;
        ButtonBox(x, y, w, h, body, a, arg, on, out int ox, out int oy);
        bool light = col is uint cc && cc != Amber && cc != Sand;
        uint ink = light ? White : Ink;
        int lw = PixelFont.Measure(label), iw = icon[0].Length, total = iw + 4 + lw;
        int x0 = x + (w - total) / 2 + ox, ty = y + (h - 2 - 7) / 2 + oy;
        Glyphs.DrawTinted(cv!, icon, x0, y + (h - 2 - icon.Length) / 2 + oy, ink);
        Text(label, x0 + iw + 4, ty, ink, null);
    }

    static void IconButton(int x, int y, int w, int h, string[] icon, uint col, MenuAction a, string? arg, bool on)
    {
        ButtonBox(x, y, w, h, on ? col : Grey, a, arg, on, out int ox, out int oy);
        bool light = col == Cherry || col == Blue || col == Grape;
        Glyphs.DrawTinted(cv!, icon, x + (w - icon[0].Length) / 2 + ox, y + (h - 2 - icon.Length) / 2 + oy, light && on ? White : on ? Ink : Dim);
    }

    static void Check(int y, string label, bool on, MenuAction a)
    {
        ButtonBox(6, y, 12, 12, on ? Mint : Paper, a, null, true, out int ox, out int oy);
        if (on)
            foreach (var (dx, dy) in new[] { (3, 5), (4, 6), (5, 7), (6, 6), (7, 5), (8, 4) })
                cv!.Set(6 + dx + ox, y + dy - 1 + oy, White, Layer.Fx);
        Text(label, 22, y + 2, Ink, null);
        // toute la ligne est cliquable
        var b = bl[^1];
        b.Wd = W - 12;
        bl[^1] = b;
    }

    /// <summary>Bouton en relief : survol = plus clair et monte d'un pixel, appui = descend.</summary>
    static void ButtonBox(int x, int y, int w, int h, uint col, MenuAction a, string? arg, bool on, out int ox, out int oy)
    {
        int idx = bl.Count;
        bl.Add(new Btn { X = x, Y = y, Wd = w, Ht = h, A = a, Arg = arg, On = on });
        bool isHover = on && idx == hov, isPressed = on && idx == prs;
        ox = 0;
        oy = isPressed ? 1 : isHover ? -1 : 0;
        uint body = isHover ? PixelCanvas.Lerp(col, White, 0.18f) : col;
        int depth = isPressed ? 1 : 2;
        // ombre portée (relief) puis le dessus
        RoundRect(x, y + oy + depth, w, h - depth, Darken(col), Ink, 2);
        RoundRect(x, y + oy, w, h - depth, body, Ink, 2);
        HLine(x + 2, y + oy + 1, w - 4, PixelCanvas.Lerp(body, White, 0.35f));
    }

    // ------------------------------------------------------------------ primitives

    static uint Darken(uint c) => PixelCanvas.Lerp(c, Ink, 0.3f);

    static void Fill(int x, int y, int w, int h, uint col)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++) cv!.Set(xx, yy, col, Layer.Fx);
    }

    static void HLine(int x, int y, int w, uint col) => Fill(x, y, w, 1, col);

    /// <summary>Rectangle plein à bord `edge`, coins arrondis de rayon `r` (1 à 3).</summary>
    static void RoundRect(int x, int y, int w, int h, uint fill, uint edge, int r)
    {
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++)
            {
                int dx = Math.Min(xx, w - 1 - xx), dy = Math.Min(yy, h - 1 - yy);
                if (dx + dy < r - 1) continue;                    // coin coupé
                bool border = dx == 0 || dy == 0 || dx + dy == r - 1;
                cv!.Set(x + xx, y + yy, border ? edge : fill, Layer.Fx);
            }
    }

    static void Text(string s, int x, int y, uint col, uint? shadow)
    {
        if (shadow is uint sh) PixelFont.Draw(cv!, s, x + 1, y + 1, sh);
        PixelFont.Draw(cv!, s, x, y, col);
    }

    static void TextC(string s, int cx, int y, uint col, uint? shadow = null) =>
        Text(s, cx - PixelFont.Measure(s) / 2, y, col, shadow);

    /// <summary>Icône agrandie ×2 ; `solid` : tous ses pixels de la couleur `ink` (pour l'ombre).</summary>
    static void Big(string[] icon, int x, int y, uint ink, bool solid = false)
    {
        var tmp = new PixelCanvas(icon[0].Length, icon.Length);
        Glyphs.DrawTinted(tmp, icon, 0, 0, ink);
        for (int yy = 0; yy < tmp.H; yy++)
            for (int xx = 0; xx < tmp.W; xx++)
            {
                uint p = tmp.Px[yy * tmp.W + xx];
                if (p >> 24 == 0) continue;
                if (solid) p = ink;
                for (int k = 0; k < 4; k++) cv!.Set(x + xx * 2 + k % 2, y + yy * 2 + k / 2, p, Layer.Fx);
            }
    }

    static void Shadowed(string[] icon, int x, int y, uint ink, uint shadow)
    {
        Glyphs.DrawTinted(cv!, icon, x + 1, y + 1, shadow);
        Glyphs.DrawTinted(cv!, icon, x, y, ink);
    }

    static void Dot(int x, int y, uint col)
    {
        Fill(x, y - 1, 3, 3, Ink);
        cv!.Set(x + 1, y, col, Layer.Fx);
    }

    static void Blit(PixelCanvas src, int x, int y, float alpha)
    {
        for (int yy = 0; yy < src.H; yy++)
            for (int xx = 0; xx < src.W; xx++)
            {
                uint p = src.Px[yy * src.W + xx];
                if (p >> 24 == 0) continue;
                if (alpha < 1) p = PixelCanvas.Lerp(p, PixelCanvas.Rgb(246, 236, 212), 1 - alpha);
                cv!.Set(x + xx, y + yy, p, Layer.Fx);
            }
    }

    /// <summary>Pose `src` (prémultiplié) par-dessus `dst` (opaque).</summary>
    static uint Over(uint dst, uint src)
    {
        uint a = src >> 24;
        if (a == 255) return src;
        uint inv = 255 - a;
        uint r = ((src >> 16) & 255) + ((dst >> 16) & 255) * inv / 255;
        uint g = ((src >> 8) & 255) + ((dst >> 8) & 255) * inv / 255;
        uint b = (src & 255) + (dst & 255) * inv / 255;
        return 0xFF000000u | (r << 16) | (g << 8) | b;
    }
}
