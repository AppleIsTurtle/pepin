namespace Pepin;

/// <summary>
/// Une visite reçue : la tortue d'un ami apparaît à côté de la nôtre, elles enchaînent des activités à deux
/// pendant la durée fixée par le serveur, puis l'invitée repart avec un souvenir.
/// Il n'y a aucune limite au nombre de visiteurs : les premiers sont dessinés (une fenêtre chacun), les suivants
/// « font foule » sans fenêtre (<see cref="MaxShown"/>) mais comptent pareil. Notre tortue joue avec un seul visiteur à la fois.
/// </summary>
public sealed class HostVisit
{
    public readonly VisitDto Info;
    public readonly Creature? Guest;               // null = visiteur « dans la foule », sans fenêtre
    readonly Creature home;
    readonly Band band;
    readonly Life? life;
    readonly Label tag = new();
    readonly Queue<string> plan = new();
    readonly List<string> played = [];
    readonly string guestName;
    double t, messageUntil, actStart;
    int state;                       // 0 arrive, 1 activités, 2 au revoir, 3 repart, 4 fini
    Behavior? guestAct;
    double nextTry;
    public bool Finished { get; private set; }

    public const int MaxShown = 8;
    static readonly List<HostVisit> live = [];
    static HostVisit? holder;                      // le visiteur qui joue en ce moment avec notre tortue

    /// <summary>Les visites en cours (la plus récente en dernier).</summary>
    public static IReadOnlyList<HostVisit> Live => live;
    public static int Shown => live.Count(v => v.Guest is not null);

    static readonly Dictionary<string, string> Phrases = new()
    {
        ["renifler"] = "reniflage", ["chat"] = "partie de chat", ["gouter"] = "goûter partagé",
        ["sieste"] = "sieste à deux", ["danse"] = "danse",
    };

    public static string Describe(IEnumerable<string>? played) =>
        string.Join(", ", (played ?? []).Select(a => Phrases.GetValueOrDefault(a, a)));

    public HostVisit(VisitDto info, Creature home, Band band, Life? life, nint inst)
    {
        Info = info;
        this.home = home;
        this.band = band;
        this.life = life;
        guestName = info.From?.Name ?? "Un ami";

        int slot = Shown;
        if (slot < MaxShown)
        {
            var senses = new Senses();
            var mood = new Mood { Energy = 0.9, Hunger = 0.2, Happiness = 0.8, Affection = 0.5 };
            var pet = new Pet(mood, senses, new Idle())
            {
                IsGuest = true, Partner = home.Pet, X = home.Pet.X, Y = home.Pet.Y, Scale = home.Scale, Slot = slot,
                Species = SpeciesInfo.Parse(info.From?.Species),        // le visiteur garde sa vraie forme
            };
            senses.Update(App.Now, 0, pet.X, pet.Y);
            pet.Switch(new Arrive());
            Guest = new Creature(pet, senses);
            Guest.Create(inst, App.ScreenDc, "Pépin (visiteur)");
            tag.Create(inst);
        }

        live.Add(this);
        Retarget(home);

        band.Hosting = true;
        band.Fast = true;
        band.Accept(info.Id);

        // programme : on se renifle d'abord ; sieste d'office si notre tortue dort
        if (home.Pet.Current.Asleep) plan.Enqueue("sieste");
        plan.Enqueue("renifler");
        var rest = new List<string> { "chat", "gouter", "danse" };
        if (home.Pet.M.Energy < 0.6) rest.Add("sieste");
        foreach (var a in rest.OrderBy(_ => Random.Shared.Next())) plan.Enqueue(a);
    }

    /// <summary>Notre tortue regarde le visiteur qui joue avec elle, à défaut le premier visiteur dessiné.</summary>
    static void Retarget(Creature home)
    {
        var lead = (holder?.Guest ?? live.FirstOrDefault(v => v.Guest is not null)?.Guest)?.Pet;
        home.Pet.Partner = lead;
        home.Pet.Orchestrator = lead is null ? null : () => new WatchFriend();
    }

    void Release()
    {
        if (holder != this) return;
        holder = null;
        Retarget(home);
    }

    /// <summary>Un visiteur de la foule : pas de fenêtre, il passe son temps puis repart avec un souvenir.</summary>
    void TickCrowd(double dt)
    {
        t += dt;
        if (t > Info.Duration) Finish(completed: true);
    }

    public void Tick(double dt)
    {
        if (Finished) return;
        if (Guest is null) { TickCrowd(dt); return; }
        t += dt;
        var g = Guest.Pet;
        var h = home.Pet;

        // étiquette : le nom, ou le petit mot pendant quelques secondes
        if (!Guest.Hidden && Guest.WinX != int.MinValue)
        {
            string text = t < messageUntil && Info.Message is string m ? $"{guestName} : « {m} »" : guestName;
            tag.Show(text, (int)g.X, (int)(g.Y - g.Z - 31 * Guest.Scale), Math.Max(1, Guest.Scale / 2 + 1));
        }

        switch (state)
        {
            case 0:
                if (g.Current is not Arrive)
                {
                    state = 1;
                    if (Info.Message is not null) messageUntil = t + 10;
                }
                break;
            case 1:
                bool actOver = guestAct is null || guestAct.Done || g.Current != guestAct || t - actStart > 45;
                if (!actOver) break;
                Release();
                // l'invitée réagit à quelque chose (attrapée, lancée, sonnée…) : on attend qu'elle ait fini
                if (g.Dragging || g.Current != guestAct && g.Current is not (Idle or Wander or LookAround or Hum or Sniff or TagGame or ShareSnack or NapTogether or DanceTogether)) break;
                if (t > Info.Duration || plan.Count == 0 && t > Info.Duration * 0.6) { SayGoodbye(); break; }
                // notre tortue joue déjà avec un autre visiteur : celui-ci se promène en attendant son tour
                if (holder is not null) { nextTry = t + 3; guestAct = null; }
                if (t < nextTry) break;
                if (plan.Count == 0) foreach (var a in new[] { "chat", "danse", "gouter" }.OrderBy(_ => Random.Shared.Next())) plan.Enqueue(a);
                StartActivity(plan.Dequeue());
                break;
            case 2:
                if (g.Current is not Goodbye)
                {
                    g.Switch(new LeaveScreen(new AwayOnVisit()));
                    Release();
                    state = 3;
                }
                break;
            case 3:
                if (g.Current is AwayOnVisit) Finish(completed: true);
                break;
        }
    }

    /// <summary>Notre tortue est libre (on n'interrompt jamais un câlin, un jeu avec toi, une réaction…).</summary>
    bool HostAvailable => (holder is null || holder == this) && !home.Pet.Dragging &&
        home.Pet.Current is WatchFriend or Idle or Rest or LookAround or Hum or Wander or Yawn or Stretch or Wiggle or Goodbye;

    void StartActivity(string act)
    {
        var g = Guest!.Pet;
        var h = home.Pet;
        bool hostIn = HostAvailable || (act == "sieste" && h.Current.Asleep);
        Behavior gb, hb;
        switch (act)
        {
            case "renifler": gb = new Sniff(mover: true); hb = new Sniff(mover: false); break;
            case "chat":
                var st = new TagState { It = Random.Shared.NextDouble() < 0.5 ? g : h };
                double d = 10 + Random.Shared.NextDouble() * 5;
                gb = new TagGame(true, st, d); hb = new TagGame(true, st, d);
                if (!hostIn) st.It = g;
                break;
            case "gouter": gb = new ShareSnack(mover: true, giver: false); hb = new ShareSnack(mover: false, giver: true); break;
            case "sieste":
                double nap = 20 + Random.Shared.NextDouble() * 15;
                gb = new NapTogether(mover: true, nap); hb = new NapTogether(mover: false, nap);
                break;
            default: gb = new DanceTogether(mover: true); hb = new DanceTogether(mover: false); break;
        }
        g.Switch(gb);
        guestAct = gb;
        actStart = t;
        if (hostIn)
        {
            holder = this;
            Retarget(home);
            if (!(act == "sieste" && h.Current.Asleep)) h.Switch(hb);
            if (!played.Contains(act)) played.Add(act);
        }
    }

    void SayGoodbye()
    {
        state = 2;
        Guest!.Pet.Switch(new Goodbye());
        if (HostAvailable) home.Pet.Switch(new Goodbye());
    }

    /// <summary>L'ami annule, ou on bloque, ou on quitte : l'invitée file sans souvenir.</summary>
    public void Cancel()
    {
        cancelled = true;
        if (Guest is null) { Cleanup(); return; }
        if (state < 3)
        {
            state = 3;
            Guest.Pet.Switch(new LeaveScreen(new AwayOnVisit()));
        }
    }
    bool cancelled;

    void Finish(bool completed)
    {
        if (completed && !cancelled)
        {
            var souvenir = Items.Random(Random.Shared);
            band.End(Info.Id, souvenir, played);
            string acts = Describe(played);
            life?.Write($"Visite de {guestName}" + (acts != "" ? $" : {acts}." : "."));
            if (life is not null) { life.Count("visits_hosted"); life.AddBond(1); }
        }
        Cleanup();
    }

    public void Cleanup()
    {
        if (Finished) return;
        Finished = true;
        tag.Destroy();
        Guest?.Destroy();
        live.Remove(this);
        Release();
        Retarget(home);
        band.Hosting = live.Count > 0;
        band.Fast = band.Hosting || band.Outgoing is not null;
    }
}
