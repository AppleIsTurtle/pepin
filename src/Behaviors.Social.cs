namespace Pepin;

// La bande : partir en visite, arriver chez un ami, jouer à deux, rentrer.

/// <summary>Demande une visite au serveur, puis s'en va (ou hausse les épaules si personne n'est dispo).</summary>
sealed class LeaveForVisit : Behavior
{
    readonly string? to, message;
    Task<(VisitDto? visit, string? error)>? req;
    int phase;
    double phaseStart;
    VisitDto? visit;
    public LeaveForVisit(string? to, string? message) { this.to = to; this.message = message; }
    public override bool Interruptible => false;
    public override string Label => phase == 1 ? "part en visite" : "prépare sa visite";
    public override void Start(Pet p)
    {
        if (p.Band is null || !p.Band.Registered || p.Band.Outgoing is not null || p.Band.Hosting) { Done = true; return; }
        req = p.Band.RequestVisit(to, message);
    }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        double pt = T - phaseStart;
        switch (phase)
        {
            case 0: // fait son sac en attendant la réponse
                v.LookX = Every(T, 1.2) ? 1 : -1;
                if (T > 1.5) v.Add(FxKind.Question, (float)T);
                if (req!.IsCompleted)
                {
                    (visit, string? err) = req.Result;
                    phaseStart = T;
                    if (visit is not null) phase = 1;
                    else { phase = 2; p.Say(Band.ErrorText(err), 3); }
                }
                else if (T > 25) { phase = 2; phaseStart = T; p.Say(Band.ErrorText("hors_ligne"), 3); }
                break;
            case 1: // en route !
                v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
                v.Add(FxKind.Exclaim, (float)pt);
                Hop(p, pt, 0.4, 5);
                if (pt > 1.2)
                {
                    Done = true;
                    p.Band!.Outgoing = visit;
                    p.Band.Fast = true;
                    if (p.Life is not null) p.Life.D.LastVisitUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    p.Say($"Je vais voir {visit!.Host?.Name} !", 2.5);
                    p.Next = new LeaveScreen(new AwayOnVisit());
                }
                break;
            case 2: // personne
                v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Pout;
                v.Add(FxKind.Question, (float)pt);
                if (pt > 2.2) Done = true;
                break;
        }
    }
}

/// <summary>Rentre dans sa carapace et glisse hors de l'écran par le bord le plus proche.</summary>
sealed class LeaveScreen : Behavior
{
    readonly Behavior? then;
    double tx, spin;
    public LeaveScreen(Behavior? then) => this.then = then;
    public override int Fps => 30;
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override string Label => "s'en va";
    public override void OnPetting(Pet p) { }
    public override void OnPoke(Pet p, int count) { }
    public override void OnStartle(Pet p) { }
    public override void Start(Pet p)
    {
        bool toLeft = p.X - p.S.Work.Left < p.S.Work.Right - p.X;
        tx = toLeft ? p.S.Work.Left - 45 * p.Scale : p.S.Work.Right + 45 * p.Scale;
        p.OffScreen = true;
    }
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        p.OffScreen = true;
        if (T < 0.35) { v.HeadOut = (float)(1 - T / 0.35); v.LegsTuck = (int)(T / 0.35 * 3); return; }
        v.InShell = true;
        spin += dt * 20;
        v.SpinFrame = (int)spin & 3;
        v.FacingRight = tx > p.X;
        v.Add(FxKind.Speed, (float)(T * 3));
        double step = 150 * p.Scale * dt;
        if (Math.Abs(tx - p.X) <= step) { p.X = tx; Done = true; p.Next = then; }
        else p.X += Math.Sign(tx - p.X) * step;
    }
}

/// <summary>Ma tortue est chez quelqu'un : invisible, elle attend le retour (App la réveille).</summary>
sealed class AwayOnVisit : Behavior
{
    public override int Fps => 2;
    public override bool Interruptible => false;
    public override bool TrackEyes => false;
    public override string Label => "est en visite";
    public override void OnPetting(Pet p) { }
    public override void OnPoke(Pet p, int count) { }
    public override void OnStartle(Pet p) { }
    public override void Start(Pet p) => p.OffScreen = true;
    public override void Tick(Pet p, double dt)
    {
        p.OffScreen = true;
        // filet de sécurité : pas de nouvelles au bout de 15 min, on rentre quand même
        if (T > 15 * 60)
        {
            if (p.Band?.Outgoing is VisitDto o) p.Band.Abort(o.Id);
            if (p.Band is not null) { p.Band.Outgoing = null; p.Band.Fast = false; }
            Done = true;
            p.Next = new ComeBack(null, noOneHome: true, "");
        }
    }
}

/// <summary>Entre à l'écran par un bord, dans sa carapace, jusqu'à un point ; puis sort la tête.</summary>
abstract class SlideIn : Behavior
{
    protected double tx, ty, arrivedAt = -1, spin;
    public override int Fps => 30;
    public override bool TrackEyes => false;
    public override bool Interruptible => false;
    public override void OnPetting(Pet p) { }
    public override void OnStartle(Pet p) { }

    protected void Enter(Pet p, double targetX, double targetY)
    {
        (tx, ty) = p.ClampPoint(targetX, targetY);
        bool fromLeft = tx - p.S.Work.Left < p.S.Work.Right - tx;
        p.X = fromLeft ? p.S.Work.Left - 40 * p.Scale : p.S.Work.Right + 40 * p.Scale;
        p.Y = ty;
        p.OffScreen = true;
    }

    /// <summary>Renvoie le temps écoulé depuis l'arrivée (négatif tant qu'elle glisse).</summary>
    protected double Slide(Pet p, double dt)
    {
        var v = p.V;
        if (arrivedAt < 0)
        {
            p.OffScreen = true;
            v.InShell = true;
            spin += dt * 18;
            v.SpinFrame = (int)spin & 3;
            v.FacingRight = tx > p.X;
            double dx = tx - p.X, speed = Math.Min(150, Math.Abs(dx) / p.Scale * 2.5 + 25), step = speed * p.Scale * dt;
            if (speed > 60) v.Add(FxKind.Speed, (float)(T * 3));
            if (Math.Abs(dx) <= step) { p.X = tx; arrivedAt = T; }
            else p.X += Math.Sign(dx) * step;
            return -1;
        }
        double t = T - arrivedAt;
        if (t < 0.25) v.InShell = true;
        else if (t < 0.65) { double k = (t - 0.25) / 0.4; v.HeadOut = (float)k; v.LegsTuck = 3 - (int)(k * 3); v.Eyes = Eyes.Wide; }
        return t;
    }
}

/// <summary>Un visiteur débarque chez nous, à côté de notre tortue.</summary>
sealed class Arrive : SlideIn
{
    public override string Label => "arrive";
    public override void Start(Pet p)
    {
        var host = p.Partner!;
        bool left = host.X - p.S.Work.Left < p.S.Work.Right - host.X;
        if (p.Slot % 2 == 1) left = !left;                     // les visiteurs suivants se répartissent des deux côtés
        int gap = 50 + 34 * ((p.Slot + 1) / 2);
        Enter(p, host.X + (left ? -gap : gap) * p.Scale, host.Y);
    }
    public override void Tick(Pet p, double dt)
    {
        double t = Slide(p, dt);
        if (t < 0.65) return;
        var v = p.V;
        if (p.Partner is Pet q) p.FaceX(q.X);
        v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
        v.Add(FxKind.Hearts, (float)t, 1);
        Hop(p, t - 0.65, 0.35, 4);
        if (t > 2) { p.Z = 0; Done = true; }
    }
}

/// <summary>Ma tortue rentre de visite, avec son souvenir (ou bredouille).</summary>
sealed class ComeBack : SlideIn
{
    readonly Item? souvenir;
    readonly bool noOneHome;
    readonly string host;
    public ComeBack(Item? souvenir, bool noOneHome, string host) { this.souvenir = souvenir; this.noOneHome = noOneHome; this.host = host; }
    public override string Label => "rentre de visite";
    public override void Start(Pet p)
    {
        // revient près de toi
        Enter(p, p.S.CX, p.S.CY + 40 * p.Scale);
    }
    public override void Tick(Pet p, double dt)
    {
        double t = Slide(p, dt);
        if (t < 0.65) return;
        var v = p.V;
        p.FaceCursor();
        if (noOneHome)
        {
            v.Eyes = Eyes.HalfLid; v.Mouth = Mouth.Pout;
            v.Add(FxKind.Question, (float)t);
            if (t > 0.7 && t < 0.8 && host != "") p.Say($"Personne chez {host}…", 3);
        }
        else
        {
            v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true;
            if (souvenir is Item it) { v.Food = it; v.Add(FxKind.Food, 0, 1); }
            v.Add(FxKind.Notes, (float)t, 2);
            Hop(p, (t - 0.65) % 0.45, 0.45, t < 2 ? 5 : 0);
            if (t > 0.7 && t < 0.8 && souvenir is Item s) p.Say($"Regarde, {Items.Label(s)} !", 3.5);
        }
        if (t > 3.2) { p.Z = 0; Done = true; }
    }
}

/// <summary>Après une mise à jour : « nouvelle carapace ! »</summary>
sealed class NewShell : Behavior
{
    public override int Fps => 24;
    public override string Label => "a fait peau neuve";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (T < 0.8) { v.InShell = true; v.SpinFrame = (int)(T * 12) & 3; }
        else { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; Hop(p, (T - 0.8) % 0.5, 0.5, T < 2 ? 4 : 0); }
        v.Add(FxKind.Sparkles, (float)(T * 0.8));
        if (T > 0.85 && T < 0.95) p.Say(p.Species == Species.Tortue ? "Nouvelle carapace !" : "Tout beau, tout neuf !", 2.5);
        if (T > 3) { p.Z = 0; Done = true; }
    }
}

// ================================================================= activités à deux (pendant une visite)

/// <summary>Base : se placer par rapport au partenaire. Seul le « mover » se déplace, l'autre attend.</summary>
abstract class Duo : Behavior
{
    protected readonly bool mover;
    protected Duo(bool mover) => this.mover = mover;
    public override int Fps => 20;
    public override bool TrackEyes => false;

    /// <summary>Se met à `gap` pixels logiques du partenaire, même hauteur. Vrai une fois placé.</summary>
    protected bool Beside(Pet p, double gap, double dt)
    {
        if (p.Partner is not Pet q) return true;
        if (!mover) { p.FaceX(q.X); return Math.Abs(q.X - p.X) < (gap + 6) * p.Scale && Math.Abs(q.Y - p.Y) < 8 * p.Scale; }
        double tx = p.X <= q.X ? q.X - gap * p.Scale : q.X + gap * p.Scale;
        bool there = p.WalkTo(tx, q.Y, 12, dt);
        if (there) p.FaceX(q.X);
        return there;
    }

    /// <summary>Le partenaire joue-t-il le jeu (même activité en cours) ?</summary>
    protected bool PartnerIn<TB>(Pet p) where TB : Behavior => p.Partner?.Current is TB;
}

sealed class Sniff : Duo
{
    double at = -1;
    public Sniff(bool mover) : base(mover) { }
    public override string Label => "renifle son ami";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (at < 0) { if (Beside(p, 38, dt) || T > 8) at = T; return; }
        double t = T - at;
        if (p.Partner is Pet q) p.FaceX(q.X);
        v.HeadDx = 1;
        v.Eyes = t < 1.5 ? Eyes.Normal : Eyes.Happy;
        if (t < 1.5) v.Add(FxKind.Question, (float)t); else v.Add(FxKind.Hearts, (float)t, 1);
        if (t > 4.5) Done = true;
    }
}

/// <summary>Chat : celui qui « l'est » poursuit l'autre ; quand il le touche, les rôles s'inversent.</summary>
sealed class TagState { public Pet? It; public double SwapAt = -10; }   // SwapAt : horloge de l'appli (commune aux deux tortues)

sealed class TagGame : Duo
{
    readonly TagState st;
    readonly double dur;
    double rx, ry, reroute;
    public TagGame(bool mover, TagState st, double dur) : base(mover) { this.st = st; this.dur = dur; }
    public override int Fps => 24;
    public override string Label => "joue à chat";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (p.Partner is not Pet q || T > dur) { Done = true; return; }
        v.Mouth = Mouth.Grin;
        double since = App.Now - st.SwapAt;
        if (since < 0.7)
        {
            // le choc : explosion et étincelles sur les deux, celui qui vient d'être touché en voit des étoiles
            v.Add(FxKind.Impact, (float)(since / 0.7));
            if (st.It == p) { v.Eyes = Eyes.Spiral; v.Mouth = Mouth.Oh; v.Add(FxKind.Stars, (float)(since * 2)); return; }
        }
        if (st.It == p)
        {
            v.Eyes = Eyes.Determined;
            p.WalkTo(q.X, q.Y, 17, dt);
            double dx = q.X - p.X, dy = q.Y - p.Y;
            if (dx * dx + dy * dy < 30 * 30 * p.Scale * p.Scale && App.Now - st.SwapAt > 1.2)
            {
                st.It = q; st.SwapAt = App.Now;                  // touché : choc, étincelles
            }
        }
        else
        {
            v.Eyes = Eyes.Happy;
            v.Add(FxKind.Notes, (float)T, 1);
            if (T > reroute)
            {
                // file à l'opposé du chat, un peu au hasard
                double ax = p.X - q.X, ay = p.Y - q.Y, n = Math.Max(1, Math.Sqrt(ax * ax + ay * ay));
                (rx, ry) = p.ClampPoint(p.X + ax / n * 70 * p.Scale + p.R.Next(-30, 30) * p.Scale, p.Y + ay / n * 40 * p.Scale);
                reroute = T + 1.5;
            }
            p.WalkTo(rx, ry, 14, dt);
        }
    }
}

sealed class ShareSnack : Duo
{
    readonly bool giver;
    double at = -1;
    public ShareSnack(bool mover, bool giver) : base(mover) => this.giver = giver;
    public override string Label => "partage un goûter";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (at < 0) { if (Beside(p, 40, dt) || T > 8) at = T; return; }
        double t = T - at;
        if (p.Partner is Pet q) p.FaceX(q.X);
        v.Food = Item.Fraise;
        if (t < 0.9)
        {
            v.Eyes = Eyes.Wide;
            if (giver) { v.Add(FxKind.Food, 0, 1); v.Add(FxKind.Exclaim, (float)t); }
        }
        else if (t < 5)
        {
            float left = (float)(1 - (t - 0.9) / 4.1);
            v.Add(FxKind.Food, 0, left);
            v.Mouth = Every(t, 0.3) ? Mouth.ChewA : Mouth.ChewB;
            v.Eyes = Eyes.Happy;
            v.Add(FxKind.Crumbs, (float)(t % 1));
        }
        else { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true; v.Add(FxKind.Hearts, (float)t, 1); }
        if (t > 6.2) Done = true;
    }
}

sealed class NapTogether : Duo
{
    readonly double dur;
    double at = -1;
    public NapTogether(bool mover, double dur) : base(mover) => this.dur = dur;
    public override bool Asleep => at >= 0;
    public override int Fps => at < 0 ? 15 : 6;
    public override string Label => "fait la sieste avec son ami";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (at < 0)
        {
            if (Beside(p, 44, dt) || T > 8)
            {
                at = T;
                if (p.Partner is Pet q) p.V.FacingRight = q.V.FacingRight;   // côte à côte, même sens
            }
            return;
        }
        double t = T - at;
        float k = (float)Math.Min(1, t / 1.2);
        v.HeadOut = 1 - 0.4f * k; v.LegsTuck = (int)(3 * k);
        v.Eyes = k < 1 ? Eyes.HalfLid : Eyes.Closed;
        if (k >= 1) v.Add(FxKind.Zzz, (float)(t / 2.4 + (mover ? 0.5 : 0)));
        if (t > dur) Done = true;
    }
    public override void OnPoke(Pet p, int count) { Done = true; p.Next = new WakeGrumpy(); }
}

sealed class DanceTogether : Duo
{
    double at = -1;
    public DanceTogether(bool mover) : base(mover) { }
    public override int Fps => 24;
    public override string Label => "danse avec son ami";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (at < 0) { if (Beside(p, 44, dt) || T > 8) at = T; return; }
        double t = T - at;
        v.FacingRight = Every(t, 1.2) ? true : false;
        v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
        v.Add(FxKind.Notes, (float)(t * 0.8), 2);
        Hop(p, (t + (mover ? 0.2 : 0)) % 0.4, 0.4, 5);
        if (t > 6) { p.Z = 0; Done = true; }
    }
}

/// <summary>Entre deux activités, la tortue de la maison regarde son invité avec bienveillance.</summary>
sealed class WatchFriend : Behavior
{
    double dur;
    public override int Fps => 10;
    public override string Label => "passe du temps avec son ami";
    public override void Start(Pet p) => dur = Rnd(p, 2, 4);
    public override void Tick(Pet p, double dt)
    {
        if (p.Partner is Pet q) p.FaceX(q.X);
        p.V.Eyes = Every(T, 3, 0.6) ? Eyes.Normal : Eyes.Happy;
        p.Breathe();
        if (T > dur) Done = true;
    }
}

/// <summary>Au revoir : cœurs et petit saut.</summary>
sealed class Goodbye : Behavior
{
    public override int Fps => 24;
    public override string Label => "dit au revoir";
    public override void Tick(Pet p, double dt)
    {
        var v = p.V;
        if (p.Partner is Pet q) p.FaceX(q.X);
        v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin;
        v.LegPhase = Every(T, 0.5) ? 3 : 0;
        v.Add(FxKind.Hearts, (float)(T * 0.6), 2);
        if (T > 2.4) Done = true;
    }
}
