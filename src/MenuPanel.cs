using static Pepin.Native;

namespace Pepin;

public enum MenuPage : byte { Home, Band, Carnet, Settings }

public enum MenuAction : byte
{
    None, Go, Back,
    Bowling, Grass, Collection, SleepWake, CallHere, Pause, Quit,
    SendRandom, SendNote, SendTo, DeckOf, BlockGuest, BandPage, CarnetPage,
    ToggleAutostart, ToggleSpontaneous, ToggleMessages, ToggleMiniGames, Size, Rename,
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
    public List<(string stamp, string text)> Journal = [];
}

/// <summary>
/// Le menu de Pépin : un panneau pixel art façon écran d'accueil de jeu mobile (tuiles pour jouer, petites icônes
/// pour les réglages), au clic droit sur l'animal et sur l'icône de notification. Se ferme au clic dehors, sur
/// Échap ou après une action. Détruit à la fermeture.
/// </summary>
public sealed class MenuPanel
{
    public const int W = 116, H = 160;
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
    public double ClosedAt = -10;

    /// <summary>Fournit l'état à afficher (appelé à chaque rafraîchissement).</summary>
    public Func<MenuModel>? Model;
    /// <summary>Action choisie ; le menu se ferme avant l'appel sauf pour la navigation et les cases à cocher.</summary>
    public Action<MenuAction, string?>? Act;

    public bool Visible => ov?.Visible == true;
    public nint Hwnd => ov?.Hwnd ?? 0;
    public bool Animating => Visible && App.Now - openedAt < OpenAnim;

    public void Init(nint instance) => inst = instance;

    /// <summary>Ouvre le menu près de `anchor` (pixels écran), dans la zone de travail `work`.</summary>
    public void Open(POINT anchor, RECT work, int pixelScale, bool aboveAnchor)
    {
        Close();
        ov = new Overlay(clickable: true, activatable: true);
        ov.Create(inst);
        ov.Mouse = OnMouse;
        page = MenuPage.Home;
        hover = pressed = -1;
        scale = pixelScale;
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
    // Palette volontairement sobre : encre + crème, un seul accent (le ciel) qui s'allume au survol, et un rouge
    // réservé aux actions destructrices (quitter, bloquer).

    static readonly uint Ink = Glyphs.K, Paper = PixelCanvas.Rgb(255, 248, 231), Sky = PixelCanvas.Rgb(138, 208, 236),
        SkyDark = PixelCanvas.Rgb(98, 168, 210), Deep = PixelCanvas.Rgb(52, 100, 150), Dim = PixelCanvas.Rgb(128, 116, 100),
        Bar = PixelCanvas.Rgb(232, 221, 196), Track = PixelCanvas.Rgb(214, 202, 178), White = Glyphs.White,
        Stripe = PixelCanvas.Rgb(244, 235, 214), Cherry = PixelCanvas.Rgb(204, 62, 74);

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
        // bandeau du haut : ciel, portrait, nom, jauges
        Fill(1, 1, W - 2, 46, Sky);
        HLine(0, 47, W, Ink);
        Fill(3, 35, 42, 10, SkyDark);                               // un bout de sol sous ses pieds
        DrawPortrait(m, 3, 3, 42, 42);

        Text(PixelFont.Fit(m.Name.ToUpperInvariant(), W - 54), 50, 5, Ink, null);
        for (int i = 0; i < 4; i++)
            Glyphs.DrawTinted(cv!, Glyphs.StarShape, 50 + i * 7, 15, i < m.Tier ? Ink : SkyDark);
        Gauge(50, 24, Glyphs.IcoHeart, m.Affection);
        Gauge(50, 31, Glyphs.IcoBolt, m.Energy);
        Gauge(50, 38, Glyphs.IcoBerry, m.Belly);

        if (m.Egg)
        {
            TextC("UN ŒUF MYSTÈRE...", W / 2, 70, Ink);
            TextC("TAPOTE-LE POUR", W / 2, 88, Dim);
            TextC("QU'IL ÉCLOSE !", W / 2, 98, Dim);
        }
        else
        {
            const int x = 4, w = W - 8;
            Row(x, 52, w, 15, Glyphs.IcoBowl, "BOWLING", MenuAction.Bowling, null, m.CanPlay);
            Row(x, 69, w, 15, Glyphs.IcoLeaf, "HERBE", MenuAction.Grass, null, m.CanGrass);
            Row(x, 86, w, 15, Glyphs.IcoSmallLetter, "VISITE", MenuAction.Go, nameof(MenuPage.Band), !m.Away);
            Row(x, 103, w, 15, Glyphs.IcoSmallBook, "COLLECTION", MenuAction.Collection, null, true);

            Pill(4, 123, 46, 14, null, m.Asleep ? "RÉVEIL" : "DODO", MenuAction.SleepWake, !m.Away);
            Pill(54, 123, 58, 14, null, "VIENS ICI", MenuAction.CallHere, !m.Away);
        }

        BottomBar(m);
    }

    static void BandPage(MenuModel m)
    {
        TopBar("LA BANDE");
        TextC(m.BandStatus, W / 2, 22, Dim);

        Pill(4, 32, 52, 13, Glyphs.IcoDice, "HASARD", MenuAction.SendRandom, m.CanSend);
        Pill(60, 32, 52, 13, Glyphs.IcoSmallLetter, "UN MOT", MenuAction.SendNote, m.CanSend);

        int y = 49;
        int rows = m.GuestName is null ? 6 : 4;
        if (m.Band.Count == 0)
        {
            TextC("PERSONNE POUR", W / 2, y + 30, Dim);
            TextC("L'INSTANT", W / 2, y + 40, Dim);
        }
        foreach (var b in m.Band.Take(rows))
        {
            Fill(4, y, W - 8, 14, Stripe);
            var ic = new PixelCanvas(16, 16);
            SpeciesArt.DrawIcon(ic, b.Species);
            Blit(ic, 5, y - 1, b.Online ? 1f : 0.45f);
            Text(PixelFont.Fit(b.Name.ToUpperInvariant(), 57), 24, y + 4, b.Online ? Ink : Dim, null);
            IconBtn(85, y + 1, 12, 12, Glyphs.IcoSmallLetter, MenuAction.SendTo, b.Id, m.CanSend && b.Available);
            IconBtn(98, y + 1, 12, 12, Glyphs.IcoSmallBook, MenuAction.DeckOf, b.Id, true);
            y += 15;
        }
        if (m.GuestName is string g)
            Pill(4, Footer - 17, W - 8, 15, Glyphs.IcoNo, PixelFont.Fit("BLOQUER " + g.ToUpperInvariant(), W - 30), MenuAction.BlockGuest, true, danger: true);
        Pill(4, Footer, W - 8, 15, Glyphs.IcoGlobe, "BANDE EN LIGNE", MenuAction.BandPage, true);
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
            Text(stamp, 6, y, Deep, null);
            for (int i = 0; i < Math.Min(2, lines.Count); i++)
            {
                string l = i == 1 && lines.Count > 2 ? PixelFont.Fit(lines[1] + "…", W - 44) : lines[i];
                Text(l, 38, y, Ink, null);
                y += 9;
            }
            y += 2;
        }
        Pill(4, Footer, W - 8, 15, Glyphs.IcoGlobe, "CARNET EN LIGNE", MenuAction.CarnetPage, m.Registered);
    }

    static void SettingsPage(MenuModel m)
    {
        TopBar("RÉGLAGES");
        int y = 23;
        Check(y, "DÉMARRAGE AUTO", m.Autostart, MenuAction.ToggleAutostart); y += 14;
        Check(y, "VISITES LIBRES", m.Spontaneous, MenuAction.ToggleSpontaneous); y += 14;
        Check(y, "PETITS MOTS", m.Messages, MenuAction.ToggleMessages); y += 14;
        Check(y, "JEUX SPONTANÉS", m.MiniGames, MenuAction.ToggleMiniGames); y += 17;

        Text("TAILLE", 8, y + 4, Ink, null);
        string[] sizes = ["P", "M", "G"];
        for (int i = 0; i < 3; i++)
        {
            bool on = m.SizeLevel == i + 2;
            uint fg = Box(58 + i * 18, y, 16, 14, MenuAction.Size, (i + 2).ToString(), true, out int ox, out int oy, selected: on);
            TextC(sizes[i], 58 + i * 18 + 8 + ox, y + 4 + oy, fg);
        }
        y += 20;
        Pill(4, y, W - 8, 15, Glyphs.IcoPencil, "RENOMMER", MenuAction.Rename, m.Registered);
        y += 19;
        Pill(4, y, W - 8, 15, Glyphs.IcoCross, "QUITTER PÉPIN", MenuAction.Quit, true, danger: true);
        TextC("VERSION " + m.Version, W / 2, H - 13, Dim);
    }

    // ------------------------------------------------------------------ éléments

    static void TopBar(string title)
    {
        Fill(1, 1, W - 2, 17, Sky);
        HLine(0, 18, W, Ink);
        IconBtn(4, 3, 16, 13, Glyphs.IcoBack, MenuAction.Back, null, true);
        Text(title, 26, 6, Ink, null);
    }

    static void BottomBar(MenuModel m)
    {
        HLine(0, H - 18, W, Ink);
        Fill(1, H - 17, W - 2, 16, Bar);
        IconBtn(4, H - 15, 16, 13, Glyphs.IcoGear, MenuAction.Go, nameof(MenuPage.Settings), true);
        IconBtn(22, H - 15, 16, 13, Glyphs.IcoNotebook, MenuAction.Go, nameof(MenuPage.Carnet), !m.Egg);
        IconBtn(40, H - 15, 16, 13, m.Paused ? Glyphs.IcoPlay : Glyphs.IcoPause, MenuAction.Pause, null, !m.Egg);
        IconBtn(W - 20, H - 15, 16, 13, Glyphs.IcoCross, MenuAction.Quit, null, true, danger: true);   // à l'écart des autres
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

    /// <summary>Jauge : icône d'encre + barre fine (la couleur ne porte pas le sens, l'icône si).</summary>
    static void Gauge(int x, int y, string[] icon, double value)
    {
        Glyphs.DrawTinted(cv!, icon, x, y, Ink);
        int bx = x + 9, bw = W - 4 - bx;
        Fill(bx, y, bw, 5, Ink);
        Fill(bx + 1, y + 1, bw - 2, 3, Paper);
        int fw = (int)Math.Round((bw - 2) * Math.Clamp(value, 0, 1));
        if (fw > 0)
        {
            Fill(bx + 1, y + 1, fw, 3, Deep);
            Fill(bx + 1, y + 1, fw, 1, PixelCanvas.Lerp(Deep, White, 0.45f));
        }
    }

    /// <summary>
    /// Cadre cliquable : repos = crème bordé d'encre, survol = bleu ciel, appui = bleu plus foncé, `selected` = encre
    /// pleine, indisponible = bord pâle. Renvoie la couleur du contenu ; `oy` : décalage d'1 pixel à l'appui.
    /// </summary>
    static uint Box(int x, int y, int w, int h, MenuAction a, string? arg, bool on, out int ox, out int oy,
                    bool selected = false, bool danger = false)
    {
        int idx = bl.Count;
        bl.Add(new Btn { X = x, Y = y, Wd = w, Ht = h, A = a, Arg = arg, On = on });
        bool isHover = on && idx == hov, isPressed = on && idx == prs;
        ox = 0;
        oy = isPressed ? 1 : 0;
        uint fill = selected ? Ink
            : isPressed ? SkyDark
            : isHover ? (danger ? Cherry : Sky)
            : Paper;
        RoundRect(x, y, w, h, fill, on ? Ink : Track, 2);
        if (!on) return Dim;
        if (selected || (danger && isHover)) return White;
        return danger ? Cherry : Ink;
    }

    /// <summary>Ligne d'action alignée à gauche : icône d'encre, nom ; petit cadenas quand c'est impossible.</summary>
    static void Row(int x, int y, int w, int h, string[] icon, string label, MenuAction a, string? arg, bool on)
    {
        uint fg = Box(x, y, w, h, a, arg, on, out int ox, out int oy);
        Glyphs.DrawTinted(cv!, icon, x + 5 + (9 - icon[0].Length) / 2 + ox, y + (h - icon.Length) / 2 + oy, fg);
        Text(label, x + 19 + ox, y + (h - 7) / 2 + oy, fg, null);
        if (!on) Glyphs.DrawTinted(cv!, Glyphs.IcoLock, x + w - 9, y + (h - 5) / 2, Dim);
    }

    /// <summary>Bouton centré : icône facultative + texte.</summary>
    static void Pill(int x, int y, int w, int h, string[]? icon, string label, MenuAction a, bool on, bool danger = false)
    {
        uint fg = Box(x, y, w, h, a, null, on, out int ox, out int oy, danger: danger);
        int lw = PixelFont.Measure(label), iw = icon?[0].Length ?? 0, total = iw + (icon is null ? 0 : 4) + lw;
        int x0 = x + (w - total) / 2 + ox;
        if (icon is not null) Glyphs.DrawTinted(cv!, icon, x0, y + (h - icon.Length) / 2 + oy, fg);
        Text(label, x0 + (icon is null ? 0 : iw + 4), y + (h - 7) / 2 + oy, fg, null);
    }

    static void IconBtn(int x, int y, int w, int h, string[] icon, MenuAction a, string? arg, bool on, bool danger = false)
    {
        uint fg = Box(x, y, w, h, a, arg, on, out int ox, out int oy, danger: danger);
        Glyphs.DrawTinted(cv!, icon, x + (w - icon[0].Length) / 2 + ox, y + (h - icon.Length) / 2 + oy, fg);
    }

    /// <summary>Case à cocher : pleine (encre + coche) quand c'est activé. Toute la ligne est cliquable.</summary>
    static void Check(int y, string label, bool on, MenuAction a)
    {
        int idx = bl.Count;
        bl.Add(new Btn { X = 4, Y = y, Wd = W - 8, Ht = 12, A = a, On = true });
        if (idx == hov) Fill(4, y, W - 8, 12, Bar);
        RoundRect(7, y + 1, 10, 10, on ? Ink : Paper, Ink, 2);
        if (on)
            foreach (var (dx, dy) in new[] { (2, 5), (3, 6), (4, 7), (5, 6), (6, 5), (7, 4) })
                cv!.Set(7 + dx, y + 1 + dy, White, Layer.Fx);
        Text(label, 22, y + 3, Ink, null);
    }

    // ------------------------------------------------------------------ primitives

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

    static void Blit(PixelCanvas src, int x, int y, float alpha)
    {
        for (int yy = 0; yy < src.H; yy++)
            for (int xx = 0; xx < src.W; xx++)
            {
                uint p = src.Px[yy * src.W + xx];
                if (p >> 24 == 0) continue;
                if (alpha < 1) p = PixelCanvas.Lerp(p, Stripe, 1 - alpha);
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
