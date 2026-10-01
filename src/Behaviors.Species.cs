namespace Pepin;

// Comportements propres à une espèce (v3). Les poids sont dans Pet.PickNext, les réflexes dans Pet.SpeciesReflexes.

#region hérisson

/// <summary>Hérisson : museau au sol, avance par à-coups en reniflant ; parfois il déniche quelque chose.</summary>
sealed class SniffGround : Behavior
{
    double tx, ty, nextStop, stopUntil = -1, dur;
    bool found, lucky;
    public override int Fps => 12;
    public override bool TrackEyes => false;
    public override string Label => "renifle le sol";
    public override void Start(Pet p)
    {
        (tx, ty) = p.RandomPointNear(20, 60);
        dur = Rnd(p, 6, 10);
        nextStop = Rnd(p, 0.8, 1.6);
        lucky = p.R.NextDouble() < 0.05;              // environ une trouvaille par heure
    }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.HeadDy = 2; v.HeadDx = 1;
        v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.ChewA;
        if (T < stopUntil)
        {
            // la truffe frétille
            v.HeadDx = Every(T, 0.16) ? 1 : 2;
            v.Mouth = Every(T, 0.16) ? Mouth.ChewA : Mouth.ChewB;
            v.Add(FxKind.Dust, (float)((T % 0.6) / 0.6));
        }
        else if (T > nextStop)
        {
            stopUntil = T + Rnd(p, 0.6, 1.2);
            nextStop = stopUntil + Rnd(p, 0.6, 1.6);
        }
        else p.WalkTo(tx, ty, 4, dt);

        if (lucky && !found && T > dur - 1.5 && p.Life is not null)
        {
            found = true;
            var it = Items.HerbObject(p.R);
            p.Life.AddItem(it);
            p.Note("finds", $"A déniché {Items.Label(it)} en reniflant.");
            p.Say($"Oh, {Items.Label(it)} !", 3);
        }
        if (T > dur) Done = true;
    }
}

#endregion

#region grenouille

/// <summary>Grenouille : une mouche tourne autour, elle la suit des yeux… et la gobe d'un coup de langue.</summary>
sealed class CatchFly : Behavior
{
    double strikeAt;
    public override int Fps => 24;
    public override bool TrackEyes => false;
    public override string Label => "chasse une mouche";
    public override void Start(Pet p) => strikeAt = Rnd(p, 2.2, 3.6);
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (T < strikeAt)
        {
            float ph = (float)(T * 0.7);
            v.Add(FxKind.Fly, ph);
            v.Eyes = Eyes.Normal; v.Mouth = Mouth.Smile;
            v.LookX = MathF.Cos(ph * 6.283f) > 0 ? 1 : -1;
            v.LookY = -1;
            v.LegsTuck = 1;
        }
        else if (T < strikeAt + 0.35)
        {
            float k = (float)((T - strikeAt) / 0.35);
            float len = k < 0.5f ? k * 2 : 2 - k * 2;               // la langue part… et revient
            v.Add(FxKind.Tongue, (float)(strikeAt * 0.7), len);
            if (k < 0.5f) v.Add(FxKind.Fly, (float)(strikeAt * 0.7));
            v.Eyes = Eyes.Determined; v.Mouth = Mouth.Tongue;
        }
        else if (T < strikeAt + 2.2)
        {
            v.Eyes = Eyes.Happy;
            v.Mouth = Every(T, 0.25) ? Mouth.ChewA : Mouth.ChewB;
            v.Puffed = Every(T, 0.5);
        }
        else
        {
            p.M.Hunger -= 0.05;
            p.M.Happiness += 0.02;
            p.Note("flies", "A gobé une mouche.", oncePerDay: true);
            Done = true;
        }
    }
}

/// <summary>Grenouille : la gorge gonfle et dégonfle, petites notes « croâ ».</summary>
sealed class Croak : Behavior
{
    public override int Fps => 12;
    public override bool TrackEyes => false;
    public override string Label => "coasse";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        v.Eyes = Eyes.Happy;
        double c = T % 1.1;
        v.Puffed = c < 0.55;
        v.Mouth = c < 0.55 ? Mouth.ChewA : Mouth.Smile;
        if (c < 0.55) v.Add(FxKind.Notes, (float)(T * 0.5), 1);
        if (T > 3.4) Done = true;
    }
}

#endregion
