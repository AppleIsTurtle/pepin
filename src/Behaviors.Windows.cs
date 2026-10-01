namespace Pepin;

// La tortue et les fenêtres : s'asseoir dessus, se cacher derrière, les pousser ; réactions aux applis.
// Toujours au premier plan : « derrière » une fenêtre = ses pixels sont gommés (Pet.Occluder).

/// <summary>Grimpe sur le bord supérieur d'une fenêtre et s'y installe. Tombe si la fenêtre bouge brusquement.</summary>
sealed class Perch : Behavior
{
    nint hwnd;
    int phase;                   // 0 y va, 1 saute dessus, 2 assis, 3 redescend
    double phaseStart, dur, tx, ty, sy0, pokedUntil = -1, pettedUntil = -1;
    RECT last;

    public override int Fps => phase == 2 ? (T - moveAt < 1 ? 20 : 8) : 20;
    public override bool Interruptible => phase < 2;
    public override string Label => phase == 2 ? "est assis sur une fenêtre" : "grimpe sur une fenêtre";
    double moveAt = -10;

    void Go(int ph) { phase = ph; phaseStart = T; }

    public override void Start(Pet p)
    {
        if (p.Win is null) { Done = true; return; }
        var spots = p.Win.Perches(p.S.Work, (int)(44 * p.Scale));
        double best = double.MaxValue;
        foreach (var s in spots)
        {
            if (s.Y < p.S.Work.Top + 44 * p.Scale) continue;            // la tortue doit tenir au-dessus du bord
            double x = Math.Clamp(p.X, s.X0 + 22 * p.Scale, s.X1 - 22 * p.Scale);
            double d = Math.Abs(x - p.X) + Math.Abs(s.Y - p.Y) + p.R.NextDouble() * 300;
            if (d < best && p.Win.TryGetRect(s.Hwnd, out var r, out bool max, out bool full) && !max && !full)
            {
                best = d; hwnd = s.Hwnd; tx = x; ty = s.Y; last = r;
            }
        }
        if (hwnd == 0) Done = true;
        dur = Rnd(p, 25, 90);
    }

    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        double pt = T - phaseStart;
        switch (phase)
        {
            case 0: // marche jusque sous la barre de titre
                if (p.WalkTo(tx, ty + 14 * p.Scale, 12, dt)) { sy0 = p.Y; Go(1); }
                else if (T > 14) Done = true;
                break;
            case 1: // hop, sur le bord
            {
                double u = Math.Min(1, pt / 0.45);
                p.Y = sy0 + (ty - sy0) * u;
                p.Z = Math.Sin(Math.PI * u) * 10 * p.Scale;
                v.Eyes = Eyes.Determined; v.LegPhase = 1;
                if (u >= 1)
                {
                    p.Z = 0;
                    if (!p.Win!.TryGetRect(hwnd, out last, out _, out _)) { Done = true; return; }
                    Go(2);
                }
                break;
            }
            case 2: // assis
                if (!FollowWindow(p, dt)) return;
                v.LegsTuck = 1;
                v.LegPhase = Every(T, 1.6) ? 1 : 3;                     // balance les pattes dans le vide
                if (T < pettedUntil) { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true; v.Add(FxKind.Hearts, (float)T, 1); }
                else if (T < pokedUntil) { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Exclaim, (float)T); }
                else if (T - moveAt < 1) { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Grin; v.Add(FxKind.Notes, (float)T, 1); } // « wiii »
                else if (pt > 20 && pt % 14 < 5) { v.Eyes = Eyes.Closed; v.Add(FxKind.Zzz, (float)(pt / 2.4)); } // somnole
                else { v.Eyes = Eyes.HalfLid; p.Breathe(2); }
                if (pt > dur) Go(3);
                break;
            case 3: // redescend d'un petit saut
                Done = true;
                p.Next = new Fall(Rnd(p, 30, 70) * p.Scale, p.V.FacingRight ? 60 : -60, gentle: true);
                p.Note("perches", "A pris la pose en haut d'une fenêtre.", oncePerDay: true);
                break;
        }
    }

    /// <summary>Suit la fenêtre ; tombe si elle disparaît, se maximise, file trop vite ou se fait recouvrir.</summary>
    bool FollowWindow(Pet p, double dt)
    {
        var w = p.Win!;
        if (!w.TryGetRect(hwnd, out var r, out bool max, out bool full) || max || full)
        {
            Tumble(p, 0);
            return false;
        }
        int dx = r.Left - last.Left, dy = r.Top - last.Top;
        if (dx != 0 || dy != 0)
        {
            moveAt = T;
            if (Math.Abs(dx) + Math.Abs(dy) > 45 * p.Scale / 3.0) { Tumble(p, dx / Math.Max(dt, 0.02) * 0.4); return false; }
            p.X += dx; p.Y += dy;
        }
        last = r;
        if (p.X < r.Left + 4 * p.Scale || p.X > r.Right - 4 * p.Scale || w.IsCovered((int)p.X, r.Top - 2, hwnd))
        {
            Tumble(p, 0);
            return false;
        }
        p.PerchHwnd = hwnd;
        return true;
    }

    void Tumble(Pet p, double vx)
    {
        Done = true;
        p.Next = new Fall(Rnd(p, 60, 200) * p.Scale, vx, gentle: false);
    }

    public override void OnPoke(Pet p, int count)
    {
        if (phase < 2) { base.OnPoke(p, count); return; }
        if (count >= 4) Tumble(p, 0);                             // à force de l'embêter, il tombe
        else pokedUntil = T + 1;
    }
    public override void OnPetting(Pet p) { if (phase == 2) pettedUntil = T + 1; else base.OnPetting(p); }
    public override void OnStartle(Pet p) { if (phase == 2 && p.R.NextDouble() < 0.3) Tumble(p, 0); else pokedUntil = T + 1; }
}

/// <summary>Chute depuis une fenêtre (sol plus bas, gravité existante).</summary>
sealed class Fall : Behavior
{
    readonly double drop, vx;
    readonly bool gentle;
    public Fall(double dropPx, double vx, bool gentle) { drop = dropPx; this.vx = vx; this.gentle = gentle; }
    public override int Fps => 40;
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override string Label => gentle ? "saute" : "tombe !";
    public override void OnPetting(Pet p) { }
    public override void OnStartle(Pet p) { }

    public override void Start(Pet p)
    {
        double maxDrop = p.S.Work.Bottom - 2 * p.Scale - p.Y;
        double d = Math.Clamp(drop, 0, Math.Max(0, maxDrop));
        p.Y += d; p.Z = d;
        p.VX = vx; p.VY = 0; p.VZ = gentle ? 220 : 0;
    }

    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        p.PhysicsStep(dt, out bool hard);
        v.LegsDangle = p.Z > 0;
        v.LegPhase = p.Z > 0 ? (int)(T * 9) & 3 : 0;
        if (gentle) { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; }
        else { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Sweat, (float)T); }
        if (T > 0.1 && p.Settled)
        {
            p.Z = 0; p.VX = p.VY = p.VZ = 0;
            Done = true;
            if (!gentle) p.Next = drop > 150 * p.Scale / 3 ? new Dizzy() : new Surprised(annoyed: p.R.NextDouble() < 0.5);
        }
    }
}

/// <summary>Cache-cache derrière le bord d'une fenêtre.</summary>
sealed class HideBehind : Behavior
{
    nint hwnd;
    int side;                    // -1 : entre par le bord gauche, +1 : par le bord droit
    int phase;                   // 0 approche, 1 se glisse derrière, 2 caché, 3 jette un œil, 4 ressort
    double phaseStart, edgeX, y, hideFor, peekFor;
    RECT r;
    bool spotted;

    public override int Fps => phase == 2 ? 8 : 15;
    public override string Label => phase >= 2 && phase < 4 ? "se cache derrière une fenêtre" : "joue à cache-cache";
    void Go(int ph) { phase = ph; phaseStart = T; }

    public override void Start(Pet p)
    {
        if (p.Win is null) { Done = true; return; }
        double best = double.MaxValue;
        foreach (var w in p.Win.Windows)
        {
            if (w.Maximized || w.Fullscreen || w.R.Bottom - w.R.Top < 70 * p.Scale) continue;
            foreach (int s in (ReadOnlySpan<int>)[-1, 1])
            {
                double ex = s < 0 ? w.R.Left : w.R.Right;
                double yy = Math.Clamp(p.Y, w.R.Top + 44 * p.Scale, w.R.Bottom - 6 * p.Scale);
                double outX = ex + (s < 0 ? -1 : 1) * 30 * p.Scale;
                if (outX < p.S.Work.Left + 30 * p.Scale || outX > p.S.Work.Right - 30 * p.Scale) continue;
                if (p.Win.IsCovered((int)ex, (int)(yy - 12 * p.Scale), w.Hwnd) || p.Win.IsCovered((int)outX, (int)yy, 0)) continue;
                double d = Math.Abs(outX - p.X) + Math.Abs(yy - p.Y) + p.R.NextDouble() * 200;
                if (d < best) { best = d; hwnd = w.Hwnd; side = s; edgeX = ex; y = yy; r = w.R; }
            }
        }
        if (hwnd == 0) Done = true;
        hideFor = Rnd(p, 2, 5);
        peekFor = Rnd(p, 1.5, 3);
    }

    double Out(Pet p, double logical) => edgeX + (side < 0 ? -1 : 1) * logical * p.Scale;   // >0 : dehors, <0 : derrière

    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (!p.Win!.TryGetRect(hwnd, out var nr, out bool max, out bool full) || max || full)
        {
            if (phase is >= 1 and <= 3) { Done = true; p.Next = new Surprised(annoyed: false); }
            else Done = true;
            return;
        }
        // la fenêtre bouge : la cachette aussi
        int dx = nr.Left - r.Left, dy = nr.Top - r.Top;
        if (dx != 0 || dy != 0) { edgeX += dx; y += dy; if (phase is >= 1 and <= 3) { p.X += dx; p.Y += dy; } }
        r = nr;
        if (phase >= 1) p.Occluder = r;

        double pt = T - phaseStart;
        bool outward = side > 0;                                   // direction « dehors » : vers la droite si bord droit
        switch (phase)
        {
            case 0:
                if (p.WalkTo(Out(p, 30), y, 12, dt)) Go(1);
                else if (T > 15) Done = true;
                break;
            case 1: // tête la première derrière la fenêtre
                if (p.WalkTo(Out(p, -30), y, 8, dt)) Go(2);
                v.FacingRight = !outward;
                break;
            case 2: // caché (invisible) : se retourne
                v.FacingRight = outward;
                if (pt > hideFor) Go(3);
                break;
            case 3: // jette un œil
            {
                p.WalkTo(Out(p, -8), y, 6, dt);
                v.FacingRight = outward;
                v.Eyes = Eyes.Wide;
                double cd = Math.Abs(p.S.CX - edgeX) + Math.Abs(p.S.CY - (y - 14 * p.Scale));
                if (!spotted && cd < 110 * p.Scale / 3 && pt > 0.6)
                {
                    // repéré ! il se recache vite, puis « coucou »
                    spotted = true;
                    v.Add(FxKind.Exclaim, (float)pt);
                    hideFor = 1.2;
                    Go(2);
                    break;
                }
                if (pt > peekFor || spotted) Go(4);
                break;
            }
            case 4: // ressort, content de lui
                v.FacingRight = outward;
                v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
                v.Add(FxKind.Notes, (float)pt, 1);
                if (p.WalkTo(Out(p, 30), y, 12, dt) || pt > 5)
                {
                    Done = true;
                    p.Note("hide", "A joué à cache-cache derrière une fenêtre.", oncePerDay: true);
                }
                break;
        }
    }

    public override void OnPoke(Pet p, int count)
    {
        if (phase is 2 or 3) { spotted = true; Go(4); }
        else base.OnPoke(p, count);
    }
    public override void OnStartle(Pet p) { if (phase == 3) Go(2); else if (phase != 2) base.OnStartle(p); }
}

/// <summary>Rarement : pousse une fenêtre de quelques pixels, ou s'agrippe au bord et la fait trembler.</summary>
sealed class PushWindow : Behavior
{
    nint hwnd;
    int side, phase;             // 0 approche, 1 pousse/secoue, 2 fier
    bool shake;
    double phaseStart, y, edgeX, target, moved;
    int shakeOffset;
    RECT r;

    public override int Fps => phase == 1 ? 20 : 15;
    public override bool Interruptible => phase != 1;
    public override string Label => shake ? "secoue une fenêtre" : "pousse une fenêtre";
    void Go(int ph) { phase = ph; phaseStart = T; }

    public override void Start(Pet p)
    {
        if (p.Win is null) { Done = true; return; }
        var work = p.S.Work;
        double workArea = (double)(work.Right - work.Left) * (work.Bottom - work.Top);
        double best = double.MaxValue;
        foreach (var w in p.Win.Windows)
        {
            double area = (double)(w.R.Right - w.R.Left) * (w.R.Bottom - w.R.Top);
            if (w.Maximized || w.Fullscreen || area > workArea * 0.7 || w.R.Bottom - w.R.Top < 70 * p.Scale) continue;
            foreach (int s in (ReadOnlySpan<int>)[-1, 1])
            {
                double ex = s < 0 ? w.R.Left : w.R.Right;
                double outX = ex + s * 26 * p.Scale;
                double yy = Math.Clamp(p.Y, w.R.Top + 44 * p.Scale, w.R.Bottom - 6 * p.Scale);
                if (outX < work.Left + 30 * p.Scale || outX > work.Right - 30 * p.Scale) continue;
                if (p.Win.IsCovered((int)outX, (int)yy, 0)) continue;
                double d = Math.Abs(outX - p.X) + Math.Abs(yy - p.Y) + p.R.NextDouble() * 200;
                if (d < best) { best = d; hwnd = w.Hwnd; side = s; edgeX = ex; y = yy; r = w.R; }
            }
        }
        if (hwnd == 0) { Done = true; return; }
        shake = p.R.NextDouble() < 0.5;
        target = Rnd(p, 18, 40);                                  // pixels écran, pas plus
    }

    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        double pt = T - phaseStart;
        if (!p.Win!.TryGetRect(hwnd, out var nr, out bool max, out bool full) || max || full) { Stop(); return; }
        edgeX += nr.Left - r.Left; r = nr;
        v.FacingRight = side < 0;                                  // tourné vers la fenêtre

        switch (phase)
        {
            case 0:
                if (p.WalkTo(edgeX + side * 23 * p.Scale, y, 12, dt)) Go(1);
                else if (T > 15) Done = true;
                break;
            case 1:
                // l'humain bouge : on arrête tout de suite (et on remet la fenêtre en place si on la secouait)
                if (p.S.IdleSeconds < 2) { Stop(); p.Next = new Surprised(annoyed: false); return; }
                v.Eyes = shake ? Eyes.Determined : Eyes.Squint; v.Mouth = Mouth.Grin;
                v.Add(FxKind.Sweat, (float)pt);
                if (shake)
                {
                    int want = pt < 1.4 ? (Every(pt, 0.1) ? 3 : -3) : 0;
                    if (want != shakeOffset) { WindowWorld.MoveBy(hwnd, want - shakeOffset, 0); shakeOffset = want; }
                    v.BodyDx = shakeOffset > 0 ? 1 : shakeOffset < 0 ? -1 : 0;
                    if (pt > 1.5) Go(2);
                }
                else
                {
                    int step = (int)Math.Max(1, Math.Round(35 * dt));
                    WindowWorld.MoveBy(hwnd, -side * step, 0);
                    p.X -= side * step;
                    moved += step;
                    v.LegPhase = (int)(T * 6) & 3;
                    if (moved >= target) Go(2);
                }
                break;
            case 2:
                v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
                v.Add(FxKind.Notes, (float)pt, 1);
                if (pt > 1.3)
                {
                    Done = true;
                    p.Note("pushes", shake ? "A secoué une fenêtre, pour voir." : "A poussé une fenêtre de quelques pixels.", oncePerDay: true);
                }
                break;
        }
    }

    void Stop()
    {
        if (shakeOffset != 0) { WindowWorld.MoveBy(hwnd, -shakeOffset, 0); shakeOffset = 0; }
        Done = true;
    }
}

// ================================================================= réactions aux applis (détection locale)

/// <summary>Une vidéo passe : il s'assoit et la regarde avec toi, grignote, rit.</summary>
sealed class WatchVideo : Behavior
{
    double dur, nextEvent, eventUntil;
    int ev;
    public override int Fps => T < eventUntil ? 12 : 6;
    public override bool TrackEyes => false;
    public override string Label => "regarde la vidéo avec toi";
    public override void Start(Pet p)
    {
        dur = Rnd(p, 45, 120);
        nextEvent = Rnd(p, 6, 15);
        p.Note("videos", "A regardé une vidéo avec toi.", oncePerDay: true, isPrivate: true);
    }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        var a = p.Apps;
        if (a is null || a.Foreground != AppActivity.Video || T > dur) { Done = true; return; }
        var r = a.ForegroundRect;
        double cx = (r.Left + r.Right) / 2.0, cy = (r.Top + r.Bottom) / 2.0;
        p.FaceX(cx);
        v.LegsTuck = 3;
        double hx = p.X + (v.FacingRight ? 11 : -11) * p.Scale, hy = p.Y - 14 * p.Scale;
        v.LookX = Math.Abs(cx - hx) > 40 ? 1 : 0;                  // vers l'écran, dans son sens
        v.LookY = cy < hy - 60 ? -1 : cy > hy + 60 ? 1 : 0;
        if (T > nextEvent) { ev = p.R.Next(3); eventUntil = T + (ev == 1 ? 4 : 1.5); nextEvent = T + Rnd(p, 10, 25); }
        if (T < eventUntil)
        {
            switch (ev)
            {
                case 0: v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Add(FxKind.Notes, (float)T, 1); break;        // rit
                case 1: v.Food = Item.Fraise; v.Add(FxKind.Food, 0, (float)(1 - (T - (eventUntil - 4)) / 4)); // popcorn de tortue
                        v.Mouth = Every(T, 0.3) ? Mouth.ChewA : Mouth.ChewB; break;
                default: v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Exclaim, (float)T); break;           // suspense
            }
        }
        else p.Breathe(2);
    }
}

/// <summary>Un rendu 3D tourne : il fait les cent pas et se ronge les ongles.</summary>
sealed class RenderWorry : Behavior
{
    double dur, baseX, baseY;
    public override int Fps => 10;
    public override bool TrackEyes => false;
    public override string Label => "stresse pour ton rendu";
    public override void Start(Pet p)
    {
        dur = Rnd(p, 40, 90);
        (baseX, baseY) = (p.X, p.Y);
        string app = p.Apps?.RenderApp ?? "3D";
        p.Note("renders", $"A surveillé un rendu {app} en se rongeant les ongles.", oncePerDay: true, isPrivate: true);
    }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (p.Apps?.Rendering != true || T > dur) { Done = true; return; }
        double cycle = T % 16;
        if (cycle < 6)
        {
            // les cent pas
            double tx = baseX + (Every(T, 6) ? 30 : -30) * p.Scale;
            p.WalkTo(tx, baseY, 6, dt);
            v.Eyes = Eyes.Normal; v.Mouth = Mouth.Frown;
            if (cycle > 4.5) v.Add(FxKind.Question, (float)T);
        }
        else if (cycle < 13)
        {
            // se ronge les ongles (la patte avant remonte vers la bouche)
            v.LegPhase = 3; v.HeadDy = 1;
            v.Eyes = Eyes.Wide;
            v.Mouth = Every(T, 0.18) ? Mouth.ChewA : Mouth.ChewB;
            v.Add(FxKind.Sweat, (float)(T * 0.7));
        }
        else { v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Frown; v.LegsTuck = 2; }   // soupir
    }
}

/// <summary>Tu tapes depuis longtemps : il te regarde, dodeline, et finit par s'endormir.</summary>
sealed class KeyboardDoze : Behavior
{
    double dur;
    public override int Fps => 8;
    public override bool TrackEyes => false;
    public override string Label => "s'assoupit en te regardant taper";
    public override void Start(Pet p)
    {
        dur = Rnd(p, 15, 25);
        p.Note("typing", "A piqué du nez en te regardant taper.", oncePerDay: true, isPrivate: true);
    }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.LegsTuck = 3;
        v.LookY = 1;                                               // regarde vers le bas, vers « le clavier »
        double k = T / dur;
        if (k < 0.4) v.Eyes = Eyes.Normal;
        else if (k < 0.8) { v.Eyes = Every(T, 1.8, 0.7) ? Eyes.HalfLid : Eyes.Closed; v.HeadDy = Every(T, 1.8, 0.7) ? 0 : 1; }
        else { v.Eyes = Eyes.Closed; v.HeadDy = 1; }
        if (T > dur) { Done = true; p.Next = new Sleep(quick: true); }
    }
}
