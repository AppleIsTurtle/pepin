namespace Pepin;

// Comportements débloqués par le lien avec son humain (voir Life).

/// <summary>Nouveau palier : fête avec étincelles et cœurs.</summary>
sealed class LevelUp : Behavior
{
    readonly int tier;
    public LevelUp(int tier) => this.tier = tier;
    public override int Fps => 24;
    public override string Label => "est tout ému";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        p.FaceCursor();
        v.Eyes = T > 1.2 && tier >= 3 ? Eyes.Hearts : Eyes.Happy;
        v.Mouth = Mouth.Grin; v.Blush = true;
        v.Add(FxKind.Sparkles, (float)(T * 0.8));
        v.Add(FxKind.Hearts, (float)(T * 0.6), Math.Min(3, tier));
        Hop(p, T % 0.5, 0.5, T < 1.5 ? 6 : 0);
        if (T > 3.2) { p.Z = 0; Done = true; }
    }
}

/// <summary>Palier 1+ : te suit un moment, sans coller.</summary>
sealed class FollowYou : Behavior
{
    double dur, lastActive;
    public override int Fps => 15;
    public override string Label => "te suit";
    public override void Start(Pet p) => dur = Rnd(p, 20, 45);
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (p.S.IdleSeconds < 2) lastActive = T;
        if (!p.S.CursorOnSameMonitor || T > dur || T - lastActive > 10) { Done = true; return; }
        double tx = p.S.CX + (p.X < p.S.CX ? -1 : 1) * 34 * p.Scale, ty = p.S.CY + 20 * p.Scale;
        double dx = tx - p.X, dy = ty - p.Y;
        if (dx * dx + dy * dy > 30 * 30 * p.Scale * p.Scale) p.WalkTo(tx, ty, 12, dt);
        else
        {
            p.FaceCursor();
            p.Breathe();
            v.Eyes = Every(T, 4, 0.3) ? Eyes.Happy : Eyes.Normal;
        }
    }
}

/// <summary>Palier 2+ : tu ne bouges plus, il vient faire la sieste contre ta souris.</summary>
sealed class NapByCursor : Behavior
{
    double cx, cy;
    public override string Label => "vient se blottir";
    public override void Start(Pet p) { cx = p.S.CX; cy = p.S.CY; }
    public override void Tick(Pet p, double dt)
    {
        if (Math.Abs(p.S.CX - cx) + Math.Abs(p.S.CY - cy) > 40 || T > 20) { Done = true; return; }
        // la tête vient toucher la pointe du curseur
        bool fromLeft = p.X < cx;
        double tx = cx + (fromLeft ? -9 : 9) * p.Scale, ty = cy + 13 * p.Scale;
        if (p.WalkTo(tx, ty, 9, dt))
        {
            p.V.FacingRight = fromLeft;
            Done = true;
            p.Next = new Sleep(quick: true);
            p.Life?.Write("A fait la sieste contre ta souris.");
        }
    }
}

/// <summary>Palier 2+ : déniche un petit objet et vient te l'offrir.</summary>
sealed class BringGift : Behavior
{
    Item item;
    int phase;
    double phaseStart;
    public override string Label => phase < 2 ? "cherche quelque chose" : "veut t'offrir " + Items.Label(item);
    public override int Fps => phase == 0 ? 20 : 15;
    public override void Start(Pet p) => item = Items.Random(p.R);
    void Go(int ph) { phase = ph; phaseStart = T; }

    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        double pt = T - phaseStart;
        v.Food = item;
        switch (phase)
        {
            case 0: // fouille
                v.Eyes = Eyes.Squint; v.Mouth = Mouth.Grin;
                v.BodyDx = Every(pt, 0.2) ? 1 : -1;
                v.HeadDy = 1;
                v.Add(FxKind.Dust, (float)(pt % 1));
                if (pt > 1.6) Go(1);
                break;
            case 1: // trouvé !
                v.Eyes = Eyes.Wide; v.Mouth = Mouth.ChewA;
                v.Add(FxKind.Food, 0, 1);
                v.Add(FxKind.Exclaim, (float)pt);
                if (pt > 0.9) Go(2);
                break;
            case 2: // l'apporte
            {
                v.Mouth = Mouth.ChewA;
                v.Add(FxKind.Food, 0, 1);
                double side = p.X < p.S.CX ? -1 : 1;
                if (p.WalkTo(p.S.CX + side * 30 * p.Scale, p.S.CY + 22 * p.Scale, 11, dt) || pt > 10) Go(3);
                break;
            }
            case 3: // attend que tu le prennes (clic ou caresse)
                p.FaceCursor();
                v.HeadDy = -1; v.Eyes = Eyes.Wide; v.Mouth = Mouth.ChewA; v.LookY = -1;
                v.Add(FxKind.Food, 0, 1);
                if (pt % 2.5 < 0.7) v.Add(FxKind.Hearts, (float)pt, 1);
                if (pt > 20) Go(5);
                break;
            case 4: // accepté
                v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true;
                v.Add(FxKind.Hearts, (float)(pt * 0.7), 3);
                Hop(p, pt % 0.5, 0.5, pt < 1 ? 5 : 0);
                if (pt > 2) { p.Z = 0; Done = true; }
                break;
            case 5: // ignoré : il le garde
                v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Pout;
                v.Add(FxKind.Food, 0, 1);
                if (pt < 1.5) v.Add(FxKind.Tear, (float)pt);
                if (pt > 2.5)
                {
                    Done = true;
                    p.Life?.AddItem(item);
                    p.Life?.Write($"Voulait t'offrir {Items.Label(item)}, c'est finalement allé dans sa collection.");
                }
                break;
        }
    }

    void Accept(Pet p)
    {
        if (phase != 3) return;
        Go(4);
        p.M.Happiness += 0.1;
        p.Life?.AddBond(4);
        p.Note("gifts", $"T'a offert {Items.Label(item)}.");
    }

    public override void OnPetting(Pet p) => Accept(p);
    public override void OnPoke(Pet p, int count) { if (phase == 3) Accept(p); else base.OnPoke(p, count); }
}

/// <summary>Palier 4 : tu t'en vas, il est triste.</summary>
sealed class SadGoodbye : Behavior
{
    public override string Label => "te regarde partir";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        p.FaceCursor();
        v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Frown;
        v.LegPhase = T < 2 && Every(T, 0.5) ? 3 : 0;   // petit signe de la patte
        v.Add(FxKind.Tear, (float)(T * 0.7));
        if (T > 3.5) { Done = true; p.Next = new Rest(); }
    }
}

/// <summary>Palier 3+ : ton retour, c'est la fête.</summary>
sealed class JoyDance : Behavior
{
    public override int Fps => 24;
    public override string Label => "danse de joie";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.FacingRight = Every(T, 0.6) ? p.S.CX > p.X : p.S.CX <= p.X;
        v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true;
        v.Add(FxKind.Notes, (float)(T * 0.8), 2);
        v.Add(FxKind.Hearts, (float)(T * 0.5), 1);
        Hop(p, T % 0.4, 0.4, 5);
        if (T > 3) { p.Z = 0; Done = true; }
    }
}
