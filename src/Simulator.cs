namespace Pepin;

/// <summary>
/// Outil de dev : `Pepin.exe --simulate [heures]` fait vivre la tortue sans fenêtre, avec une souris
/// et un utilisateur factices, puis affiche les comportements observés et les anomalies.
/// </summary>
public static class Simulator
{
    public static int Run(double hours)
    {
        var mood = new Mood { Energy = 0.8, Hunger = 0.3, Happiness = 0.7, Affection = 0.4 };
        var s = new Senses { Work = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 }, CursorOnSameMonitor = true };
        var life = new Life(new LifeData { Bond = 200 });          // proche du palier « Ami » pour voir les débloqués
        var pet = new Pet(mood, s) { Scale = 3, X = 1500, Y = 900, Life = life };
        var canvas = new PixelCanvas(TurtleArt.CW, TurtleArt.CH);
        var rnd = new Random(42);
        var counts = new SortedDictionary<string, int>();
        var time = new SortedDictionary<string, double>();
        string last = "";
        int problems = 0;
        const double dt = 1 / 15.0;
        double tx = 900, ty = 500, lastInput = 0, awayUntil = -1;
        double nextPet = 120, nextClick = 200, nextSpam = 700, nextThrow = 400, nextAway = 1800;
        double petUntil = -1, dragUntil = -1;
        int spamLeft = 0;

        for (double t = 0; t < hours * 3600; t += dt)
        {
            bool away = t < awayUntil;
            double prevX = s.CX, prevY = s.CY;

            // l'utilisateur factice
            if (!away)
            {
                if (t > nextAway) { awayUntil = t + 900; nextAway = t + 3600; }
                else if (t > nextPet) { petUntil = t + 4; nextPet = t + rnd.Next(120, 400); }
                else if (t > nextThrow && dragUntil < 0) { pet.OnGrab(); dragUntil = t + 1.5; nextThrow = t + rnd.Next(300, 900); }
                else if (t > nextClick) { pet.OnClick(); nextClick = t + rnd.Next(150, 500); }
                else if (t > nextSpam) { spamLeft = 5; nextSpam = t + rnd.Next(600, 1500); }

                if (petUntil > t)
                {
                    // frotte doucement sur la carapace
                    s.CX = pet.X + Math.Sin(t * 3) * 25; s.CY = pet.Y - 30 - pet.Z;
                }
                else if (dragUntil > t)
                {
                    s.CX += 25; s.CY -= 8;
                }
                else
                {
                    if (rnd.NextDouble() < 0.01) { tx = rnd.Next(0, 1920); ty = rnd.Next(0, 1040); }
                    double spd = rnd.NextDouble() < 0.002 ? 4000 : 400;
                    double dx = tx - s.CX, dy = ty - s.CY, d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > 1) { double st = Math.Min(d, spd * dt); s.CX += dx / d * st; s.CY += dy / d * st; }
                }
                if (spamLeft > 0 && rnd.NextDouble() < 0.3) { spamLeft--; pet.OnClick(); }
                if (Math.Abs(s.CX - prevX) + Math.Abs(s.CY - prevY) > 0.5) lastInput = t;
            }
            if (dragUntil > 0 && t >= dragUntil)
            {
                pet.OnRelease(1800, -600);
                dragUntil = -1;
            }
            s.Speed = Math.Sqrt((s.CX - prevX) * (s.CX - prevX) + (s.CY - prevY) * (s.CY - prevY)) / dt;
            s.IdleSeconds = t - lastInput;

            if (pet.Dragging) pet.DragFollow();
            int wx = (int)Math.Round(pet.X) - TurtleArt.AX * 3, wy = (int)Math.Round(pet.Y - pet.Z) - TurtleArt.AY * 3;
            pet.CursorOnMe = canvas.IsSolid((int)Math.Floor((s.CX - wx) / 3), (int)Math.Floor((s.CY - wy) / 3));
            try
            {
                pet.Update(dt);
                TurtleArt.Draw(canvas, pet.V);
            }
            catch (Exception e)
            {
                if (problems++ < 5) Console.WriteLine($"[{t:0}s] EXCEPTION dans {pet.Current.Label}: {e.Message}");
            }

            if (!pet.Dragging && (pet.X < 0 || pet.X > 1920 || pet.Y < 0 || pet.Y > 1040 || double.IsNaN(pet.X) || pet.Z < -1))
                if (problems++ < 5) Console.WriteLine($"[{t:0}s] HORS ÉCRAN ({pet.X:0},{pet.Y:0},{pet.Z:0}) dans {pet.Current.Label}");

            string label = pet.Current.Label;
            time[label] = time.GetValueOrDefault(label) + dt;
            if (label != last) { counts[label] = counts.GetValueOrDefault(label) + 1; last = label; }
        }

        Console.WriteLine($"Simulation de {hours} h — {problems} anomalie(s)");
        Console.WriteLine($"{"comportement",-26} {"fois",5} {"minutes",8}");
        foreach (var (k, n) in counts.OrderByDescending(kv => time[kv.Key]))
            Console.WriteLine($"{k,-26} {n,5} {time[k] / 60,8:0.0}");
        Console.WriteLine($"Jauges finales : {mood.Describe()}");
        Console.WriteLine($"{life.Summary()} — lien {life.D.Bond:0} pts — collection : {string.Join(", ", life.D.Collection.Select(kv => $"{kv.Key}×{kv.Value}"))}");
        Console.WriteLine("Journal (dernières entrées) :");
        foreach (var j in life.D.Journal.TakeLast(12)) Console.WriteLine($"  {(j.Private ? "[privé] " : "")}{j.Text}");
        problems += Duo();
        return problems == 0 ? 0 : 1;
    }

    /// <summary>
    /// Outil de dev : `--bandtest [id]` inscrit une tortue de test, l'envoie en visite (chez `id` ou au hasard)
    /// et attend son retour. Sert à tester une vraie visite de bout en bout avec une instance réelle comme hôte.
    /// </summary>
    public static int BandTest(string? to)
    {
        var d = new LifeData();
        var band = new Band(d);
        var events = new List<BandEventDto>();
        band.OnEvent = e => { events.Add(e); Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] évènement {e.Type} : {e.Visit?.Host?.Name ?? e.Visit?.From?.Name} souvenir={e.Visit?.Souvenir} activités={string.Join(",", e.Visit?.Played ?? [])} raison={e.Visit?.Reason}"); };
        band.Start();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (d.BandToken is null && sw.Elapsed.TotalSeconds < 30) { band.DrainUi(); Thread.Sleep(200); }
        if (d.BandToken is null) { Console.WriteLine("inscription impossible"); return 1; }
        Console.WriteLine($"tortue de test : {d.Name} ({d.BandId})");
        band.Poke();
        Thread.Sleep(3000);
        band.DrainUi();
        foreach (var t in band.Turtles) Console.WriteLine($"  bande : {t.Name} ({t.Id}) en ligne={t.Online} statut={t.Status}");
        var req = band.RequestVisit(to, "coucou, c'est un test !");
        req.Wait(30000);
        var (visit, err) = req.Result;
        if (visit is null) { Console.WriteLine($"visite refusée : {err}"); return 1; }
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] visite {visit.Id} chez {visit.Host?.Name}, durée {visit.Duration} s");
        band.Status = "visiting";
        band.Fast = true;
        while (sw.Elapsed.TotalMinutes < 12 && !events.Any(e => e.Type == "return")) { band.DrainUi(); Thread.Sleep(500); }
        return events.Any(e => e.Type == "return") ? 0 : 1;
    }

    /// <summary>Deux tortues qui enchaînent les activités d'une visite : tout doit se terminer, sans sortir de l'écran.</summary>
    static int Duo()
    {
        var work = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        var hs = new Senses { Work = work, CursorOnSameMonitor = true, CX = 200, CY = 200, IdleSeconds = 1 };
        var gs = new Senses { Work = work, CursorOnSameMonitor = true, CX = 200, CY = 200, IdleSeconds = 1 };
        var host = new Pet(new Mood { Energy = 0.9, Happiness = 0.8 }, hs, new Idle()) { Scale = 3, X = 900, Y = 700 };
        var guest = new Pet(new Mood { Energy = 0.9, Happiness = 0.8 }, gs, new Idle()) { Scale = 3, X = 900, Y = 700, IsGuest = true };
        host.Partner = guest; guest.Partner = host;
        host.Orchestrator = () => new WatchFriend();
        guest.Switch(new Arrive());
        int problems = 0;
        const double dt = 1 / 20.0;
        void Run(string name, Func<bool> done, double max)
        {
            double t = 0;
            for (; t < max && !done(); t += dt)
            {
                host.Update(dt); guest.Update(dt);
                if (!guest.OffScreen && (guest.X < 0 || guest.X > 1920 || guest.Y < 0 || guest.Y > 1040)) { problems++; Console.WriteLine($"  {name} : invitée hors écran"); break; }
            }
            double gap = Math.Abs(host.X - guest.X) / 3;
            Console.WriteLine($"  {name,-10} {(t >= max ? "PAS FINI" : "ok")} en {t,5:0.0} s   écart {gap,4:0} px logiques   hôte: {host.Current.Label}");
            if (t >= max) problems++;
        }
        Console.WriteLine("Visite simulée :");
        Run("arrivée", () => guest.Current is not Arrive, 20);
        foreach (var act in new[] { "renifler", "chat", "gouter", "sieste", "danse" })
        {
            Behavior g, h;
            switch (act)
            {
                case "renifler": g = new Sniff(true); h = new Sniff(false); break;
                case "chat": var st = new TagState { It = guest }; g = new TagGame(true, st, 12); h = new TagGame(true, st, 12); break;
                case "gouter": g = new ShareSnack(true, false); h = new ShareSnack(false, true); break;
                case "sieste": g = new NapTogether(true, 20); h = new NapTogether(false, 20); break;
                default: g = new DanceTogether(true); h = new DanceTogether(false); break;
            }
            guest.Switch(g); host.Switch(h);
            Run(act, () => g.Done, 60);
        }
        guest.Switch(new LeaveScreen(new AwayOnVisit()));
        Run("départ", () => guest.Current is AwayOnVisit, 20);
        return problems;
    }
}
