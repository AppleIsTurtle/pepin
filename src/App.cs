using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using static Pepin.Native;

namespace Pepin;

/// <summary>
/// Chef d'orchestre : tortue de la maison, visiteur éventuel, bulles, fenêtres et applis perçues,
/// la bande, les mises à jour, l'icône de notification et son menu, la boucle de messages.
/// </summary>
public sealed unsafe class App
{
    public const string WindowClass = "PepinCompagnon";
    public static nint ScreenDc { get; private set; }
    static readonly Stopwatch clock = Stopwatch.StartNew();
    public static double Now => clock.Elapsed.TotalSeconds;
    public static double DpiScale { get; private set; } = 1;
    static App I = null!;

    const uint WM_TRAY = WM_APP + 1;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly bool updated;
    readonly bool eggTest;             // --egg : œuf de démonstration, rien n'est sauvegardé ni envoyé
    Egg? egg;                          // première installation : l'œuf à faire éclore
    bool hatched, announceName;
    public bool AutoTap;               // --egg auto : l'œuf est tapoté tout seul (test sans souris)
    double nextAutoTap = 3;
    nint inst, trayIcon, handCursor;
    uint taskbarCreated;
    int sizeLevel;                     // 2 petit, 3 moyen, 4 grand (à 100 % de zoom Windows)

    readonly Mood mood;
    readonly Life life;
    readonly Band band;
    readonly WindowWorld win = new();
    readonly AppWatch apps = new();
    readonly Creature home;
    readonly Label say = new(), note = new();
    readonly Overlay lane = new();
    readonly Bowling bowling;
    readonly GrassEvent grass = new();
    readonly DeckView deck = new();
    readonly MenuPanel menu = new();
    readonly SnailTrail trail = new();
    readonly MenuModel menuModel = new();
    bool autostartCached;
    double nextGrassAt = Now + 600;      // première touffe 10 min après le lancement, puis toutes les 40 à 90 min
    HostVisit? visit;
    (int x, int y) leftFrom;           // d'où ma tortue est partie en visite (pour le petit mot)

    double lastT, lastSave, lastTopmost, lastWinRefresh, lastCarnet = -500, nextUpdateCheck = 20;
    int timerFps;
    bool paused, updating;
    volatile string? readyUpdate;

    string PetName => life.D.Name ?? "Pépin";

    public App(bool updated, bool eggTest = false, Species? forced = null)
    {
        I = this;
        this.updated = updated;
        this.eggTest = eggTest;
        mood = eggTest ? new Mood { Energy = 0.8, Hunger = 0.3, Happiness = 0.7, Affection = 0.35 } : Mood.Load();
        if (mood.FirstRun || eggTest) mood.LifeData.Egg = true;
        sizeLevel = mood.Scale;
        life = new Life(mood.LifeData);
        life.CatchUp(mood.HoursAway);
        band = new Band(mood.LifeData) { OnEvent = OnBandEvent, OnChanged = Save, SpeciesCode = SpeciesInfo.Code(SpeciesInfo.Parse(mood.LifeData.Species)) };
        bowling = new Bowling(lane);
        var senses = new Senses();
        var pet = new Pet(mood, senses, updated ? new NewShell() : null) { Win = win, Apps = apps, Life = life, Band = band, Bowl = bowling, Grass = grass, Species = SpeciesInfo.Parse(mood.LifeData.Species) };
        home = new Creature(pet, senses);
        if (mood.LifeData.Egg)
            egg = new Egg(forced ?? SpeciesInfo.All[Random.Shared.Next(SpeciesInfo.All.Length)]) { Hatched = OnHatched };
    }

    public int Run()
    {
        inst = GetModuleHandleW(0);
        handCursor = LoadCursorW(0, IDC_HAND);
        fixed (char* cls = WindowClass)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WndProc,
                hInstance = inst,
                hCursor = handCursor,
                lpszClassName = (nint)cls,
            };
            RegisterClassExW(&wc);
        }
        ScreenDc = GetDC(0);

        PlaceAtStart();
        home.Create(inst, ScreenDc, "Pépin");
        if (home.Hwnd == 0) return 1;
        say.Create(inst);
        note.Create(inst);
        lane.Create(inst);
        grass.Create(inst);
        grass.Resolved = OnGrass;
        deck.Create(inst);
        trail.Create(inst);
        menu.Init(inst);
        menu.Model = BuildMenuModel;
        menu.Act = OnMenu;
        taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        if (!eggTest) SetupAutostart();
        autostartCached = Autostart;
        AddTray();
        Updater.CleanupOld();
        if (updated) life.Write($"A fait peau neuve (version {Updater.Current.ToString(3)}).");
        if (egg is not null)
        {
            // l'animal n'existe pas encore : l'œuf tombe à l'endroit où il apparaîtra
            egg.Create(inst);
            home.SetHidden(true);
            home.Senses.Update(Now, 0, home.Pet.X, home.Pet.Y);
            egg.Drop(home.Pet.X, home.Pet.Y, home.Senses.Work, EggScale());
            Save();
        }
        else band.Start();

        Tick();
        if (egg is null) ShowWindow(home.Hwnd, SW_SHOWNOACTIVATE);

        MSG msg;
        while (GetMessageW(&msg, 0, 0, 0) > 0)
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        return 0;
    }

    void PlaceAtStart()
    {
        var pet = home.Pet;
        double x = mood.SavedX, y = mood.SavedY;
        if (double.IsNaN(x) || double.IsNaN(y))
        {
            // première fois : en bas à droite de l'écran principal
            home.Senses.Update(0, 0, 0, 0);
            x = home.Senses.Work.Right - 160;
            y = home.Senses.Work.Bottom - 10;
        }
        pet.X = x; pet.Y = y;
        home.Senses.Update(0, 0, x, y);
        (pet.X, pet.Y) = pet.ClampPoint(x, y);
    }

    // ------------------------------------------------------------------ messages

    [UnmanagedCallersOnly]
    static nint WndProc(nint h, uint m, nint w, nint l)
    {
        try { return I.Handle(h, m, w, l); }
        catch (Exception e) { Log(e); return DefWindowProcW(h, m, w, l); }
    }

    nint Handle(nint h, uint m, nint w, nint l)
    {
        if (menu.Handle(h, m, w, l)) return 0;
        switch (m)
        {
            case WM_TIMER:
                if (h == home.Hwnd) Tick();
                return 0;
            case WM_MOUSEACTIVATE:
                return h == menu.Hwnd ? MA_ACTIVATE : MA_NOACTIVATE;
            case WM_SETCURSOR:
                SetCursor(handCursor);
                return 1;
            case WM_LBUTTONDOWN or WM_MOUSEMOVE or WM_LBUTTONUP or WM_CAPTURECHANGED:
                if (Creature.From(h) is Creature c && c.HandleMouse(m))
                {
                    if (c.Pet.Dragging) SetFps(60);
                    return 0;
                }
                if (Overlay.From(h) is Overlay ov && ov.HandleMouse(m, l)) return 0;
                break;
            case WM_RBUTTONUP:
                if (h != menu.Hwnd) ShowMenu(fromTray: false);
                return 0;
            case WM_TRAY:
            {
                int ev = LoWord(l);
                if (ev == WM_RBUTTONUP || ev == WM_LBUTTONUP) ShowMenu(fromTray: true);
                return 0;
            }
            case WM_QUERYENDSESSION:
                return 1;
            case WM_ENDSESSION:
                Save();
                return 0;
            case WM_DESTROY:
                if (h != home.Hwnd) break;
                Save();
                if (visit is not null) band.AbortNow(visit.Info.Id);
                RemoveTray();
                PostQuitMessage(0);
                return 0;
        }
        if (m == taskbarCreated && taskbarCreated != 0) { AddTray(); return 0; }
        return DefWindowProcW(h, m, w, l);
    }

    // ------------------------------------------------------------------ boucle

    void Tick()
    {
        double now = Now;
        double dt = Math.Min(now - lastT, 0.25);
        lastT = now;
        var pet = home.Pet;

        // perception du bureau (fenêtres ~2×/s, applis : AppWatch se limite lui-même)
        if (now - lastWinRefresh > 0.5)
        {
            Span<nint> own = [home.Hwnd, visit?.Guest.Hwnd ?? 0, 0];
            win.Refresh(own);
            lastWinRefresh = now;
        }
        apps.Update(now, home.Senses.IdleSeconds, home.Senses.CX, home.Senses.CY);

        if (egg is not null && !hatched)
        {
            // avant l'éclosion il n'y a que l'œuf : rien d'autre ne vit ni ne s'affiche
            uint edpi = Math.Max(96u, GetDpiForWindow(home.Hwnd));
            DpiScale = edpi / 96.0;
            if (AutoTap && egg.Phase == EggPhase.Idle && now > nextAutoTap) { egg.DebugTap(); nextAutoTap = now + 1.2; }
            egg.Tick(dt, EggScale(), home.Hwnd);
            if (!hatched)
            {
                SetFps(Math.Max(6, egg.Fps));
                if (now - lastSave > 60) { Save(); lastSave = now; }
                return;
            }
        }
        if (egg is not null)
        {
            egg.Tick(dt, home.Scale, home.Hwnd);           // éclats, rayons et bannière après l'éclosion
            if (egg.Phase == EggPhase.Done) { egg.Destroy(); egg = null; }
        }
        if (announceName && band.Registered && pet.Current is not Hatched)
        {
            announceName = false;
            pet.Say($"Je m'appelle {PetName} !", 4);
        }

        band.DrainUi();
        band.Tier = life.Tier;
        // une tortue qui dort reste visitable (l'invitée fait la sieste à côté) ; « away » = personne devant l'écran
        band.Status = pet.Current is AwayOnVisit or LeaveScreen && band.Outgoing is not null ? "visiting"
                    : paused || home.Senses.IdleSeconds > 600 ? "away" : "home";

        uint dpi = GetDpiForWindow(home.Hwnd);
        if (dpi < 96) dpi = 96;
        DpiScale = dpi / 96.0;
        home.Tick(now, dt, dpi, sizeLevel);
        home.SetHidden(paused || pet.Current is AwayOnVisit);
        trail.Tick(now, pet, home.Scale, home.Hwnd, !home.Hidden);

        if (bowling.Active && (paused || visit is not null || pet.Current is AwayOnVisit or LeaveForVisit or LeaveScreen)) bowling.Stop();
        bowling.Tick(dt, pet, home.Scale, home.Hwnd);

        if (grass.Active && (paused || visit is not null || bowling.Active || pet.Current is AwayOnVisit or LeaveForVisit or LeaveScreen)) grass.Stop();
        MaybeSpawnGrass(now);
        grass.Tick(dt, home.Scale, home.Hwnd);
        deck.Tick(now);
        if (menu.Visible)
        {
            menu.TrackHover((int)home.Senses.CX, (int)home.Senses.CY);
            if (!menu.Animating) menu.Refresh();
        }

        if (visit is not null)
        {
            uint gdpi = Math.Max(96u, GetDpiForWindow(visit.Guest.Hwnd));
            visit.Guest.Tick(now, dt, gdpi, sizeLevel);
            visit.Tick(dt);
            if (visit.Finished) visit = null;
        }

        Bubbles();

        int fps = pet.Fps;
        if (visit is not null) fps = Math.Max(fps, visit.Guest.Pet.Fps);
        fps = Math.Max(fps, Math.Max(bowling.Fps, grass.Fps));
        if (egg is not null) fps = Math.Max(fps, egg.Fps);
        if (pet.Dragging) fps = 60;
        SetFps(fps);

        if (now - lastSave > 60) { Save(); lastSave = now; }
        if (now - lastCarnet > 600 && life.Dirty && band.Registered) { PushCarnet(); lastCarnet = now; }
        if (now - lastTopmost > 5)
        {
            // certaines applis passent devant : on réaffirme « toujours au premier plan »
            SetWindowPos(home.Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            if (visit is not null) SetWindowPos(visit.Guest.Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            lane.PlaceBelow(home.Hwnd);                          // le décor reste juste sous la tortue
            lastTopmost = now;
        }
        CheckUpdate(now);
    }

    /// <summary>Bulle de parole de la tortue, et petit mot laissé quand elle est en visite.</summary>
    void Bubbles()
    {
        var pet = home.Pet;
        int px = Math.Max(1, home.Scale / 2 + 1);
        if (!home.Hidden && pet.SayText is string s && pet.Time < pet.SayUntil)
            say.Show(s, (int)pet.X, (int)(pet.Y - pet.Z - 31 * home.Scale), px);
        else say.Hide();

        if (pet.Current is AwayOnVisit && band.Outgoing is VisitDto o && !paused)
            note.Show($"{PetName} est en visite chez {o.Host?.Name}", leftFrom.x, leftFrom.y, px);
        else
        {
            note.Hide();
            if (pet.Current is not AwayOnVisit) leftFrom = ((int)pet.X, (int)(pet.Y - 4 * home.Scale));
        }
    }

    void SetFps(int fps)
    {
        if (fps == timerFps || paused) return;
        timerFps = fps;
        SetTimer(home.Hwnd, 1, (uint)(1000 / fps), 0);
    }

    // ------------------------------------------------------------------ la bande

    void OnBandEvent(BandEventDto e)
    {
        if (e.Visit is not VisitDto v) return;
        switch (e.Type)
        {
            case "visitor":
                var pet = home.Pet;
                bool busy = visit is not null || paused || band.Outgoing is not null ||
                            pet.Current is AwayOnVisit or LeaveScreen or LeaveForVisit;
                if (busy) { band.Abort(v.Id); break; }
                visit = new HostVisit(v, home, band, life, inst);
                bowling.Stop();                                  // une visite passe avant le jeu
                grass.Stop();
                break;

            case "return":
                OnReturn(v);
                break;

            case "visit_cancelled":
                if (visit?.Info.Id == v.Id) visit.Cancel();
                break;
        }
    }

    void OnReturn(VisitDto v)
    {
        var item = Items.Parse(v.Souvenir);
        string host = v.Host?.Name ?? "";
        bool noOneHome = v.Reason is not null && item is null;
        band.Outgoing = null;
        band.Fast = visit is not null;

        if (noOneHome) life.Write(v.Reason == "personne_a_la_maison" ? $"Visite chez {host} : personne à la maison." : $"Visite chez {host} écourtée.");
        else
        {
            string acts = HostVisit.Describe(v.Played);
            string text = $"Visite chez {host}" + (acts != "" ? $" : {acts}" : "") + (item is Item i ? $". Souvenir : {Items.Label(i)}." : ".");
            life.Write(text);
            life.Count("visits_made");
            life.AddBond(2);
            if (item is Item it) life.AddItem(it);
        }

        var pet = home.Pet;
        if (pet.Current is AwayOnVisit or LeaveScreen) pet.Switch(new ComeBack(item, noOneHome, host));
        else if (!noOneHome) pet.Say(item is Item s ? $"De retour de chez {host} avec {Items.Label(s)} !" : $"De retour de chez {host} !", 4);
        Save();
    }

    void SendOnVisit(string? to, string? message)
    {
        var pet = home.Pet;
        if (paused) TogglePause();
        if (band.Outgoing is not null || visit is not null || pet.Current is LeaveForVisit or LeaveScreen or AwayOnVisit) return;
        band.Poke();
        pet.Switch(new LeaveForVisit(to, message));
    }

    void PushCarnet()
    {
        var d = life.D;
        var c = new CarnetReq { Tier = d.Tier, Bond = Math.Round(d.Bond, 1), Stats = new(d.Totals), Collection = new(d.Collection) };
        foreach (var j in d.Journal.Where(j => !j.Private).TakeLast(60))
            c.Journal.Add(new CarnetEntry { T = j.T, Text = j.Text });
        band.PushCarnet(c);
        life.Dirty = false;
    }

    // ------------------------------------------------------------------ évènements et collection

    void ShowDeck(string title, string subtitle, IReadOnlyDictionary<string, int> collection, IReadOnlyDictionary<string, int>? mine, Species species)
    {
        home.Senses.Update(Now, 0, home.Pet.X, home.Pet.Y);
        deck.Show(title, subtitle, collection, mine, species, home.Senses.Work, (int)Math.Max(2, Math.Round(3 * DpiScale)));
    }

    /// <summary>Une touffe d'herbe de temps en temps, quand tout est calme et que tu es là.</summary>
    void MaybeSpawnGrass(double now)
    {
        if (now < nextGrassAt) return;
        var pet = home.Pet;
        bool calm = life.D.MiniGames && !paused && visit is null && band.Outgoing is null && !bowling.Active && !grass.Active &&
                    !pet.Dragging && pet.Current.Interruptible && !pet.Current.Asleep &&
                    pet.Current is not (AwayOnVisit or LeaveForVisit or LeaveScreen or ComeBack) &&
                    home.Senses.IdleSeconds < 30 && home.Senses.CursorOnSameMonitor &&
                    !apps.Rendering && apps.Foreground != AppActivity.Video;
        if (!calm) { nextGrassAt = now + 60; return; }       // pas maintenant : on réessaie dans une minute
        nextGrassAt = now + Random.Shared.Next(2400, 5400);
        SpawnGrass();
    }

    void SpawnGrass()
    {
        var pet = home.Pet;
        home.Senses.Update(Now, 0, pet.X, pet.Y);
        if (grass.TrySpawn(pet) && pet.Current.Interruptible && !pet.Current.Asleep && !pet.Dragging) pet.Switch(new NoticeTuft());
    }

    /// <summary>Résultat d'un clic sur la touffe : la collection, le carnet, la réaction de la tortue.</summary>
    void OnGrass(GrassOutcome outcome, Item? found)
    {
        var pet = home.Pet;
        bool free = pet.Current.Interruptible && !pet.Dragging && pet.Current is not (AwayOnVisit or LeaveForVisit or LeaveScreen);
        switch (outcome)
        {
            case GrassOutcome.Nothing:
                pet.Say("Rien, que de l'herbe...", 3);
                break;
            case GrassOutcome.Object when found is Item it:
                life.AddItem(it);
                life.Count("finds");
                life.AddBond(0.5);
                life.Write($"A trouvé {Items.Label(it)} dans l'herbe.");
                pet.M.Happiness += 0.03;
                pet.Say($"Trouvé : {Items.Label(it)} !", 3.5);
                if (free) pet.Switch(new Hooray());
                Save();
                break;
            case GrassOutcome.Critter when found is Item an:
            {
                string label = Items.Label(an);
                life.AddItem(an);
                life.Count("critters");
                life.AddBond(1);
                life.Write($"{char.ToUpperInvariant(label[0])}{label[1..]} a surgi de l'herbe.");
                pet.Say($"Oh, {label} !", 3.5);
                if (free) pet.Switch(new WatchCritter());
                Save();
                break;
            }
        }
    }

    // ------------------------------------------------------------------ mise à jour automatique

    void CheckUpdate(double now)
    {
        if (now >= nextUpdateCheck && Updater.Enabled)
        {
            nextUpdateCheck = now + 24 * 3600;
            Updater.CheckInBackground(path => readyUpdate = path);
        }
        var pet = home.Pet;
        // on attend un moment calme : pas de drag, pas de visite en cours, pas d'éclosion
        if (readyUpdate is string path && !updating && egg is null && !eggTest && !pet.Dragging && visit is null && band.Outgoing is null &&
            pet.Current is not (LeaveForVisit or LeaveScreen or AwayOnVisit or ComeBack))
        {
            updating = true;
            Save();
            if (Updater.ApplyAndRestart(path)) DestroyWindow(home.Hwnd);
            else { readyUpdate = null; updating = false; }
        }
    }

    // ------------------------------------------------------------------ zone de notification

    void AddTray()
    {
        if (trayIcon == 0) trayIcon = MakeIcon();
        var nid = NewNid();
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.uCallbackMessage = WM_TRAY;
        nid.hIcon = trayIcon;
        const string tip = "Pépin";
        for (int i = 0; i < tip.Length; i++) nid.szTip[i] = tip[i];
        Shell_NotifyIconW(NIM_ADD, &nid);
    }

    void RemoveTray()
    {
        var nid = NewNid();
        Shell_NotifyIconW(NIM_DELETE, &nid);
    }

    NOTIFYICONDATAW NewNid() => new() { cbSize = (uint)sizeof(NOTIFYICONDATAW), hWnd = home.Hwnd, uID = 1 };

    void RefreshTrayIcon()
    {
        nint old = trayIcon;
        trayIcon = MakeIcon();
        var nid = NewNid();
        nid.uFlags = NIF_ICON;
        nid.hIcon = trayIcon;
        Shell_NotifyIconW(NIM_MODIFY, &nid);
        if (old != 0) DestroyIcon(old);
    }

    nint MakeIcon()
    {
        var ic = new PixelCanvas(16, 16);
        if (egg is not null && !hatched) Egg.DrawIcon(ic);
        else SpeciesArt.DrawIcon(ic, home.Pet.Species);
        const int S = 32;
        var bi = new BITMAPINFOHEADER { biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = S, biHeight = -S, biPlanes = 1, biBitCount = 32 };
        void* b;
        nint color = CreateDIBSection(ScreenDc, &bi, 0, &b, 0, 0);
        uint* p = (uint*)b;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                p[y * S + x] = ic.Px[(y / 2) * 16 + x / 2];
        byte* maskBits = stackalloc byte[S * S / 8];
        for (int i = 0; i < S * S / 8; i++) maskBits[i] = 0;
        nint mask = CreateBitmap(S, S, 1, 1, maskBits);
        var ii = new ICONINFO { fIcon = 1, hbmMask = mask, hbmColor = color };
        nint icon = CreateIconIndirect(&ii);
        DeleteObject(mask);
        DeleteObject(color);
        return icon;
    }

    // ------------------------------------------------------------------ menu

    bool CanPlay => !bowling.Active && !paused && visit is null && band.Outgoing is null &&
                    home.Pet.Current is not (LeaveForVisit or LeaveScreen or AwayOnVisit);
    bool CanSend => band.Registered && band.Outgoing is null && visit is null &&
                    home.Pet.Current is not (LeaveForVisit or LeaveScreen or AwayOnVisit);

    /// <summary>Ouvre le panneau : au-dessus de l'animal (clic droit sur lui) ou du curseur (icône de notification).</summary>
    void ShowMenu(bool fromTray)
    {
        if (menu.Visible) { menu.Close(); return; }
        if (fromTray && Now - menu.ClosedAt < 0.4) return;     // ce clic vient de le fermer (perte du focus)
        band.Poke();
        var pet = home.Pet;
        POINT anchor;
        if (fromTray || home.Hidden) GetCursorPos(&anchor);
        else anchor = new POINT { X = (int)pet.X, Y = (int)(pet.Y - pet.Z - 34 * home.Scale) };
        var mi = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(MonitorFromPoint(anchor, MONITOR_DEFAULTTONEAREST), &mi);
        menu.Open(anchor, mi.rcWork, (int)Math.Max(2, Math.Round(3 * DpiScale)), aboveAnchor: true);
    }

    MenuModel BuildMenuModel()
    {
        var pet = home.Pet;
        var m = menuModel;
        m.Name = PetName;
        m.Species = pet.Species;
        pet.V.CopyTo(m.Portrait);
        m.Tier = life.Tier;
        m.Summary = life.Summary();
        m.Today = life.TodayLine();
        m.Affection = mood.Affection; m.Energy = mood.Energy; m.Belly = 1 - mood.Hunger;
        m.Egg = egg is not null && !hatched;
        m.CanPlay = CanPlay;
        m.CanGrass = CanPlay && !grass.Active;
        m.CanSend = CanSend;
        m.Asleep = pet.Current.Asleep;
        m.Away = pet.Current is AwayOnVisit or LeaveScreen or LeaveForVisit;
        m.Paused = paused;
        m.Autostart = autostartCached;
        m.Spontaneous = life.D.SpontaneousVisits; m.Messages = life.D.AcceptMessages; m.MiniGames = life.D.MiniGames;
        m.Registered = band.Registered;
        m.SizeLevel = sizeLevel;
        m.Version = Updater.Current.ToString(3);
        m.BandStatus = !band.Registered ? "CONNEXION…" : !band.Connected ? "HORS LIGNE" : $"{band.Online}/{band.BandSize} EN LIGNE";
        m.Band.Clear();
        foreach (var t in band.Turtles.OrderByDescending(t => t.Online).Take(6))
            m.Band.Add(new BandEntry(t.Id, t.Name, t.Online, t.Online && t.Status == "home", SpeciesInfo.Parse(t.Species)));
        m.GuestName = visit?.Info.From?.Name;
        m.Journal.Clear();
        foreach (var j in life.D.Journal.TakeLast(8).Reverse())
        {
            var when = DateTimeOffset.FromUnixTimeSeconds(j.T).ToLocalTime();
            m.Journal.Add((when.Date == DateTime.Today ? when.ToString("HH:mm") : when.ToString("dd/MM"), j.Text));
        }
        return m;
    }

    void OnMenu(MenuAction a, string? arg)
    {
        var pet = home.Pet;
        switch (a)
        {
            case MenuAction.SleepWake:
                if (pet.Current is AwayOnVisit) break;
                pet.Switch(pet.Current.Asleep ? new WakeUp() : new Sleep());
                break;
            case MenuAction.CallHere:
                CallHere();
                break;
            case MenuAction.Size:
                sizeLevel = int.Parse(arg!);
                mood.Scale = sizeLevel;
                break;
            case MenuAction.Pause:
                TogglePause();
                break;
            case MenuAction.ToggleAutostart:
                Autostart = !Autostart;
                autostartCached = Autostart;
                break;
            case MenuAction.SendRandom:
                SendOnVisit(null, null);
                break;
            case MenuAction.Bowling:
                if (CanPlay && !pet.Dragging) pet.Switch(new BowlSetup());
                break;
            case MenuAction.Grass:
                if (CanPlay) SpawnGrass();
                break;
            case MenuAction.Collection:
                ShowDeck($"Collection de {PetName}", life.Summary(), life.D.Collection, null, pet.Species);
                break;
            case MenuAction.DeckOf when arg is not null:
                band.FetchTurtle(arg, (d, err) =>
                {
                    if (d is null) { pet.Say(Band.ErrorText(err), 3); return; }
                    string tier = Life.TierNames[Math.Clamp(d.Tier, 0, 4)];
                    string sub = d.Friendship > 0 ? $"{tier} - amitié {d.Friendship}" : tier;
                    ShowDeck($"Collection de {d.Name}", sub, d.Collection, life.D.Collection, SpeciesInfo.Parse(d.Species));
                });
                break;
            case MenuAction.ToggleMiniGames:
                life.D.MiniGames = !life.D.MiniGames;
                Save();
                break;
            case MenuAction.SendNote:
            {
                string? msg = InputDialog.Ask("Petit mot", $"Un petit mot que {PetName} portera (80 caractères) :", "", 80, "Envoyer");
                if (!string.IsNullOrWhiteSpace(msg)) SendOnVisit(null, msg);
                break;
            }
            case MenuAction.SendTo when arg is not null:
                SendOnVisit(arg, null);
                break;
            case MenuAction.ToggleSpontaneous:
                life.D.SpontaneousVisits = !life.D.SpontaneousVisits;
                Save();
                break;
            case MenuAction.ToggleMessages:
                life.D.AcceptMessages = !life.D.AcceptMessages;
                band.AcceptMessages = life.D.AcceptMessages;
                Save();
                break;
            case MenuAction.Rename:
            {
                string? name = InputDialog.Ask("Renommer", "Nouveau nom (2 à 16 lettres) :", PetName, 16, "OK");
                if (!string.IsNullOrWhiteSpace(name) && name != PetName)
                    band.Rename(name, err => pet.Say(err is null ? $"Je m'appelle {PetName} !" : Band.ErrorText(err), 3));
                break;
            }
            case MenuAction.BlockGuest:
                if (visit?.Info.From?.Id is string gid)
                {
                    band.Block(gid);
                    band.Abort(visit.Info.Id);
                    visit.Cancel();
                }
                break;
            case MenuAction.CarnetPage:
                if (band.Registered) OpenUrl($"{Net.Base}t/{life.D.BandId}");
                break;
            case MenuAction.BandPage:
                OpenUrl($"{Net.Base}bande");
                break;
            case MenuAction.Quit:
                DestroyWindow(home.Hwnd);
                break;
        }
    }

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception e) { Log(e); }
    }

    void CallHere()
    {
        var pet = home.Pet;
        if (pet.Current is AwayOnVisit or LeaveScreen or LeaveForVisit) return;
        double now = Now;
        home.Senses.Update(now, 0, pet.X, pet.Y);
        if (paused) TogglePause();
        if (!home.Senses.CursorOnSameMonitor)
        {
            // autre écran : il y apparaît directement
            pet.X = home.Senses.CX; pet.Y = home.Senses.CY + 14 * home.Scale;
            home.Senses.Update(now, 0, pet.X, pet.Y);
            (pet.X, pet.Y) = pet.ClampPoint(pet.X, pet.Y);
            pet.Switch(new Greet());
        }
        else pet.Switch(new Travel(home.Senses.CX, home.Senses.CY + 14 * home.Scale));
    }

    void TogglePause()
    {
        paused = !paused;
        if (paused)
        {
            KillTimer(home.Hwnd, 1);
            timerFps = 0;
            home.SetHidden(true);
            say.Hide();
            note.Hide();
            trail.Clear();
            bowling.Hide();
            grass.Stop();
            deck.Hide();
            // un visiteur ne reste pas si on cache tout
            if (visit is not null) { band.Abort(visit.Info.Id); visit.Cleanup(); visit = null; }
            // la pause arrête le timer : on rafraîchit l'état « away » tout de suite
            band.Status = "away";
            band.Poke();
        }
        else
        {
            lastT = Now;
            home.SetHidden(false);
            SetFps(home.Pet.Fps);
        }
    }

    // ------------------------------------------------------------------ démarrage auto, sauvegarde

    /// <summary>
    /// Premier lancement : il s'inscrit au démarrage de Windows (décochable dans le menu).
    /// Ensuite, si c'est actif, on remet à jour le chemin au cas où l'exe a été déplacé.
    /// </summary>
    void SetupAutostart()
    {
        try
        {
            if (!mood.AutostartSet)
            {
                Autostart = true;
                mood.AutostartSet = true;
                Save();
            }
            else if (Autostart) Autostart = true;
        }
        catch (Exception e) { Log(e); }
    }

    static bool Autostart
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue("Pepin") is string;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue("Pepin", $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue("Pepin", false);
        }
    }

    /// <summary>Taille des pixels de l'œuf : comme ceux de l'animal (taille choisie × zoom Windows).</summary>
    int EggScale()
    {
        uint dpi = Math.Max(96u, GetDpiForWindow(home.Hwnd));
        return Math.Max(1, (int)Math.Round(sizeLevel * dpi / 96.0));
    }

    /// <summary>Flash de l'éclosion : l'animal tiré au sort apparaît à la place de l'œuf.</summary>
    void OnHatched(Species s)
    {
        hatched = true;
        var pet = home.Pet;
        pet.Species = s;
        band.SpeciesCode = SpeciesInfo.Code(s);
        if (!eggTest) life.Hatch(s);
        else { life.D.Egg = false; life.D.Species = SpeciesInfo.Code(s); }
        pet.X = egg!.X; pet.Y = egg.Y; pet.Z = 0;
        pet.Switch(new Hatched());
        home.SetHidden(false);
        RefreshTrayIcon();
        lastT = Now;
        if (!eggTest)
        {
            band.Start();
            announceName = true;
            Save();
        }
    }

    void Save()
    {
        if (eggTest) return;
        var pet = home.Pet;
        // en visite, on se souvient d'où elle est partie plutôt que de sa position hors écran
        bool away = pet.Current is AwayOnVisit or LeaveScreen || pet.OffScreen;
        mood.Save(away ? leftFrom.x : pet.X, away ? leftFrom.y + 4 * home.Scale : pet.Y);
    }

    public static void Log(Exception e)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pepin");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTime.Now:s} {e}\n");
        }
        catch { }
    }
}
