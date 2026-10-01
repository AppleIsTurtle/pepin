namespace Pepin;

// Mini-jeux : la tortue propose, tu joues si tu veux. Voir Bowling.

/// <summary>Va se placer, puis installe les quilles (elles tombent du ciel une à une).</summary>
sealed class BowlSetup : Behavior
{
    bool placed;
    double placedAt;
    public override int Fps => 15;
    public override string Label => "installe des quilles";

    public override void Start(Pet p)
    {
        if (p.Bowl is null || !p.Bowl.TryStart(p)) Done = true;
    }

    public override void Tick(Pet p, double dt)
    {
        var b = p.Bowl;
        if (b is null || !b.Active) { Done = true; return; }
        var v = p.V;
        if (!placed)
        {
            var (sx, sy) = b.Start;
            if (p.WalkTo(sx, sy, 12, dt) || T > 14) { placed = true; placedAt = T; b.Begin(); }
            return;
        }
        v.FacingRight = b.Dir > 0;
        v.Eyes = Eyes.Squint; v.Mouth = Mouth.Smile;
        v.HeadDy = 1;
        if (T - placedAt > 1.4 && b.Phase == BowlPhase.Wait) { Done = true; p.Next = new BowlWait(); }
    }
}

/// <summary>Attend d'être lancée. Revient au point de lancer si on l'a déplacée. Abandonne si personne ne joue.</summary>
sealed class BowlWait : Behavior
{
    double nextHint = 1.5;
    public override int Fps => 10;
    public override string Label => "joue au bowling";

    public override void Tick(Pet p, double dt)
    {
        var b = p.Bowl;
        if (b is null || !b.Active)
        {
            Done = true;
            if (b is { Abandoned: true })
            {
                p.Say("Bon... une autre fois.", 3);
                if (b.Throws > 0) p.Note("bowling", $"Partie de bowling interrompue : {b.Fell} quille{(b.Fell > 1 ? "s" : "")} sur {Bowling.PinCount}.");
            }
            return;
        }
        if (b.Phase is BowlPhase.Prep or BowlPhase.Setup) { p.Breathe(); return; }

        var (sx, sy) = b.Start;
        double dx = sx - p.X, dy = sy - p.Y;
        if (dx * dx + dy * dy > 6 * 6 * p.Scale * p.Scale) { p.WalkTo(sx, sy, 11, dt); return; }

        p.V.FacingRight = b.Dir > 0;
        p.Breathe();
        if (T > nextHint)
        {
            p.Say(b.Throws == 0 ? "Lance-moi !" : "Encore une fois !", 3.5);
            nextHint = T + 25;
        }
    }
}

/// <summary>Après un lancer : attend que les quilles s'arrêtent, compte, réagit. Fin de tour = bilan et rangement.</summary>
sealed class BowlReact : Behavior
{
    ThrowResult res;
    bool decided;
    double decideAt, dur;
    public override int Fps => 24;
    public override bool Interruptible => false;
    public override string Label => "regarde les quilles";

    public override void Tick(Pet p, double dt)
    {
        var b = p.Bowl;
        var v = p.V;
        if (!decided)
        {
            if (b is null || !b.Throwing) { Done = true; return; }
            v.FacingRight = b.Dir > 0;
            v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh;
            if (b.Settled || T > 6)
            {
                res = b.EndThrow();
                decided = true;
                decideAt = T;
                dur = res == ThrowResult.Strike ? 3 : res == ThrowResult.Spare ? 2.4 : res == ThrowResult.Some ? 1.6 : 1.8;
                p.Say(res switch
                {
                    ThrowResult.Strike => "STRIKE !",
                    ThrowResult.Spare => "Toutes tombées !",
                    ThrowResult.Some => $"{b.Fell} sur {Bowling.PinCount}",
                    _ => "Raté...",
                }, 2.5);
            }
            return;
        }

        double rt = T - decideAt;
        p.FaceCursor();
        switch (res)
        {
            case ThrowResult.Strike:
                v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true;
                v.Add(FxKind.Sparkles, (float)(rt * 0.8));
                v.Add(FxKind.Notes, (float)(rt * 0.7), 2);
                Hop(p, rt % 0.4, 0.4, rt < 2 ? 6 : 0);
                break;
            case ThrowResult.Spare:
                v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
                v.Add(FxKind.Notes, (float)(rt * 0.8), 2);
                Hop(p, rt % 0.45, 0.45, rt < 1.2 ? 5 : 0);
                break;
            case ThrowResult.Some:
                v.Eyes = Eyes.Happy; v.Mouth = Mouth.Smile;
                Hop(p, rt, 0.4, 3);
                break;
            default:
                v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Frown;
                v.Add(FxKind.Sweat, (float)rt);
                break;
        }

        if (rt > dur)
        {
            p.Z = 0;
            Done = true;
            if (b is not null && b.RoundOver) Finish(p, b);
        }
    }

    /// <summary>Bilan du tour : lien, humeur, carnet. Les quilles s'en vont.</summary>
    void Finish(Pet p, Bowling b)
    {
        int fell = b.Fell;
        bool strike = res == ThrowResult.Strike;
        string text = strike ? "Strike au bowling !"
                    : fell >= Bowling.PinCount ? "A renversé toutes les quilles en deux coups."
                    : $"Partie de bowling : {fell} quille{(fell > 1 ? "s" : "")} sur {Bowling.PinCount}.";
        p.Note("bowling", text);
        if (strike) p.Note("strikes");
        p.Life?.AddBond(0.5 + fell * 0.4 + (strike ? 2 : 0));
        p.M.Happiness += 0.03 + fell * 0.01;
        b.Stop();
    }
}

// ======================================================================== évènements : l'herbe

/// <summary>Une touffe d'herbe vient de pousser : elle l'a vue, va voir de plus près et attend (un peu) que tu t'y intéresses.</summary>
sealed class NoticeTuft : Behavior
{
    public override int Fps => 15;
    public override string Label => "a vu de l'herbe";

    public override void Tick(Pet p, double dt)
    {
        var g = p.Grass;
        var v = p.V;
        if (g is null || g.Phase is GrassPhase.Off or GrassPhase.Leave || T > 45) { Done = true; return; }
        if (g.Phase == GrassPhase.Result) { Done = true; return; }       // l'App prend la suite (réaction au résultat)
        var (gx, gy) = g.Spot;
        if (T < 1.1)
        {
            p.FaceX(gx);
            v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh;
            v.Add(FxKind.Exclaim, (float)T);
            return;
        }
        double side = p.X < gx ? -1 : 1;
        double tx = gx + side * 16 * p.Scale;
        if (Math.Abs(p.X - tx) > 3 * p.Scale || Math.Abs(p.Y - gy) > 4 * p.Scale)
        {
            p.WalkTo(tx, gy, 10, dt);
            return;
        }
        p.FaceX(gx);
        p.Breathe();
        v.Eyes = Every(T, 6, 0.7) ? Eyes.Normal : Eyes.Wide;
        if (Every(T, 12, 0.15)) v.Add(FxKind.Question);
    }
}

/// <summary>Une petite bête est sortie de l'herbe : elle la regarde passer, intriguée.</summary>
sealed class WatchCritter : Behavior
{
    public override int Fps => 15;
    public override string Label => "observe une petite bête";

    public override void Tick(Pet p, double dt)
    {
        var g = p.Grass;
        var v = p.V;
        if (g is null || !g.CritterActive) { Done = true; return; }
        var (cx, cy) = g.CritterPos;
        p.FaceX(cx);
        p.Breathe();
        bool close = Math.Abs(cx - p.X) < 24 * p.Scale;
        v.Eyes = Eyes.Wide;
        v.Mouth = close ? Mouth.Oh : Mouth.Smile;
        v.LookY = cy < p.Y - 20 * p.Scale ? -1 : 0;
        if (T < 1.2) v.Add(FxKind.Exclaim, (float)T);
        else if (Every(T, 5, 0.25)) v.Add(FxKind.Hearts, (float)(T % 5), 1);
    }
}
