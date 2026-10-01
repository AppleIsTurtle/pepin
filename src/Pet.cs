namespace Pepin;

/// <summary>
/// La tortue : position dans le monde, jauges, stimuli et choix du prochain comportement.
/// Coordonnées en pixels écran : (X, Y) = point au sol sous les pattes, Z = hauteur.
/// </summary>
public sealed class Pet
{
    public double X, Y, Z, VX, VY, VZ;
    public double Scale = 3;                  // pixels écran par pixel logique
    public double Time;                       // secondes depuis le lancement
    public readonly Visual V = new();
    public readonly Mood M;
    public readonly Senses S;
    public readonly Random R = Random.Shared;

    // contexte (null pour un visiteur ou dans le simulateur)
    public WindowWorld? Win;
    public AppWatch? Apps;
    public Life? Life;
    public Band? Band;
    public Bowling? Bowl;                     // mini-jeu de bowling (tortue de la maison seulement)
    public GrassEvent? Grass;                 // évènement « touffe d'herbe » (tortue de la maison seulement)
    public bool IsGuest;                      // tortue d'un ami en visite chez nous
    public Pet? Partner;                      // l'autre tortue pendant une visite
    public bool CursorOnMe;                   // le curseur est sur un pixel de la tortue (renseigné par Creature)
    public RECT? Occluder;                    // fenêtre "devant" la tortue : ses pixels y sont gommés
    public nint PerchHwnd;                    // fenêtre sur laquelle elle est assise
    public Func<Behavior?>? Orchestrator;     // pendant une visite : activités imposées aux deux tortues
    public bool OffScreen;                    // en train d'entrer/sortir de l'écran : pas de bornage
    public string? SayText;                   // petite bulle au-dessus de la tortue
    public double SayUntil;

    public void Say(string text, double seconds) { SayText = text; SayUntil = Time + seconds; }

    public Behavior Current { get; private set; } = null!;
    public Behavior? Next;                    // comportement imposé pour la suite
    public bool Dragging;
    public bool MouseDown;                    // bouton gauche enfoncé sur la tortue
    public int Fps => Dragging ? 60 : Current.Fps;

    // interactions rares avec la souris
    public double NextMouseGameAt;
    public double SnackReadyAt;
    public double NextWindowGameAt = 90, NextPushAt = 1200, NextFollowAt = 300, NextGiftAt = 1800, NextAppReactAt = 30;
    public double NextGameAt = 1500;          // prochain mini-jeu spontané (rare)
    bool wasRendering;

    // stimuli
    int clickCount;
    double clickWindowStart = -10;
    double pettingAccum;
    public bool BeingPetted;
    public double PettingContinuous;          // durée de la caresse en cours
    double startleCooldown;
    bool userWasAway;

    // petits automatismes
    double nextBlink = 2, blinkUntil;
    float tintAmount, flush;
    public float FlushTarget;                 // mis à jour par le comportement à chaque tick

    const double Gravity = 1500;

    public Pet(Mood mood, Senses senses, Behavior? first = null)
    {
        M = mood;
        S = senses;
        NextMouseGameAt = R.Next(150, 400);
        SnackReadyAt = 60;
        Switch(first ?? new WakeUp());
    }

    public void Switch(Behavior b)
    {
        if (b is not Thrown and not Fall) { Z = 0; VX = VY = VZ = 0; }   // un saut interrompu ne reste pas suspendu
        OffScreen = false;
        PerchHwnd = 0;
        Current = b;
        b.Start(this);
    }

    // ------------------------------------------------------------------ boucle

    public void Update(double dt)
    {
        Time += dt;
        FlushTarget = 0;
        Occluder = null;
        bool night = DateTime.Now.Hour is < 7 or >= 23;
        M.Tick(dt, Current.Asleep, night);

        Stimuli(dt);
        LifeEvents(dt);

        V.Reset();
        Current.T += dt;
        Current.Tick(this, dt);
        if (Current.Done)
        {
            var n = Next ?? PickNext();
            Next = null;
            Switch(n);
            V.Reset();
            Current.Tick(this, 0);
        }

        if (!Dragging && !OffScreen) ClampToScreen();
        Automatisms(dt);
    }

    void Automatisms(double dt)
    {
        // clignement (au moins une image affichée)
        if (Time > nextBlink)
        {
            blinkUntil = Time + Math.Max(0.13, 1.0 / Fps);
            nextBlink = Time + (R.NextDouble() < 0.2 ? 0.3 : 2.5 + R.NextDouble() * 4);
        }
        V.Blink = Time < blinkUntil;

        // les yeux suivent le curseur
        if (Current.TrackEyes && V.Eyes is Eyes.Normal or Eyes.Wide or Eyes.Determined or Eyes.HalfLid)
        {
            double hxs = X + (V.FacingRight ? 11 : -11) * Scale, hys = Y - Z - 14 * Scale;
            double dx = S.CX - hxs, dy = S.CY - hys;
            if (dx * dx + dy * dy < 700 * 700)
            {
                int sx = Math.Abs(dx) > 30 ? Math.Sign(dx) : 0;
                V.LookX = V.FacingRight ? sx : -sx;
                V.LookY = dy < -70 ? -1 : dy > 70 ? 1 : 0;
            }
        }

        // teinte d'humeur : pâle en dormant, rouge qui monte quand il est en colère
        float target = Current.Asleep ? 0.3f : 0f;
        tintAmount += (target - tintAmount) * (float)Math.Min(1, dt * 1.5);
        V.Tint = PixelCanvas.Rgb(160, 190, 200);
        V.TintAmount = tintAmount;
        flush += (FlushTarget - flush) * (float)Math.Min(1, dt * (FlushTarget > flush ? 4 : 1.5));
        V.Flush = flush < 0.03f ? 0 : flush;

        V.AnimFrame = (int)(Time * 7);
        V.ShadowZ = (int)Math.Round(Z / Scale);
        if (Dragging) V.NoShadow = true;
    }

    // ------------------------------------------------------------------ stimuli

    void Stimuli(double dt)
    {
        // caresse : le curseur bouge doucement sur la tortue, sans clic
        bool rubbing = !Dragging && !MouseDown && CursorOnMe && S.Speed > 15 && S.Speed < 1800;
        pettingAccum = rubbing ? Math.Min(pettingAccum + dt, 1.2) : Math.Max(0, pettingAccum - dt * 1.5);
        bool was = BeingPetted;
        BeingPetted = pettingAccum > 0.35;
        PettingContinuous = BeingPetted ? PettingContinuous + dt : 0;
        if (BeingPetted)
        {
            M.Affection += 0.004 * dt;      // l'attachement se construit sur des jours
            M.Happiness += 0.03 * dt;
            Life?.AddBond(dt / 10);
            if (!was) Life?.Count("pets");
            if (!was || Current.Interruptible) Current.OnPetting(this);
        }

        // sursaut : la souris file à toute vitesse tout près
        startleCooldown -= dt;
        if (!Dragging && startleCooldown <= 0 && S.Speed > 2800 && DistToCursor() < 170)
        {
            startleCooldown = 6;
            Current.OnStartle(this);
        }

        // l'utilisateur part / revient
        if (S.IdleSeconds > 180 && !userWasAway)
        {
            userWasAway = true;
            if (Life?.Tier >= 4 && !Current.Asleep && Current.Interruptible && !IsGuest) Switch(new SadGoodbye());
        }
        else if (userWasAway && S.IdleSeconds < 1)
        {
            userWasAway = false;
            if (!Current.Asleep && Current.Interruptible)
                Switch(Life?.Tier >= 3 ? new JoyDance() : new Greet());
        }
    }

    public void OnClick()
    {
        if (Time - clickWindowStart > 2.5) { clickWindowStart = Time; clickCount = 0; }
        clickCount++;
        if (clickCount > 1) M.Happiness -= 0.01;
        Current.OnPoke(this, clickCount);
        if (clickCount >= 4) clickCount = 0;
    }

    /// <summary>Réaction par défaut à un clic, selon le nombre de clics rapprochés.</summary>
    public void ReactPoke(int count)
    {
        if (count >= 4)
        {
            M.Happiness -= 0.08;
            Life?.AddBond(-1);
            Switch(new Angry(biteCursor: R.NextDouble() < 0.5));
        }
        else if (count >= 2) Switch(new Surprised(annoyed: true));
        else if (M.Affection > 0.6 && R.NextDouble() < 0.6) Switch(new EnjoyPet(giggle: true));
        else Switch(new Surprised(annoyed: false));
    }

    double grabDx, grabDy;

    public void OnGrab()
    {
        Dragging = true;
        grabDx = X - S.CX; grabDy = Y - Z - S.CY;
        Z = 0; VX = VY = VZ = 0;
        Switch(new Grabbed());
    }

    public void DragFollow()
    {
        X = S.CX + grabDx;
        Y = S.CY + grabDy;
    }

    public void OnRelease(double vx, double vy)
    {
        Dragging = false;
        // il était "en l'air" : on pose le sol un peu plus bas pour qu'il retombe dessus
        const double dropLogical = 10;
        Y += dropLogical * Scale;
        Z = dropLogical * Scale;
        double sp = Math.Sqrt(vx * vx + vy * vy);
        if (sp > 3000) { vx *= 3000 / sp; vy *= 3000 / sp; sp = 3000; }
        VX = vx; VY = vy;
        VZ = sp < 150 ? 0 : 150 + Math.Min(sp * 0.12, 300);
        Switch(new Thrown(sp));
        if (sp >= 150) Bowl?.NoteThrow();     // un vrai lancer en carapace compte pour le bowling
    }

    // ------------------------------------------------------------------ cerveau

    /// <summary>Temps passé ensemble, paliers atteints, fins de rendu : ce qui nourrit le lien.</summary>
    void LifeEvents(double dt)
    {
        if (Life is null) return;
        if (!Current.Asleep && S.IdleSeconds < 60) Life.AddBond(dt * 0.2 / 60);
        if (Life.PendingLevelUp > 0 && Current.Interruptible && !Current.Asleep && !Dragging)
        {
            Switch(new LevelUp(Life.PendingLevelUp));
            Life.PendingLevelUp = 0;
        }
        // fin d'un rendu surveillé : petite fête
        bool rendering = Apps?.Rendering == true;
        if (wasRendering && !rendering && Current is RenderWorry) Switch(new Hooray());
        wasRendering = rendering;
    }

    /// <summary>Compte un évènement, et l'écrit au journal (une seule fois par jour si demandé).</summary>
    public void Note(string stat, string? journal = null, bool oncePerDay = false, bool isPrivate = false)
    {
        if (Life is null) return;
        Life.Count(stat);
        if (journal is not null && (!oncePerDay || Life.D.Today.GetValueOrDefault(stat) == 1)) Life.Write(journal, isPrivate);
    }

    readonly List<(double w, Func<Behavior> make, int kind)> picks = [];
    void Add(double w, Func<Behavior> make, int kind = 0) { if (w > 0) picks.Add((w, make, kind)); }

    Behavior PickNext()
    {
        if (Orchestrator?.Invoke() is Behavior imposed) return imposed;
        if (IsGuest) return GuestPick();
        if (Bowl is { Active: true }) return new BowlWait();     // une partie est en cours : on y reste

        double e = M.Energy, h = M.Hunger, hap = M.Happiness, aff = M.Affection;
        bool night = DateTime.Now.Hour is < 7 or >= 23;
        bool bored = S.IdleSeconds > 180;
        if (S.IdleSeconds > 480 || e < 0.1 || (night && bored)) return new Sleep();

        picks.Clear();
        Add(3, () => new Idle());
        Add(2.2 * e, () => new Wander());
        Add(1.5 + 2 * (1 - e) + (bored ? 2 : 0), () => new Rest());
        Add(0.6 + 1.5 * (1 - e) + (bored ? 1 : 0), () => new Yawn());
        Add(0.5, () => new Stretch());
        Add(0.8, () => new LookAround());
        Add(Time >= SnackReadyAt && h > 0.15 ? 0.2 + 3 * h : 0, () => new SnackTime());   // gourmand
        Add(hap > 0.55 ? hap : 0, () => new Hum());
        double sleepW = e < 0.3 ? 6 : e < 0.55 ? 0.8 : 0.15;                              // paresseux
        if (night) sleepW *= 2.5;
        if (bored) sleepW *= 3;
        Add(sleepW, () => new Sleep());
        Add(0.15 * e, () => new Travel());
        Add(0.4, () => new Wiggle());
        Add(hap < 0.3 ? 2.5 : 0, () => new Sulk());

        // jeux avec la souris (rares)
        if (Time >= NextMouseGameAt && e > 0.35 && S.IdleSeconds < 20 && S.CursorOnSameMonitor && !CursorOnMe)
        {
            Add(0.35 * (0.5 + hap), () => new CircleCursor(), 1);
            Add(0.3, () => new AttackCursor(quick: false), 1);
            Add(0.25 * e, () => new ChaseCursor(), 1);
            Add(0.4 * aff, () => new SeekAttention(), 1);
        }

        // les fenêtres
        if (Win is not null && Time >= NextWindowGameAt && e > 0.25)
        {
            Add(0.6, () => new Perch(), 2);
            Add(0.4, () => new HideBehind(), 2);
        }
        if (Win is not null && Time >= NextPushAt && e > 0.5 && S.IdleSeconds > 3)
            Add(0.25, () => new PushWindow(), 3);

        // ce que tu fais (détection locale)
        if (Apps is not null && Time >= NextAppReactAt)
        {
            if (Apps.Rendering) Add(3, () => new RenderWorry(), 6);
            if (Apps.Foreground == AppActivity.Video && S.IdleSeconds > 5) Add(3, () => new WatchVideo(), 6);
            if (Apps.TypingStreak > 300) Add(2, () => new KeyboardDoze(), 6);
        }

        // ce que le lien a débloqué
        int tier = Life?.Tier ?? 0;
        if (tier >= 1 && Time >= NextFollowAt && S.IdleSeconds < 10 && S.CursorOnSameMonitor) Add(0.5 + 0.2 * tier, () => new FollowYou(), 5);
        if (tier >= 2 && S.IdleSeconds > 90 && e < 0.8 && S.CursorOnSameMonitor) Add(2, () => new NapByCursor());
        if (tier >= 2 && Time >= NextGiftAt && S.IdleSeconds < 30 && S.CursorOnSameMonitor) Add(0.3, () => new BringGift(), 4);

        // mini-jeux : rares, et seulement si tu es là, qu'elle est en forme et que rien d'autre ne l'occupe
        if (Bowl is not null && Life?.D.MiniGames == true && Time >= NextGameAt && e > 0.5 && hap > 0.4 &&
            S.IdleSeconds < 20 && S.CursorOnSameMonitor && Apps?.Rendering != true && Apps?.Foreground != AppActivity.Video)
            Add(0.25, () => new BowlSetup(), 7);

        // partir en visite chez un ami de la bande
        if (Band?.CanGoSpontaneously(this) == true) Add(0.3, () => new LeaveForVisit(to: null, message: null));

        double total = 0;
        foreach (var x in picks) total += x.w;
        double r = R.NextDouble() * total;
        var chosen = picks[^1];
        foreach (var x in picks)
        {
            r -= x.w;
            if (r <= 0) { chosen = x; break; }
        }
        switch (chosen.kind)
        {
            case 1: NextMouseGameAt = Time + R.Next(480, 900); break;
            case 2: NextWindowGameAt = Time + R.Next(120, 360); break;
            case 3: NextPushAt = Time + R.Next(1200, 2400); break;
            case 4: NextGiftAt = Time + R.Next(2700, 5400); break;
            case 5: NextFollowAt = Time + R.Next(300, 900); break;
            case 6: NextAppReactAt = Time + R.Next(120, 300); break;
            case 7: NextGameAt = Time + R.Next(5400, 10800); break;
        }
        return chosen.make();
    }

    /// <summary>Un visiteur, entre deux activités à deux : il se contente de petites choses.</summary>
    Behavior GuestPick() => R.Next(4) switch
    {
        0 => new Wander(),
        1 => new LookAround(),
        2 => new Hum(),
        _ => new Idle(),
    };

    // ------------------------------------------------------------------ outils pour les comportements

    public double DistToCursor()
    {
        double dx = S.CX - X, dy = S.CY - (Y - Z - 10 * Scale);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void FaceCursor() => FaceX(S.CX);

    public void FaceX(double tx)
    {
        if (Math.Abs(tx - X) > 4) V.FacingRight = tx > X;
    }

    /// <summary>Marche vers (tx, ty) à `speed` pixels logiques/s. Vrai une fois arrivée.</summary>
    public bool WalkTo(double tx, double ty, double speed, double dt)
    {
        double dx = tx - X, dy = ty - Y, d = Math.Sqrt(dx * dx + dy * dy);
        double step = speed * Scale * dt;
        V.LegPhase = (int)(Time * speed * 0.6) & 3;
        if (Math.Abs(dx) > 2) V.FacingRight = dx > 0;
        if (d <= Math.Max(step, 1)) { X = tx; Y = ty; V.LegPhase = 0; return true; }
        X += dx / d * step;
        Y += dy / d * step;
        return false;
    }

    public (double x, double y) RandomPointNear(double minLogical, double maxLogical)
    {
        double a = R.NextDouble() * Math.PI * 2, d = (minLogical + R.NextDouble() * (maxLogical - minLogical)) * Scale;
        return ClampPoint(X + Math.Cos(a) * d, Y + Math.Sin(a) * d * 0.7);
    }

    // marges : toute la toile (tortue + effets autour) reste visible
    double MinX => S.Work.Left + TurtleArt.AX * Scale;
    double MaxX => S.Work.Right - (TurtleArt.CW - TurtleArt.AX) * Scale;
    double MinY => S.Work.Top + TurtleArt.AY * Scale;
    double MaxY => S.Work.Bottom - 2 * Scale;

    public (double x, double y) ClampPoint(double x, double y) =>
        (Math.Clamp(x, MinX, Math.Max(MinX, MaxX)), Math.Clamp(y, MinY, Math.Max(MinY, MaxY)));

    void ClampToScreen() => (X, Y) = ClampPoint(X, Y);

    /// <summary>Physique de vol/rebond. Renvoie le nombre de chocs contre un bord pendant ce pas.</summary>
    public int PhysicsStep(double dt, out bool landedHard)
    {
        landedHard = false;
        int bonks = 0;
        X += VX * dt;
        Y += VY * dt;
        if (Z > 0 || VZ > 0)
        {
            VZ -= Gravity * dt;
            Z += VZ * dt;
            if (Z <= 0)
            {
                Z = 0;
                if (VZ < -250) { landedHard = true; VZ = -VZ * 0.35; }
                else VZ = 0;
            }
        }
        if (Z <= 0)
        {
            // frottement au sol
            double sp = Math.Sqrt(VX * VX + VY * VY);
            double dec = 1400 * dt;
            if (sp <= dec) VX = VY = 0;
            else { VX -= VX / sp * dec; VY -= VY / sp * dec; }
        }
        if (X < MinX) { X = MinX; if (VX < -200) bonks++; VX = -VX * 0.55; }
        if (X > MaxX) { X = MaxX; if (VX > 200) bonks++; VX = -VX * 0.55; }
        if (Y < MinY) { Y = MinY; if (VY < -200) bonks++; VY = -VY * 0.55; }
        if (Y > MaxY) { Y = MaxY; if (VY > 200) bonks++; VY = -VY * 0.55; }
        return bonks;
    }

    public bool Settled => Z <= 0 && VZ == 0 && VX * VX + VY * VY < 400;

    /// <summary>Petit rebond de la tête pour respirer.</summary>
    public void Breathe(double period = 1.6) => V.HeadDy += (int)(Time / period) % 2;
}
