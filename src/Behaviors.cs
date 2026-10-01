namespace Pepin;

/// <summary>
/// Un comportement pilote la tortue tant qu'il n'est pas `Done`.
/// À chaque tick, `Visual` vient d'être remis à zéro : le comportement décrit l'image voulue.
/// </summary>
public abstract class Behavior
{
    public double T;                          // temps écoulé dans ce comportement
    public bool Done;
    public virtual int Fps => 12;
    public virtual bool TrackEyes => true;
    public virtual bool Interruptible => true;
    public virtual bool Asleep => false;
    public virtual string Label => "traîne";

    public virtual void Start(Pet p) { }
    public abstract void Tick(Pet p, double dt);

    public virtual void OnPetting(Pet p) { if (Interruptible) p.Switch(new EnjoyPet(giggle: false)); }
    public virtual void OnPoke(Pet p, int count) => p.ReactPoke(count);
    public virtual void OnStartle(Pet p)
    {
        if (Interruptible) p.Switch(p.R.NextDouble() < 0.35 ? new HideInShell() : new Surprised(annoyed: false));
    }

    protected static double Rnd(Pet p, double a, double b) => a + p.R.NextDouble() * (b - a);
    protected static bool Every(double t, double period, double duty = 0.5) => t % period < period * duty;

    /// <summary>Petit saut sur place (hauteur en pixels logiques).</summary>
    protected static void Hop(Pet p, double t, double duration, double height) =>
        p.Z = t >= 0 && t < duration ? Math.Sin(Math.PI * t / duration) * height * p.Scale : 0;
}

// ======================================================================== ambiants

sealed class Idle : Behavior
{
    double dur, nextTurn;
    public override int Fps => 8;
    public override void Start(Pet p) { dur = Rnd(p, 3, 8); nextTurn = Rnd(p, 1, 3); }
    public override void Tick(Pet p, double dt)
    {
        p.Breathe();
        if (T > nextTurn)
        {
            nextTurn = T + Rnd(p, 2, 4);
            if (p.DistToCursor() < 450) p.FaceCursor();
        }
        if (T > dur) Done = true;
    }
}

sealed class Wander : Behavior
{
    double tx, ty, speed, pauseAt = -1;
    public override int Fps => 15;
    public override string Label => "se promène";
    public override void Start(Pet p)
    {
        (tx, ty) = p.RandomPointNear(30, 110);
        speed = Rnd(p, 6, 9);
        if (p.R.NextDouble() < 0.3) pauseAt = Rnd(p, 1.5, 4);
    }
    public override void Tick(Pet p, double dt)
    {
        if (pauseAt > 0 && T >= pauseAt && T < pauseAt + 1.6) { p.Breathe(); return; }
        if (p.WalkTo(tx, ty, speed, dt) || T > 20) Done = true;
    }
}

sealed class Rest : Behavior
{
    double dur;
    public override int Fps => T < 0.5 ? 15 : 6;
    public override string Label => "se repose";
    public override void Start(Pet p) => dur = Rnd(p, 8, 18);
    public override void Tick(Pet p, double dt)
    {
        p.V.LegsTuck = Math.Min(3, (int)(T * 8));
        p.V.Eyes = Eyes.HalfLid;
        p.Breathe(2.2);
        if (T > dur)
        {
            Done = true;
            if (p.M.Energy < 0.3 && p.R.NextDouble() < 0.7) p.Next = new Sleep(quick: true);
        }
    }
}

sealed class Yawn : Behavior
{
    public override bool TrackEyes => false;
    public override string Label => "bâille";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (T < 0.5) { v.HeadDy = -1; v.Eyes = Eyes.Squint; v.Mouth = Mouth.Oh; }
        else if (T < 1.9) { v.HeadDy = -2; v.Eyes = Eyes.Closed; v.Mouth = Mouth.Yawn; }
        else if (T < 2.5) { v.Eyes = Eyes.HalfLid; v.Mouth = Every(T, 0.25) ? Mouth.ChewA : Mouth.ChewB; }
        else Done = true;
    }
}

sealed class Stretch : Behavior
{
    public override int Fps => T > 1.6 ? 20 : 10;
    public override bool TrackEyes => false;
    public override string Label => "s'étire";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (T < 0.4) v.HeadDx = 1;
        else if (T < 1.6) { v.HeadDx = 2; v.HeadDy = -2; v.Eyes = Eyes.Squint; v.Mouth = Mouth.Oh; v.LegPhase = 3; }
        else if (T < 2.4) { v.BodyDx = Every(T, 0.12) ? 1 : -1; v.Eyes = Eyes.Closed; }
        else Done = true;
    }
}

sealed class LookAround : Behavior
{
    bool startFacing, showQ;
    public override int Fps => 8;
    public override bool TrackEyes => false;
    public override string Label => "regarde autour de lui";
    public override void Start(Pet p) { startFacing = p.V.FacingRight; showQ = p.R.NextDouble() < 0.35; }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        switch ((int)(T / 0.9))
        {
            case 0: v.LookX = 1; break;
            case 1: v.FacingRight = !startFacing; v.LookX = 1; break;
            case 2: v.FacingRight = startFacing; v.LookX = -1; break;
            case 3: break;
            default: Done = true; break;
        }
        if (showQ && T > 1.8 && T < 3.2) v.Add(FxKind.Question);
    }
}

sealed class SnackTime : Behavior
{
    Item food;
    const int Bites = 5;
    const double BiteDur = 1.0, Intro = 0.9;
    public override bool TrackEyes => false;
    public override string Label => food == Item.Fraise ? "grignote une fraise" : "grignote de la salade";
    public override void Start(Pet p) => food = p.R.NextDouble() < 0.45 ? Item.Fraise : Item.Salade;
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.Food = food;
        if (T < Intro)
        {
            v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh;
            v.Add(FxKind.Exclaim, (float)T);
            v.Add(FxKind.Food, 0, 1);
        }
        else if (T < Intro + Bites * BiteDur)
        {
            double bt = T - Intro;
            int bite = (int)(bt / BiteDur);
            double inBite = bt - bite * BiteDur;
            bool biting = inBite < 0.18;
            float remaining = 1 - (bite + (biting ? 0 : 1)) / (float)Bites;
            v.Mouth = biting ? Mouth.Bite : Every(inBite, 0.28) ? Mouth.ChewA : Mouth.ChewB;
            v.Eyes = biting ? Eyes.Normal : Eyes.Happy;
            v.HeadDy = biting ? 1 : 0;
            if (remaining > 0) v.Add(FxKind.Food, 0, remaining);
            v.Add(FxKind.Crumbs, (float)inBite);
        }
        else if (T < Intro + Bites * BiteDur + 1.4)
        {
            v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true;
            v.Add(FxKind.Hearts, (float)(T * 0.7), 1);
        }
        else
        {
            Done = true;
            p.M.Hunger -= 0.55;
            p.M.Happiness += 0.08;
            p.Note("snacks");
            p.SnackReadyAt = p.Time + 420;
        }
    }
}

sealed class Hum : Behavior
{
    double dur;
    public override int Fps => 8;
    public override bool TrackEyes => false;
    public override string Label => "fredonne";
    public override void Start(Pet p) => dur = Rnd(p, 5, 8);
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.Eyes = Eyes.Happy;
        v.Mouth = Every(T, 1) ? Mouth.Smile : Mouth.Grin;
        v.HeadDx = Every(T, 1) ? 0 : 1;
        v.Add(FxKind.Notes, (float)(T * 0.45), 2);
        if (T > dur) Done = true;
    }
}

sealed class Wiggle : Behavior
{
    public override int Fps => 20;
    public override bool TrackEyes => false;
    public override string Label => "se secoue";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (T < 1.2) { v.BodyDx = Every(T, 0.14) ? 1 : -1; v.Eyes = Eyes.Squint; v.Mouth = Mouth.Grin; }
        else { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Smile; }
        if (T < 0.6) v.Add(FxKind.Dust, (float)(T / 0.6));
        if (T > 1.7) Done = true;
    }
}

/// <summary>Longs déplacements : rentre dans sa carapace et glisse en tournoyant.</summary>
sealed class Travel : Behavior
{
    readonly double? forcedX, forcedY;
    double tx, ty, spin, arrivedAt = -1;
    public Travel() { }
    public Travel(double x, double y) { forcedX = x; forcedY = y; }
    public override int Fps => 30;
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override string Label => "voyage en carapace";
    public override void OnStartle(Pet p) { }
    public override void Start(Pet p)
    {
        (tx, ty) = forcedX is double fx && forcedY is double fy ? p.ClampPoint(fx, fy) : p.RandomPointNear(120, 380);
    }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        const double retract = 0.35;
        if (T < retract)
        {
            v.HeadOut = (float)(1 - T / retract);
            v.LegsTuck = (int)(T / retract * 3);
            v.Eyes = Eyes.Determined;
            return;
        }
        if (arrivedAt < 0)
        {
            v.InShell = true;
            double dx = tx - p.X, dy = ty - p.Y, d = Math.Sqrt(dx * dx + dy * dy);
            double speed = Math.Min(150, d / p.Scale * 2.5 + 25);   // pixels logiques/s, ralentit en approchant
            double step = speed * p.Scale * dt;
            if (d <= step || T > 12) { p.X = tx; p.Y = ty; arrivedAt = T; }
            else { p.X += dx / d * step; p.Y += dy / d * step; }
            if (Math.Abs(dx) > 2) v.FacingRight = dx > 0;
            spin += dt * (speed * 0.12 + 2);
            v.SpinFrame = (int)spin & 3;
            if (speed > 60) v.Add(FxKind.Speed, (float)(T * 3));
            if (T < retract + 0.4) v.Add(FxKind.Dust, (float)((T - retract) / 0.4));
            return;
        }
        double t = T - arrivedAt;
        if (t < 0.3) v.InShell = true;
        else if (t < 0.7)
        {
            double k = (t - 0.3) / 0.4;
            v.HeadOut = (float)k;
            v.LegsTuck = 3 - (int)(k * 3);
            v.Eyes = Eyes.Wide;
        }
        else if (t < 1.3) { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; }
        else Done = true;
    }
}

// ======================================================================== sommeil

sealed class Sleep : Behavior
{
    readonly bool quick;
    double nextDream, dreamUntil = -1, nextMumble, mumbleUntil = -1, pettedUntil = -1, stirUntil = -1, minDur;
    Snack dream;
    public Sleep(bool quick = false) => this.quick = quick;
    public override bool Asleep => true;
    public override bool Interruptible => false;
    public override bool TrackEyes => false;
    public override string Label => T < dreamUntil ? "rêve" : "dort";
    public override int Fps => T < 1.3 ? 15 : T < dreamUntil || T < pettedUntil || T < stirUntil ? 8 : 5;

    public override void Start(Pet p)
    {
        nextDream = Rnd(p, 20, 40);
        nextMumble = Rnd(p, 15, 35);
        minDur = p.M.Energy > 0.8 ? Rnd(p, 120, 300) : 60;   // même une sieste sans fatigue dure un peu
    }

    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        double settle = quick ? 0.4 : 1.2;
        float k = (float)Math.Min(1, T / settle);
        v.HeadOut = 1 - 0.4f * k;
        v.LegsTuck = (int)(3 * k);
        v.Eyes = k < 1 ? Eyes.HalfLid : Eyes.Closed;
        v.Mouth = Mouth.Smile;
        if (T < settle) return;

        if (T > nextDream)
        {
            dreamUntil = T + 6;
            p.Note("dreams");
            nextDream = T + Rnd(p, 25, 50);
            var m = p.M;
            dream = m.Hunger > 0.4 ? (p.R.NextDouble() < 0.6 ? Snack.Strawberry : Snack.Lettuce)
                  : m.Affection > 0.6 && p.R.NextDouble() < 0.5 ? Snack.Heart
                  : p.R.NextDouble() < 0.5 ? Snack.Strawberry : Snack.Lettuce;
        }
        if (T > nextMumble) { mumbleUntil = T + 2; nextMumble = T + Rnd(p, 20, 40); }
        if (T < mumbleUntil) v.Mouth = Every(T, 0.5) ? Mouth.ChewA : Mouth.ChewB;   // mâchouille en dormant

        if (T < stirUntil) { v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Frown; }
        else if (T < pettedUntil) { v.Mouth = Mouth.Grin; v.Blush = true; v.Add(FxKind.Hearts, (float)(T * 0.5), 1); }
        else if (T < dreamUntil) { v.DreamOf = dream; v.Add(FxKind.Dream); }
        else
        {
            v.Add(FxKind.Zzz, (float)(T / 2.4));
            v.Add(FxKind.Bubble, 0, (float)(0.5 + 0.5 * Math.Sin(T * 2 * Math.PI / 3.2)));
        }

        bool night = DateTime.Now.Hour is < 7 or >= 23;
        bool keepSleeping = night && p.S.IdleSeconds > 300;   // la nuit, sans personne, il fait sa nuit
        if (p.M.Energy > 0.97 && T > minDur && !keepSleeping)
        {
            Done = true;
            p.Next = new WakeUp();
            int min = (int)(T / 60);
            p.Note("naps", min >= 10 ? $"A fait une grosse sieste ({min} min)." : null);
            if (dream == Snack.Strawberry && p.R.NextDouble() < 0.3) p.Life?.Write("A rêvé de fraises.");
        }
    }

    public override void OnPetting(Pet p) => pettedUntil = T + 1.2;   // sourit dans son sommeil
    public override void OnPoke(Pet p, int count)
    {
        p.M.Happiness -= 0.06;
        p.Switch(new WakeGrumpy());
    }
    public override void OnStartle(Pet p)
    {
        if (p.R.NextDouble() < 0.4) { p.M.Happiness -= 0.03; p.Switch(new WakeGrumpy()); }
        else stirUntil = T + 1.5;
    }
}

sealed class WakeUp : Behavior
{
    public override bool TrackEyes => false;
    public override string Label => "se réveille";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (T < 1)
        {
            v.HeadOut = (float)(0.6 + 0.4 * T);
            v.LegsTuck = 3 - (int)(3 * T);
            v.Eyes = Eyes.HalfLid;
        }
        else if (T < 2.6) { v.Eyes = Eyes.Closed; v.Mouth = Mouth.Yawn; v.HeadDy = -2; }
        else if (T < 3.2) { v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.ChewA; }
        else if (T > 3.8) Done = true;
    }
}

sealed class WakeGrumpy : Behavior
{
    public override int Fps => 10;
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override string Label => "est grognon";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.LegsTuck = 3; v.HeadOut = 0.8f;
        v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Frown;
        p.FlushTarget = 0.3f;
        if (T < 1.5) v.Add(FxKind.Anger, (float)(T * 2));
        if (T > 1) v.Add(FxKind.Grumble, (float)(T * 2));
        if (T > 3.2)
        {
            Done = true;
            p.Next = p.R.NextDouble() < 0.55 ? new Sleep(quick: true) : new Sulk(isShort: true);   // paresseux : se rendort souvent
        }
    }
}

// ======================================================================== souris (rares)

sealed class CircleCursor : Behavior
{
    int phase;
    double theta, turned, dir, finaleT;
    public override int Fps => 30;
    public override string Label => "joue avec ta souris";
    public override void OnStartle(Pet p) { }
    public override void Start(Pet p) => dir = p.R.NextDouble() < 0.5 ? 1 : -1;
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (!p.S.CursorOnSameMonitor || p.DistToCursor() > 900 || T > 25) { Done = true; return; }
        double cx = p.S.CX, cy = p.S.CY + 10 * p.Scale, r = 26 * p.Scale;
        v.Mouth = Mouth.Grin;
        if (phase == 0)
        {
            theta = Math.Atan2((p.Y - cy) / 0.7, p.X - cx);
            if (p.WalkTo(cx + Math.Cos(theta) * r, cy + Math.Sin(theta) * r * 0.7, 16, dt)) phase = 1;
        }
        else if (phase == 1)
        {
            theta += dt * 1.3 * dir;
            turned += dt * 1.3;
            p.WalkTo(cx + Math.Cos(theta) * r, cy + Math.Sin(theta) * r * 0.7, 24, dt);
            v.Eyes = Every(T, 3) ? Eyes.Happy : Eyes.Normal;
            v.Add(FxKind.Notes, (float)(T * 0.5), 1);
            if (turned > 4 * Math.PI)
            {
                phase = 2; finaleT = T; p.M.Happiness += 0.06;
                p.Life?.AddBond(3); p.Note("games", "A fait la ronde autour de ta souris.", oncePerDay: true);
            }
        }
        else
        {
            double t = T - finaleT;
            p.FaceCursor();
            Hop(p, t, 0.5, 8);
            v.Eyes = Eyes.Happy;
            v.Add(FxKind.Hearts, (float)t, 2);
            if (t > 1.4) { p.Z = 0; Done = true; }
        }
    }
}

sealed class AttackCursor : Behavior
{
    readonly bool quick;
    int phase, attempts;
    double phaseStart, sx, sy, tx, ty;
    public AttackCursor(bool quick) => this.quick = quick;
    public override int Fps => 30;
    public override bool Interruptible => false;
    public override string Label => "chasse ta souris";
    public override void OnStartle(Pet p) { }
    public override void Start(Pet p) => phase = quick ? 1 : 0;

    void Go(int ph) { phase = ph; phaseStart = T; }

    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        double pt = T - phaseStart;
        if (!p.S.CursorOnSameMonitor && phase < 2) { Done = true; return; }
        switch (phase)
        {
            case 0: // approche
            {
                p.FaceCursor();
                v.Eyes = Eyes.Determined;
                double side = p.X < p.S.CX ? -1 : 1;
                bool arrived = p.WalkTo(p.S.CX + side * 36 * p.Scale, p.S.CY + 14 * p.Scale, 12, dt);
                if (arrived || p.DistToCursor() < 45 * p.Scale) Go(1);
                else if (T > 10) Done = true;
                break;
            }
            case 1: // se tasse et remue le derrière
            {
                double dur = quick ? 0.9 : 1.6;
                p.FaceCursor();
                v.LegsTuck = 1;
                v.Eyes = Eyes.Determined;
                if (pt > dur - 0.8) v.BodyDx = Every(pt, 0.16) ? 1 : -1;
                if (pt > dur - 0.35) v.Add(FxKind.Exclaim, (float)pt);
                if (pt >= dur)
                {
                    v.FacingRight = p.S.CX > p.X;
                    (tx, ty) = p.ClampPoint(p.S.CX - (v.FacingRight ? 11 : -11) * p.Scale, p.S.CY + 14 * p.Scale);
                    sx = p.X; sy = p.Y;
                    Go(2);
                }
                break;
            }
            case 2: // bond
            {
                double u = Math.Min(1, pt / 0.45);
                p.X = sx + (tx - sx) * u;
                p.Y = sy + (ty - sy) * u;
                p.Z = 4 * u * (1 - u) * 14 * p.Scale;
                v.Mouth = Mouth.Bite; v.Eyes = Eyes.Determined; v.LegPhase = 1;
                if (u >= 1)
                {
                    p.Z = 0;
                    double hx = p.X + (v.FacingRight ? 11 : -11) * p.Scale, hy = p.Y - 14 * p.Scale;
                    double dx = p.S.CX - hx, dy = p.S.CY - hy;
                    Go(dx * dx + dy * dy < 13 * 13 * p.Scale * p.Scale ? 3 : 4);
                }
                break;
            }
            case 3: // attrapé ! croque puis triomphe
            {
                if (pt < 0.8) { v.Mouth = Every(pt, 0.2) ? Mouth.Bite : Mouth.ChewA; v.Eyes = Eyes.Squint; }
                else
                {
                    v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
                    v.Add(FxKind.Notes, (float)pt, 2);
                    Hop(p, pt - 0.8, 0.4, 5);
                }
                if (pt > 2)
                {
                    p.Z = 0; p.M.Happiness += 0.1; Done = true;
                    p.Life?.AddBond(3); p.Note("games", "A attrapé ta souris !");
                }
                break;
            }
            case 4: // raté : trépigne
            {
                v.Eyes = Eyes.Angry; v.Mouth = Mouth.Zigzag;
                p.FlushTarget = 0.5f;
                v.Add(FxKind.Steam, (float)(pt * 1.5));
                v.Add(FxKind.Anger, (float)(pt * 2));
                v.LegPhase = Every(pt, 0.3) ? 0 : 2;
                if (pt > 1.3)
                {
                    attempts++;
                    if (attempts < 3 && p.R.NextDouble() < 0.75) Go(0);
                    else { Done = true; p.Next = new Sulk(isShort: true); }
                }
                break;
            }
        }
    }
}

sealed class ChaseCursor : Behavior
{
    double dur, fastTime;
    public override int Fps => 30;
    public override bool Interruptible => false;
    public override string Label => "poursuit ta souris";
    public override void OnStartle(Pet p) { }
    public override void Start(Pet p) => dur = Rnd(p, 8, 12);
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (!p.S.CursorOnSameMonitor) { Done = true; return; }
        v.Eyes = Eyes.Determined; v.Mouth = Mouth.Grin;
        // elle accélère quand la souris s'éloigne (45 à 70 px logiques/s) : avant, 20 ne rattrapait jamais rien
        double speed = 45 + Math.Min(25, p.DistToCursor() / p.Scale / 6);
        p.WalkTo(p.S.CX, p.S.CY + 14 * p.Scale, speed, dt);
        if (p.DistToCursor() < 14 * p.Scale)
        {
            p.M.Happiness += 0.08;
            p.Life?.AddBond(3); p.Note("games", "A rattrapé ta souris à la course.", oncePerDay: true);
            Done = true; p.Next = new Hooray();
            return;
        }
        if (p.S.Speed > 1500) fastTime += dt;
        if (fastTime > 1.2 || T > dur)
        {
            Done = true;
            p.Next = p.R.NextDouble() < 0.5 ? new Sulk(isShort: true) : new Rest();
        }
    }
}

sealed class SeekAttention : Behavior
{
    bool waiting;
    double waitStart;
    public override string Label => "réclame un câlin";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (!p.S.CursorOnSameMonitor || T > 25) { Done = true; return; }
        if (!waiting)
        {
            double side = p.X < p.S.CX ? -1 : 1;
            if (p.WalkTo(p.S.CX + side * 30 * p.Scale, p.S.CY + 22 * p.Scale, 11, dt) || T > 8) { waiting = true; waitStart = T; }
            return;
        }
        double wt = T - waitStart;
        p.FaceCursor();
        v.HeadDy = -1; v.Eyes = Eyes.Wide; v.Mouth = Mouth.Smile;
        if (wt % 2.5 < 0.6) v.Add(p.M.Affection > 0.6 ? FxKind.Hearts : FxKind.Exclaim, (float)wt, 1);
        if (p.DistToCursor() > 260 * p.Scale / 3) waiting = false;
        if (wt > 12)
        {
            p.M.Happiness -= 0.03;
            Done = true; p.Next = new Sulk(isShort: true);
        }
    }
}

// ======================================================================== réactions

sealed class Sulk : Behavior
{
    readonly bool isShort;
    double dur, resistUntil = -1;
    public Sulk(bool isShort = false) => this.isShort = isShort;
    public override int Fps => 6;
    public override bool TrackEyes => false;
    public override string Label => "boude";
    public override void Start(Pet p) => dur = isShort ? Rnd(p, 3, 5) : Rnd(p, 7, 12);
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.FacingRight = p.S.CX < p.X;        // tourne le dos au curseur
        v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Pout; v.LegsTuck = 2;
        v.Add(FxKind.Grumble, (float)(T * 0.8));
        if (T < resistUntil) { v.Add(FxKind.Anger, (float)(T * 2)); p.FlushTarget = 0.25f; }
        if (T > dur) Done = true;
    }
    public override void OnPetting(Pet p)
    {
        // résiste un peu… puis craque
        if (p.PettingContinuous > 1.8) { p.M.Happiness += 0.05; p.Switch(new EnjoyPet(giggle: false)); }
        else resistUntil = T + 0.3;
    }
}

sealed class Grabbed : Behavior
{
    public override int Fps => 60;
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override string Label => "est dans tes mains";
    public override void OnPetting(Pet p) { }
    public override void OnPoke(Pet p, int count) { }
    public override void OnStartle(Pet p) { }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.LegsDangle = true;
        v.LegPhase = (int)(T * 9) & 3;
        if (T < 2 || p.M.Affection < 0.6) { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Sweat, (float)(T * 1.5)); }
        else { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Add(FxKind.Notes, (float)(T * 0.8), 1); }
    }
}

sealed class Thrown : Behavior
{
    readonly double strength;
    readonly bool gentle;
    double spin, dustUntil = -1, starsUntil = -1;
    int bonks;
    public Thrown(double strength) { this.strength = strength; gentle = strength < 150; }
    public override int Fps => 40;
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override string Label => "vole !";
    public override void OnPetting(Pet p) { }
    public override void OnPoke(Pet p, int count) { }
    public override void OnStartle(Pet p) { }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        int b = p.PhysicsStep(dt, out bool hard);
        bonks += b;
        if (b > 0 || hard) dustUntil = T + 0.35;
        if (b > 0) starsUntil = T + 0.6;
        double sp = Math.Sqrt(p.VX * p.VX + p.VY * p.VY);

        if (gentle)
        {
            v.LegsDangle = p.Z > 0;
            v.LegPhase = p.Z > 0 ? (int)(T * 9) & 3 : 0;
            v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh;
        }
        else
        {
            v.InShell = true;
            spin += dt * (sp / p.Scale * 0.25 + 4);
            v.SpinFrame = (int)spin & 3;
            if (Math.Abs(p.VX) > 30) v.FacingRight = p.VX > 0;
            if (sp > 300) v.Add(FxKind.Speed, (float)(T * 3));
        }
        if (T < dustUntil) v.Add(FxKind.Dust, (float)(1 - (dustUntil - T) / 0.35));
        if (T < starsUntil) v.Add(FxKind.Stars, (float)(T * 1.5));

        if (T > 0.2 && p.Settled)
        {
            p.Z = 0; p.VX = p.VY = p.VZ = 0;
            Done = true;
            if (p.Bowl is { Throwing: true }) p.Next = new BowlReact();      // un lancer de bowling : ni vol plané ni rancune
            else if (gentle) p.Next = new Surprised(annoyed: false);
            else if (strength > 900 || bonks >= 2)
            {
                p.M.Happiness -= 0.1; p.M.Affection -= 0.03;
                p.Life?.AddBond(-2);
                p.Note("throws", "A traversé l'écran en vol plané. Pas fan.", oncePerDay: true);
                p.Next = new Dizzy();
            }
            else if (p.M.Happiness > 0.6 && p.R.NextDouble() < 0.4) { p.M.Happiness += 0.03; p.Next = new Hooray(); } // il a aimé !
            else { p.M.Happiness -= 0.04; p.Next = new Surprised(annoyed: true); }
        }
    }
}

sealed class Dizzy : Behavior
{
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override string Label => "est sonné";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.Eyes = Eyes.Spiral; v.Mouth = Mouth.Zigzag;
        v.Add(FxKind.Stars, (float)(T * 0.9));
        v.HeadDy = (int)(T * 3) % 2;
        v.BodyDx = (int)(T * 2) % 2;
        if (T > 3.5)
        {
            Done = true;
            p.Next = p.M.Happiness < 0.35 ? new Angry(biteCursor: false) : new Wiggle();
        }
    }
}

sealed class Surprised : Behavior
{
    readonly bool annoyed;
    public Surprised(bool annoyed) => this.annoyed = annoyed;
    public override int Fps => 24;
    public override string Label => annoyed ? "n'est pas content" : "sursaute";
    public override void OnStartle(Pet p) { }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        Hop(p, T, 0.35, annoyed ? 2 : 5);
        if (!annoyed) { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Exclaim, (float)T); }
        else
        {
            p.FaceCursor();
            v.Eyes = Eyes.Angry; v.Mouth = Mouth.Frown;
            v.Add(FxKind.Anger, (float)(T * 2));
            p.FlushTarget = 0.25f;
        }
        if (T > 1.2) { p.Z = 0; Done = true; }
    }
}

sealed class HideInShell : Behavior
{
    double dur, peekAt;
    public override int Fps => 12;
    public override bool TrackEyes => false;
    public override string Label => "se cache";
    public override void OnStartle(Pet p) { if (T > peekAt) T = 0.3; }
    public override void Start(Pet p) { dur = Rnd(p, 3.5, 6); peekAt = dur - 1.8; }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (T < 0.25) { v.HeadOut = (float)(1 - T / 0.25); v.LegsTuck = (int)(T / 0.25 * 3); v.Eyes = Eyes.Wide; }
        else if (T < peekAt) { v.InShell = true; if (T < 1.3) v.Add(FxKind.Sweat, (float)T); }
        else if (T < dur - 0.5) { v.HeadOut = 0.35f; v.LegsTuck = 3; v.Eyes = Eyes.Wide; v.LookX = Every(T, 1.2) ? 1 : -1; }
        else if (T < dur) { double k = (T - (dur - 0.5)) / 0.5; v.HeadOut = (float)(0.35 + 0.65 * k); v.LegsTuck = 3 - (int)(3 * k); }
        else Done = true;
    }
}

sealed class Angry : Behavior
{
    readonly bool biteCursor;
    public Angry(bool biteCursor) => this.biteCursor = biteCursor;
    public override int Fps => 20;
    public override bool Interruptible => false;
    public override string Label => "est en colère";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        p.FaceCursor();
        p.FlushTarget = 0.75f;
        v.Eyes = Eyes.Angry; v.Mouth = Mouth.Zigzag;
        v.Add(FxKind.Anger, (float)(T * 2));
        v.Add(FxKind.Steam, (float)(T * 1.4));
        v.LegPhase = Every(T, 0.28) ? 0 : 2;       // trépigne
        if (T > 3)
        {
            Done = true;
            if (biteCursor && p.S.CursorOnSameMonitor && p.DistToCursor() < 400) p.Next = new AttackCursor(quick: true);
        }
    }
}

sealed class EnjoyPet : Behavior
{
    readonly bool giggle;
    double lastPet;
    public EnjoyPet(bool giggle) => this.giggle = giggle;
    public override int Fps => 12;
    public override bool TrackEyes => false;
    public override string Label => "se fait câliner";
    public override void OnPetting(Pet p) { }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (p.BeingPetted) lastPet = T;
        double tolerance = 6 + 30 * p.M.Affection;
        if (p.PettingContinuous > tolerance)
        {
            // ça suffit, les câlins
            Done = true; p.Next = new Sulk(isShort: true);
            return;
        }
        p.FaceCursor();
        v.HeadDy = 1; v.Blush = true; v.Mouth = Mouth.Grin;
        v.Eyes = p.M.Affection > 0.75 && T > 2 ? Eyes.Hearts : Eyes.Happy;
        v.Add(FxKind.Hearts, (float)(T * 0.6), p.M.Affection > 0.5 ? 2 : 1);
        if (giggle) Hop(p, T, 0.3, 3);
        if (T - lastPet > 1.2) { p.Z = 0; Done = true; }
    }
}

sealed class Greet : Behavior
{
    public override int Fps => 20;
    public override string Label => "te dit bonjour";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        p.FaceCursor();
        if (T < 0.8) { v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Exclaim, (float)T); }
        else
        {
            v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
            v.Add(p.M.Affection > 0.5 ? FxKind.Hearts : FxKind.Notes, (float)T, 2);
        }
        Hop(p, T - 0.2, 0.3, 5);
        if (T > 2.6) { p.Z = 0; Done = true; }
    }
}

sealed class Hooray : Behavior
{
    public override int Fps => 24;
    public override string Label => "est tout content";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        p.FaceCursor();
        v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
        v.Add(FxKind.Notes, (float)(T * 0.8), 2);
        Hop(p, T % 0.45, 0.45, T < 1 ? 5 : 0);
        if (T > 1.6) { p.Z = 0; Done = true; }
    }
}
