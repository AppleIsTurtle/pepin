namespace Pepin;

public enum BowlPhase : byte { Off, Prep, Setup, Wait, Throw, Over }
public enum ThrowResult : byte { Gutter, Some, Spare, Strike }

/// <summary>
/// Mini-jeu de bowling : 6 quilles posées dans un coin, que tu renverses en lançant la tortue (le lancer normal,
/// en carapace). Discret : un seul tour de deux lancers, quilles dans une fenêtre qui laisse passer la souris,
/// abandon automatique si tu n'y touches pas.
/// Les distances sont en « u » (pixels logiques) : 1 u = `Scale` pixels écran.
/// </summary>
public sealed class Bowling
{
    public const int PinCount = 6;
    const double PinH = 10;
    const double Near = 8, Far = 62, HalfLane = 26;      // marge côté tireur, profondeur de la piste, demi-largeur
    const double StartDist = 55;                         // du point de lancer à la quille de tête
    const int CW = (int)(Near + Far), CH = 68;           // toile du décor, en pixels logiques
    const double HeadRoom = 39;                          // du sol de la quille de tête au haut de la toile
    const double FallTime = 0.3;

    static readonly string[] PinSprite =
    [
        ".kkk.",
        "kwwwk",
        "kwwwk",
        ".kwk.",
        ".krk.",
        ".kwk.",
        "kwwwk",
        "kwwwk",
        "kwwwk",
        ".kkk.",
    ];

    sealed class Pin
    {
        public double X, Y, VX, VY;      // pieds, pixels écran
        public int State;                // 0 debout, 1 tombe, 2 couchée
        public double FallT, FallDir;
        public double Pop;               // < 0 attend, 0..1 tombe du ciel, 1 posée
        public double Vanish;            // > 0 : s'en va
        public bool Counted;
        public double Speed => Math.Sqrt(VX * VX + VY * VY);
    }

    readonly Overlay ov;
    readonly PixelCanvas canvas = new(CW, CH);
    readonly List<Pin> pins = [];

    double s = 3, dir, rx, ry, sx, sy, winLeft, winTop, xMin, xMax, yMin, yMax;
    double life, waitT;
    nint home;
    double prevX, prevY;                  // position de la carapace à l'image précédente (collisions sur le trajet)
    bool hasPrev;

    public BowlPhase Phase { get; private set; }
    public bool Active => Phase is >= BowlPhase.Prep and <= BowlPhase.Throw;
    public bool Throwing => Phase == BowlPhase.Throw;
    public bool Ready => Phase == BowlPhase.Wait;           // les quilles sont posées : le prochain lancer compte
    public bool Abandoned { get; private set; }
    public bool RoundOver { get; private set; }
    public int Throws { get; private set; }
    public int Fell { get; private set; }
    public int Dir => dir > 0 ? 1 : -1;
    public (double X, double Y) Start => (sx, sy);
    public double WaitT => waitT;

    public Bowling(Overlay overlay) => ov = overlay;

    internal PixelCanvas Canvas => canvas;                 // outil de dev --preview

    /// <summary>Images par seconde dont le décor a besoin (0 = rien à animer).</summary>
    public int Fps => pins.Count == 0 || Phase == BowlPhase.Prep ? 0
                    : Phase == BowlPhase.Setup || !Settled || pins.Exists(p => p.Vanish > 0) ? 40 : 10;

    /// <summary>Plus rien ne bouge : les quilles sont posées, couchées ou parties.</summary>
    public bool Settled
    {
        get
        {
            foreach (var p in pins)
                if (p.Vanish == 0 && (p.State == 1 || p.Pop < 1 || p.Speed > 12)) return false;
            return true;
        }
    }

    // ------------------------------------------------------------------ cycle de vie

    /// <summary>Choisit l'emplacement de la piste près de la tortue. Faux s'il n'y a pas la place.</summary>
    public bool TryStart(Pet p)
    {
        if (Phase != BowlPhase.Off || pins.Count > 0) return false;
        s = p.Scale;
        var w = p.S.Work;
        double span = (StartDist + Far) * s, margin = 14 * s;
        double roomR = w.Right - p.X, roomL = p.X - w.Left;
        dir = roomR >= roomL ? 1 : -1;
        if (w.Right - w.Left < span + margin + 6 * s || w.Bottom - w.Top < (HeadRoom + HalfLane + 3) * s) return false;

        sx = dir > 0 ? Math.Clamp(p.X, w.Left + margin, w.Right - span - 4 * s)
                     : Math.Clamp(p.X, w.Left + span + 4 * s, w.Right - margin);
        ry = Math.Clamp(p.Y, w.Top + HeadRoom * s, w.Bottom - (HalfLane + 3) * s);
        sy = ry;
        rx = sx + dir * StartDist * s;
        winLeft = dir > 0 ? rx - Near * s : rx - Far * s;
        winTop = ry - HeadRoom * s;
        xMin = winLeft + 3 * s; xMax = winLeft + (CW - 3) * s;
        yMin = ry - HalfLane * s; yMax = ry + HalfLane * s;

        // triangle de 6 : 1 quille de tête, puis 2, puis 3 (la tête côté tireur)
        pins.Clear();
        for (int col = 0; col < 3; col++)
            for (int k = 0; k <= col; k++)
                pins.Add(new Pin { X = rx + dir * col * 7 * s, Y = ry + (k - col / 2.0) * 7.5 * s, Pop = -10 });

        Phase = BowlPhase.Prep;
        life = waitT = 0;
        Throws = Fell = 0;
        RoundOver = Abandoned = false;
        return true;
    }

    /// <summary>La tortue est en place : les quilles tombent du ciel une à une.</summary>
    public void Begin()
    {
        if (Phase != BowlPhase.Prep) return;
        Phase = BowlPhase.Setup;
        for (int i = 0; i < pins.Count; i++) pins[i].Pop = -0.18 * i;
    }

    /// <summary>La tortue vient d'être lancée en carapace : ce lancer compte.</summary>
    public void NoteThrow()
    {
        if (Phase == BowlPhase.Wait) { Phase = BowlPhase.Throw; waitT = 0; }
    }

    /// <summary>Compte les quilles tombées par ce lancer, balaie celles par terre et dit où en est le tour.</summary>
    public ThrowResult EndThrow()
    {
        Throws++;
        int newly = 0;
        foreach (var p in pins)
            if (p.State != 0 && p.Vanish == 0 && !p.Counted)
            {
                p.Counted = true;
                newly++;
                p.Vanish = 0.001;
            }
        Fell += newly;
        bool all = Fell >= PinCount;
        RoundOver = all || Throws >= 2;
        Phase = RoundOver ? BowlPhase.Over : BowlPhase.Wait;
        waitT = 0;
        return all ? (Throws == 1 ? ThrowResult.Strike : ThrowResult.Spare) : newly == 0 ? ThrowResult.Gutter : ThrowResult.Some;
    }

    /// <summary>Fin de partie : les quilles disparaissent d'un coup de fumée.</summary>
    public void Stop()
    {
        if (Phase == BowlPhase.Off) return;
        Phase = BowlPhase.Off;
        foreach (var p in pins) if (p.Vanish == 0) p.Vanish = 0.001;
    }

    /// <summary>Tout de suite, sans animation (pause, fermeture).</summary>
    public void Hide()
    {
        Phase = BowlPhase.Off;
        pins.Clear();
        ov.Hide();
    }

    // ------------------------------------------------------------------ boucle

    public void Tick(double dt, Pet pet, int scale, nint homeHwnd)
    {
        home = homeHwnd;
        if (pins.Count == 0) { if (ov.Visible) ov.Hide(); return; }
        if (scale != (int)s && Phase != BowlPhase.Off) Hide();     // la taille a changé : la piste n'est plus au bon endroit
        if (pins.Count == 0) return;

        life += dt;
        switch (Phase)
        {
            case BowlPhase.Setup:
                if (pins.TrueForAll(p => p.Pop >= 1)) { Phase = BowlPhase.Wait; waitT = 0; }
                break;
            case BowlPhase.Wait:
                waitT += dt;
                if (waitT > 45 || life > 300) { Abandoned = true; Stop(); }      // personne ne joue : on range, même en cours de tour
                break;
        }

        Physics(dt, pet);
        pins.RemoveAll(p => p.Vanish > 0.5);
        if (pins.Count == 0) { ov.Hide(); return; }
        Render();
    }

    void Physics(double dt, Pet pet)
    {
        double fr = 800 * dt * s / 3;
        foreach (var p in pins)
        {
            if (p.Vanish > 0) { p.Vanish += dt; continue; }
            if (p.Pop < 1 && Phase != BowlPhase.Prep) p.Pop += dt / 0.4;
            if (p.State == 1 && (p.FallT += dt) > FallTime) p.State = 2;
            double sp = p.Speed;
            if (sp <= 0) continue;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            if (sp <= fr) p.VX = p.VY = 0;
            else { p.VX -= p.VX / sp * fr; p.VY -= p.VY / sp * fr; }
            if (p.X < xMin) { p.X = xMin; p.VX = Math.Abs(p.VX) * 0.5; }
            if (p.X > xMax) { p.X = xMax; p.VX = -Math.Abs(p.VX) * 0.5; }
            if (p.Y < yMin) { p.Y = yMin; p.VY = Math.Abs(p.VY) * 0.5; }
            if (p.Y > yMax) { p.Y = yMax; p.VY = -Math.Abs(p.VY) * 0.5; }
        }

        if (Phase == BowlPhase.Throw && pet.Current is Thrown && pet.Z < 8 * s) HitByTurtle(pet);
        else hasPrev = false;

        // quilles contre quilles : une quille qui file en renverse d'autres
        double min = 6.2 * s;
        for (int i = 0; i < pins.Count; i++)
            for (int j = i + 1; j < pins.Count; j++)
            {
                Pin a = pins[i], b = pins[j];
                if (a.Vanish > 0 || b.Vanish > 0 || a.Pop < 1 || b.Pop < 1) continue;
                double dx = b.X - a.X, dy = b.Y - a.Y, d2 = dx * dx + dy * dy;
                if (d2 >= min * min || d2 < 0.01) continue;
                double d = Math.Sqrt(d2), nx = dx / d, ny = dy / d;
                double vrel = (a.VX - b.VX) * nx + (a.VY - b.VY) * ny;
                if (vrel <= 0) continue;
                a.VX -= nx * vrel * 0.8; a.VY -= ny * vrel * 0.8;
                b.VX += nx * vrel * 0.8; b.VY += ny * vrel * 0.8;
                Knock(a); Knock(b);
            }
    }

    static void Knock(Pin p)
    {
        if (p.State != 0) return;
        p.State = 1;
        p.FallT = 0;
        p.FallDir = Math.Abs(p.VX) > 5 ? Math.Sign(p.VX) : Random.Shared.Next(2) * 2 - 1;
    }

    /// <summary>La carapace qui glisse pousse les quilles qu'elle touche et perd un peu de vitesse.</summary>
    void HitByTurtle(Pet pet)
    {
        double tvx = pet.VX, tvy = pet.VY, sp = Math.Sqrt(tvx * tvx + tvy * tvy);
        double bx = pet.X, by = pet.Y - 3 * s;
        // on teste tout le segment parcouru depuis la dernière image : à 2000 px/s elle avance de 40+ px par image
        double ax = hasPrev ? prevX : bx, ay = hasPrev ? prevY : by;
        prevX = bx; prevY = by; hasPrev = true;
        if (sp < 90) return;
        double thr = (6.5 + 2.5) * s;
        double sx = bx - ax, sy = by - ay, len2 = sx * sx + sy * sy;
        foreach (var p in pins)
        {
            if (p.Vanish > 0 || p.Pop < 1) continue;
            double u = len2 > 0.01 ? Math.Clamp(((p.X - ax) * sx + (p.Y - ay) * sy) / len2, 0, 1) : 1;
            double cx = ax + sx * u, cy = ay + sy * u;          // point du trajet le plus proche de la quille
            double dx = p.X - cx, dy = p.Y - cy, d2 = dx * dx + dy * dy;
            if (d2 >= thr * thr) continue;
            double d = Math.Sqrt(d2), nx = d > 0.1 ? dx / d : tvx / sp, ny = d > 0.1 ? dy / d : tvy / sp;
            double vn = tvx * nx + tvy * ny;
            if (vn <= 0) continue;
            double jitter = (Random.Shared.NextDouble() - 0.5) * 0.3 * sp;
            p.VX = tvx * 0.85 + nx * vn * 0.25 - ny * jitter;
            p.VY = tvy * 0.85 + ny * vn * 0.25 + nx * jitter;
            Knock(p);
            pet.VX -= nx * vn * 0.12;
            pet.VY -= ny * vn * 0.12;
            p.X = cx + nx * thr;
            p.Y = cy + ny * thr;
        }
    }

    // ------------------------------------------------------------------ rendu

    void Render()
    {
        canvas.Clear();
        pins.Sort((a, b) => a.Y.CompareTo(b.Y));
        foreach (var p in pins) DrawPin(p);
        ov.Present(canvas, (int)s, (int)Math.Round(winLeft), (int)Math.Round(winTop), home);
    }

    static double Ease(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);

    void DrawPin(Pin p)
    {
        if (p.Pop <= 0) return;
        double lx = (p.X - winLeft) / s, ly = (p.Y - winTop) / s;
        double z = p.Pop < 1 ? (1 - p.Pop) * (1 - p.Pop) * 16 : 0;

        if (p.Vanish > 0)
        {
            if (p.Vanish < 0.35 && (int)(p.Vanish * 30) % 2 == 0)
                Glyphs.Draw(canvas, Glyphs.Puff, (int)Math.Round(lx), (int)Math.Round(ly) - 4);
            if (p.Vanish > 0.2) return;
        }

        int ix = (int)Math.Round(lx), iy = (int)Math.Round(ly - z);

        // ombre
        uint shadow = PixelCanvas.Argb(70, 0, 0, 0);
        int half = z > 6 ? 1 : 2;
        for (int x = -half; x <= half; x++) canvas.Set((int)Math.Round(lx) + x, (int)Math.Round(ly), shadow, Layer.Fx);

        double ang = p.State == 0 ? 0 : (p.State == 1 ? Ease(p.FallT / FallTime) : 1) * Math.PI / 2 * p.FallDir;
        double cos = Math.Cos(ang), sin = Math.Sin(ang);
        iy -= (int)Math.Round(2.5 * Math.Abs(sin));            // couchée, elle repose sur le flanc

        for (int dy = -13; dy <= 3; dy++)
            for (int dx = -13; dx <= 13; dx++)
            {
                double ox = dx + 0.5, oy = dy + 0.5;
                int sxi = (int)Math.Floor(ox * cos + oy * sin + 2.5);
                int syi = (int)Math.Floor(-ox * sin + oy * cos + PinH);
                if ((uint)syi >= PinSprite.Length || (uint)sxi >= PinSprite[0].Length) continue;
                uint c = PinSprite[syi][sxi] switch { 'k' => Glyphs.K, 'w' => Glyphs.White, 'r' => Glyphs.Red, _ => 0u };
                if (c != 0) canvas.Set(ix + dx, iy + dy, c, Layer.Fx);
            }
    }
}
