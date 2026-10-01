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
    HostVisit? visit;
    (int x, int y) leftFrom;           // d'où ma tortue est partie en visite (pour le petit mot)

    double lastT, lastSave, lastTopmost, lastWinRefresh, lastCarnet = -500, nextUpdateCheck = 20;
    int timerFps;
    bool paused, updating;
    volatile string? readyUpdate;

    string PetName => life.D.Name ?? "Pépin";

    public App(bool updated)
    {
        I = this;
        this.updated = updated;
        mood = Mood.Load();
        sizeLevel = mood.Scale;
        life = new Life(mood.LifeData);
        life.CatchUp(mood.HoursAway);
        band = new Band(mood.LifeData) { OnEvent = OnBandEvent, OnChanged = Save };
        var senses = new Senses();
        var pet = new Pet(mood, senses, updated ? new NewShell() : null) { Win = win, Apps = apps, Life = life, Band = band };
        home = new Creature(pet, senses);
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
        taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        SetupAutostart();
        AddTray();
        Updater.CleanupOld();
        if (updated) life.Write($"A fait peau neuve (version {Updater.Current.ToString(3)}).");
        band.Start();

        Tick();
        ShowWindow(home.Hwnd, SW_SHOWNOACTIVATE);

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
        switch (m)
        {
            case WM_TIMER:
                if (h == home.Hwnd) Tick();
                return 0;
            case WM_MOUSEACTIVATE:
                return MA_NOACTIVATE;
            case WM_SETCURSOR:
                SetCursor(handCursor);
                return 1;
            case WM_LBUTTONDOWN or WM_MOUSEMOVE or WM_LBUTTONUP or WM_CAPTURECHANGED:
                if (Creature.From(h) is Creature c && c.HandleMouse(m))
                {
                    if (c.Pet.Dragging) SetFps(60);
                    return 0;
                }
                break;
            case WM_RBUTTONUP:
                ShowMenu();
                return 0;
            case WM_TRAY:
            {
                int ev = LoWord(l);
                if (ev == WM_RBUTTONUP || ev == WM_LBUTTONUP) ShowMenu();
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
        if (pet.Dragging) fps = 60;
        SetFps(fps);

        if (now - lastSave > 60) { Save(); lastSave = now; }
        if (now - lastCarnet > 600 && life.Dirty && band.Registered) { PushCarnet(); lastCarnet = now; }
        if (now - lastTopmost > 5)
        {
            // certaines applis passent devant : on réaffirme « toujours au premier plan »
            SetWindowPos(home.Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            if (visit is not null) SetWindowPos(visit.Guest.Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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

    // ------------------------------------------------------------------ mise à jour automatique

    void CheckUpdate(double now)
    {
        if (now >= nextUpdateCheck && Updater.Enabled)
        {
            nextUpdateCheck = now + 24 * 3600;
            Updater.CheckInBackground(path => readyUpdate = path);
        }
        var pet = home.Pet;
        // on attend un moment calme : pas de drag, pas de visite en cours
        if (readyUpdate is string path && !updating && !pet.Dragging && visit is null && band.Outgoing is null &&
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

    nint MakeIcon()
    {
        var ic = new PixelCanvas(16, 16);
        TurtleArt.DrawIcon(ic);
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

    const int IdSendRandom = 40, IdSendNote = 41, IdBandPage = 42, IdSpontaneous = 43, IdMessages = 44, IdRename = 45,
              IdBlockGuest = 46, IdCarnetPage = 50, IdSendTo = 1000;

    void ShowMenu()
    {
        var pet = home.Pet;
        band.Poke();
        nint menu = CreatePopupMenu(), sizes = CreatePopupMenu(), carnet = CreatePopupMenu(), bande = CreatePopupMenu(), sendTo = CreatePopupMenu();

        AppendMenuW(menu, MF_STRING | MF_GRAYED, 1, $"{PetName} {pet.Current.Label}");
        AppendMenuW(menu, MF_STRING | MF_GRAYED, 2, mood.Describe());
        AppendMenuW(menu, MF_SEPARATOR, 0, null);

        // carnet
        AppendMenuW(carnet, MF_STRING | MF_GRAYED, 3, life.Summary());
        AppendMenuW(carnet, MF_STRING | MF_GRAYED, 4, life.TodayLine());
        AppendMenuW(carnet, MF_SEPARATOR, 0, null);
        foreach (var j in life.D.Journal.TakeLast(8).Reverse())
        {
            var when = DateTimeOffset.FromUnixTimeSeconds(j.T).ToLocalTime();
            string stamp = when.Date == DateTime.Today ? when.ToString("HH:mm") : when.ToString("dd/MM");
            string text = j.Text.Length > 70 ? j.Text[..69] + "…" : j.Text;
            AppendMenuW(carnet, MF_STRING | MF_GRAYED, 5, $"{stamp}   {text.Replace("&", "&&")}");
        }
        if (band.Registered)
        {
            AppendMenuW(carnet, MF_SEPARATOR, 0, null);
            AppendMenuW(carnet, MF_STRING, IdCarnetPage, "Ouvrir son carnet en ligne");
        }
        AppendMenuW(menu, MF_POPUP, (nuint)carnet, "Carnet");

        // la bande
        bool canSend = band.Registered && band.Outgoing is null && visit is null && pet.Current is not (LeaveForVisit or LeaveScreen or AwayOnVisit);
        string header = !band.Registered ? "La bande : connexion…" : !band.Connected ? "La bande : hors ligne" :
                        $"La bande : {band.BandSize} tortue{(band.BandSize > 1 ? "s" : "")}, {band.Online} en ligne";
        AppendMenuW(bande, MF_STRING | MF_GRAYED, 6, header);
        if (band.Outgoing is VisitDto o) AppendMenuW(bande, MF_STRING | MF_GRAYED, 7, $"{PetName} est chez {o.Host?.Name}");
        if (visit is not null) AppendMenuW(bande, MF_STRING | MF_GRAYED, 8, $"{visit.Info.From?.Name} est en visite ici");
        AppendMenuW(bande, MF_SEPARATOR, 0, null);
        AppendMenuW(bande, canSend ? MF_STRING : MF_GRAYED, IdSendRandom, "Envoyer en visite au hasard");
        var available = band.Turtles.Where(t => t.Online && t.Status == "home").Take(20).ToList();
        for (int i = 0; i < available.Count; i++)
            AppendMenuW(sendTo, canSend ? MF_STRING : MF_GRAYED, (nuint)(IdSendTo + i), available[i].Name.Replace("&", "&&"));
        if (available.Count == 0) AppendMenuW(sendTo, MF_STRING | MF_GRAYED, 9, "personne de dispo pour l'instant");
        AppendMenuW(bande, MF_POPUP, (nuint)sendTo, "Envoyer chez…");
        AppendMenuW(bande, canSend ? MF_STRING : MF_GRAYED, IdSendNote, "Envoyer avec un petit mot…");
        AppendMenuW(bande, MF_SEPARATOR, 0, null);
        AppendMenuW(bande, life.D.SpontaneousVisits ? MF_CHECKED : 0, IdSpontaneous, "Visites spontanées");
        AppendMenuW(bande, life.D.AcceptMessages ? MF_CHECKED : 0, IdMessages, "Accepter les petits mots");
        AppendMenuW(bande, band.Registered ? MF_STRING : MF_GRAYED, IdRename, $"Renommer {PetName}…");
        if (visit is not null) AppendMenuW(bande, MF_STRING, IdBlockGuest, $"Bloquer {visit.Info.From?.Name}");
        AppendMenuW(bande, MF_STRING, IdBandPage, "Voir la bande en ligne");
        AppendMenuW(menu, MF_POPUP, (nuint)bande, "La bande");
        AppendMenuW(menu, MF_SEPARATOR, 0, null);

        AppendMenuW(menu, MF_STRING, 10, pet.Current.Asleep ? "Le réveiller" : "Le mettre au lit");
        AppendMenuW(menu, MF_STRING, 11, "L'appeler ici");
        AppendMenuW(sizes, sizeLevel == 2 ? MF_CHECKED : 0, 20, "Petit");
        AppendMenuW(sizes, sizeLevel == 3 ? MF_CHECKED : 0, 21, "Moyen");
        AppendMenuW(sizes, sizeLevel == 4 ? MF_CHECKED : 0, 22, "Grand");
        AppendMenuW(menu, MF_POPUP, (nuint)sizes, "Taille");
        AppendMenuW(menu, paused ? MF_CHECKED : 0, 30, "Pause (le cacher)");
        AppendMenuW(menu, Autostart ? MF_CHECKED : 0, 31, "Lancer au démarrage de Windows");
        AppendMenuW(menu, MF_STRING | MF_GRAYED, 32, $"Version {Updater.Current.ToString(3)}");
        AppendMenuW(menu, MF_SEPARATOR, 0, null);
        AppendMenuW(menu, MF_STRING, 99, "Quitter");

        POINT p;
        GetCursorPos(&p);
        SetForegroundWindow(home.Hwnd);
        int cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, p.X, p.Y, 0, home.Hwnd, 0);
        PostMessageW(home.Hwnd, WM_NULL, 0, 0);
        DestroyMenu(menu);

        switch (cmd)
        {
            case 10:
                if (pet.Current is AwayOnVisit) break;
                pet.Switch(pet.Current.Asleep ? new WakeUp() : new Sleep());
                break;
            case 11:
                CallHere();
                break;
            case 20 or 21 or 22:
                sizeLevel = cmd - 18;
                mood.Scale = sizeLevel;
                break;
            case 30:
                TogglePause();
                break;
            case 31:
                Autostart = !Autostart;
                break;
            case IdSendRandom:
                SendOnVisit(null, null);
                break;
            case IdSendNote:
            {
                string? msg = InputDialog.Ask("Petit mot", $"Un petit mot que {PetName} portera (80 caractères) :", "", 80, "Envoyer");
                if (!string.IsNullOrWhiteSpace(msg)) SendOnVisit(null, msg);
                break;
            }
            case >= IdSendTo when cmd - IdSendTo < available.Count:
                SendOnVisit(available[cmd - IdSendTo].Id, null);
                break;
            case IdSpontaneous:
                life.D.SpontaneousVisits = !life.D.SpontaneousVisits;
                Save();
                break;
            case IdMessages:
                life.D.AcceptMessages = !life.D.AcceptMessages;
                band.AcceptMessages = life.D.AcceptMessages;
                Save();
                break;
            case IdRename:
            {
                string? name = InputDialog.Ask("Renommer", "Nouveau nom (2 à 16 lettres) :", PetName, 16, "OK");
                if (!string.IsNullOrWhiteSpace(name) && name != PetName)
                    band.Rename(name, err => pet.Say(err is null ? $"Je m'appelle {PetName} !" : Band.ErrorText(err), 3));
                break;
            }
            case IdBlockGuest:
                if (visit?.Info.From?.Id is string gid)
                {
                    band.Block(gid);
                    band.Abort(visit.Info.Id);
                    visit.Cancel();
                }
                break;
            case IdCarnetPage:
                OpenUrl($"{Net.Base}t/{life.D.BandId}");
                break;
            case IdBandPage:
                OpenUrl($"{Net.Base}bande");
                break;
            case 99:
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

    void Save()
    {
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
