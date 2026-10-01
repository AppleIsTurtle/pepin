namespace Pepin;

public enum GrassPhase : byte { Off, Grow, Wait, Result, Leave }
public enum GrassOutcome : byte { Nothing, Object, Critter }

/// <summary>
/// Évènement aléatoire : une touffe d'herbe pousse au bas de l'écran. Un clic dessus tente sa chance : rien,
/// un objet (qui rejoint la collection) ou une petite bête qui sort, se promène et s'en va. Sans clic elle repart
/// toute seule. Fenêtre minuscule : seuls les pixels de la touffe reçoivent le clic.
/// </summary>
public sealed class GrassEvent
{
    const int CW = 96, CH = 34;                 // toile en pixels logiques (large : la bête a de la place pour se promener)
    const int BaseY = 30, CenterX = 48;         // sol et milieu de la touffe dans la toile
    const double GrowTime = 0.7, WaitMax = 90, LeaveTime = 0.5;

    readonly Overlay ov = new(clickable: true);
    readonly PixelCanvas canvas = new(CW, CH);
    readonly Random r = Random.Shared;

    double s = 3, winLeft, winTop, t, nextSway;
    nint home;

    // résultat
    GrassOutcome outcome;
    Item item;
    double critX, critY, critDir, critZ, critT;
    bool critMoved;
    readonly double[] leafX = new double[5], leafY = new double[5], leafVX = new double[5], leafVY = new double[5];

    public GrassPhase Phase { get; private set; }
    public bool Active => Phase != GrassPhase.Off;
    public Action<GrassOutcome, Item?>? Resolved;           // appelé sur le thread UI, au moment du clic

    /// <summary>Une bête est sortie de l'herbe et se promène encore.</summary>
    public bool CritterActive => Phase == GrassPhase.Result && outcome == GrassOutcome.Critter;
    public (double X, double Y) Spot => (winLeft + CenterX * s, winTop + BaseY * s);
    public (double X, double Y) CritterPos => (winLeft + critX * s, winTop + (critY - critZ) * s);

    public int Fps => Phase switch
    {
        GrassPhase.Off => 0,
        GrassPhase.Wait => t < nextSway + 0.6 && t >= nextSway ? 20 : 6,
        _ => 30,
    };

    internal PixelCanvas Canvas => canvas;                 // outils de dev (--preview)
    internal void DebugForce(GrassOutcome o, Item i)
    {
        outcome = o; item = i;
        Phase = GrassPhase.Result; t = 0; critT = 0; critX = CenterX; critY = BaseY; critZ = 0; critDir = 1; critMoved = false;
        for (int k = 0; k < leafX.Length; k++) { leafX[k] = CenterX + k - 2; leafY[k] = BaseY - 4; leafVX[k] = (k - 2) * 6; leafVY[k] = -14; }
    }

    public void Create(nint inst)
    {
        ov.Create(inst);
        ov.Click = OnClick;
    }

    /// <summary>Fait pousser une touffe loin de la tortue. Faux s'il n'y a pas la place.</summary>
    public bool TrySpawn(Pet p)
    {
        if (Active || ov.Visible) return false;
        s = p.Scale;
        var w = p.S.Work;
        double margin = (CW / 2 + 2) * s;
        if (w.Right - w.Left < 2 * margin + 60 * s) return false;
        double x = 0;
        for (int i = 0; i < 8; i++)
        {
            x = w.Left + margin + r.NextDouble() * (w.Right - w.Left - 2 * margin);
            if (Math.Abs(x - p.X) > 50 * s) break;
        }
        double ground = w.Bottom - (CH - BaseY) * s;
        winLeft = x - CenterX * s;
        winTop = ground - BaseY * s;
        Phase = GrassPhase.Grow;
        t = 0;
        nextSway = 4 + r.NextDouble() * 3;
        return true;
    }

    /// <summary>Disparition immédiate (pause, visite, jeu).</summary>
    public void Stop()
    {
        Phase = GrassPhase.Off;
        ov.Hide();
    }

    void OnClick()
    {
        if (Phase != GrassPhase.Wait) return;
        double roll = r.NextDouble();
        outcome = roll < 0.40 ? GrassOutcome.Nothing : roll < 0.85 ? GrassOutcome.Object : GrassOutcome.Critter;
        Phase = GrassPhase.Result;
        t = 0;
        Item? found = null;
        if (outcome == GrassOutcome.Object) { item = Items.HerbObject(r); found = item; }
        if (outcome == GrassOutcome.Critter)
        {
            item = Items.Critter(r);
            found = item;
            critX = CenterX; critY = BaseY; critZ = 0; critT = 0;
            critDir = r.Next(2) * 2 - 1;
            critMoved = false;
        }
        for (int i = 0; i < leafX.Length; i++)
        {
            leafX[i] = CenterX + r.Next(-4, 5); leafY[i] = BaseY - 4;
            leafVX[i] = (r.NextDouble() - 0.5) * 22; leafVY[i] = -12 - r.NextDouble() * 12;
        }
        Resolved?.Invoke(outcome, found);
    }

    public void Tick(double dt, int scale, nint homeHwnd)
    {
        home = homeHwnd;
        if (Phase == GrassPhase.Off) { if (ov.Visible) ov.Hide(); return; }
        if (scale != (int)s) { Stop(); return; }              // la taille a changé : la touffe n'est plus au bon endroit
        t += dt;

        switch (Phase)
        {
            case GrassPhase.Grow:
                if (t >= GrowTime) { Phase = GrassPhase.Wait; t = 0; nextSway = 4 + r.NextDouble() * 3; }
                break;
            case GrassPhase.Wait:
                if (t > nextSway + 0.6) nextSway = t + 5 + r.NextDouble() * 4;
                if (t > WaitMax) { Phase = GrassPhase.Leave; t = 0; }
                break;
            case GrassPhase.Result:
                UpdateResult(dt);
                break;
            case GrassPhase.Leave:
                if (t >= LeaveTime) { Stop(); return; }
                break;
        }
        Render();
    }

    void UpdateResult(double dt)
    {
        for (int i = 0; i < leafX.Length; i++)
        {
            leafX[i] += leafVX[i] * dt; leafY[i] += leafVY[i] * dt; leafVY[i] += 60 * dt;
        }
        switch (outcome)
        {
            case GrassOutcome.Nothing:
                if (t > 0.9) { Phase = GrassPhase.Leave; t = 0; }
                break;
            case GrassOutcome.Object:
                if (t > 3) { Phase = GrassPhase.Leave; t = 0; }
                break;
            case GrassOutcome.Critter:
                critT += dt;
                MoveCritter(dt);
                bool out_ = critX < 4 || critX > CW - 4 || critY - critZ < 3;
                if (critT > 10 || (critMoved && out_)) { Phase = GrassPhase.Leave; t = 0; }
                break;
        }
    }

    void MoveCritter(double dt)
    {
        switch (item)
        {
            case Item.Papillon:          // vole en zigzag vers le haut
                critX += critDir * 5 * dt;
                critZ = Math.Min(critT * 3.5, 16) + Math.Sin(critT * 7) * 2;
                critMoved = critT > 1.5;
                break;
            case Item.Coccinelle:        // trottine, s'arrête de temps en temps
                if (Math.Sin(critT * 2.2) > -0.3) critX += critDir * 6 * dt;
                critMoved = critT > 1;
                break;
            case Item.Escargot:          // très lentement
                critX += critDir * 2.2 * dt;
                critMoved = critT > 2;
                break;
            default:                     // grenouille : petits bonds
            {
                double ph = critT % 0.9;
                if (ph < 0.45) { critX += critDir * 14 * dt; critZ = Math.Sin(Math.PI * ph / 0.45) * 5; }
                else critZ = 0;
                critMoved = critT > 1;
                break;
            }
        }
    }

    // ------------------------------------------------------------------ rendu

    static double Ease(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);

    void Render()
    {
        canvas.Clear();

        // la touffe : pousse par le bas, ondule, rétrécit en partant
        double f = Phase switch
        {
            GrassPhase.Grow => Ease(t / GrowTime),
            GrassPhase.Leave => 1 - Ease(t / LeaveTime),
            _ => 1,
        };
        int rows = Math.Max(1, (int)Math.Ceiling(Glyphs.Tuft.Length * f));
        double sway = 0;
        if (Phase == GrassPhase.Wait && t >= nextSway && t < nextSway + 0.6) sway = Math.Sin((t - nextSway) * 28);
        else if (Phase == GrassPhase.Result && outcome == GrassOutcome.Nothing) sway = Math.Sin(t * 30);
        else if (Phase == GrassPhase.Result && t < 0.6) sway = Math.Sin(t * 26);
        if (f > 0.02)
            for (int i = 0; i < rows; i++)
            {
                int row = Glyphs.Tuft.Length - rows + i;          // les rangées du haut ondulent
                int shift = row < 3 ? (int)Math.Round(sway * (3 - row) * 0.5) : 0;
                Glyphs.DrawAt(canvas, [Glyphs.Tuft[row]], CenterX - 6 + shift, BaseY - rows + i);
            }

        if (Phase == GrassPhase.Result) DrawResult();
        ov.Present(canvas, (int)s, (int)Math.Round(winLeft), (int)Math.Round(winTop), home);
    }

    void DrawResult()
    {
        uint leaf = PixelCanvas.Rgb(175, 232, 115);
        if (t < 1.4 && outcome != GrassOutcome.Critter || t < 0.8)
            for (int i = 0; i < leafX.Length; i++) canvas.Set((int)leafX[i], (int)leafY[i], leaf, Layer.Fx);

        switch (outcome)
        {
            case GrassOutcome.Object:
            {
                // l'objet jaillit de l'herbe et brille
                double up = Ease(t / 0.5) * 8;
                var g = Glyphs.ForItem(item);
                int y = BaseY - 10 - (int)up;
                Glyphs.Draw(canvas, g, CenterX, y);
                if (t > 0.5)
                {
                    uint y1 = PixelCanvas.Rgb(255, 224, 90);
                    int k = (int)(t * 6) % 3;
                    canvas.Set(CenterX - 7 + k, y - 4, y1, Layer.Fx);
                    canvas.Set(CenterX + 7 - k, y + 2, y1, Layer.Fx);
                    canvas.Set(CenterX + 5, y - 6 + k, y1, Layer.Fx);
                }
                break;
            }
            case GrassOutcome.Critter:
            {
                var g = Glyphs.ForItem(item);
                if (critDir < 0) g = Flip(g);
                if (item == Item.Papillon && (int)(critT * 10) % 2 == 0) g = Glyphs.Butterfly[..4];   // ailes qui battent
                int h = g.Length;
                // sort de l'herbe : n'apparaît que lorsqu'elle s'écarte
                int gx = (int)Math.Round(critX), gy = (int)Math.Round(critY - critZ) - h / 2 - 1;
                if (critT > 0.25) Glyphs.Draw(canvas, g, gx, gy);
                break;
            }
        }
    }

    static string[] Flip(string[] g)
    {
        var o = new string[g.Length];
        for (int i = 0; i < g.Length; i++) { var a = g[i].ToCharArray(); Array.Reverse(a); o[i] = new string(a); }
        return o;
    }
}
