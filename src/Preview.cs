namespace Pepin;

/// <summary>Outil de dev : `Pepin.exe --preview [dossier]` dessine en PNG la carte de collection, la touffe d'herbe et une partie de bowling simulée.</summary>
public static class Preview
{
    static readonly uint Bg = PixelCanvas.Rgb(70, 110, 80);

    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);

        // carte de collection
        var col = new Dictionary<string, int> { ["fraise"] = 3, ["trefle"] = 1, ["coccinelle"] = 2, ["papillon"] = 1, ["fleur"] = 5, ["champignon"] = 1, ["escargot"] = 1 };
        var mine = new Dictionary<string, int> { ["fraise"] = 1, ["fleur"] = 2 };
        DeckView.Render("Clémentine d'Été", "Ami - amitié 12", col, mine).SavePng(Path.Combine(dir, "deck.png"), 4, Bg);

        var work = new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        var mood = new Mood { Energy = 0.9, Hunger = 0.2, Happiness = 0.8, Affection = 0.5 };

        // touffe d'herbe : quelques états côte à côte
        var s = new Senses { Work = work, CursorOnSameMonitor = true };
        var pet = new Pet(mood, s) { Scale = 3, X = 300, Y = 900 };
        var sheet = new PixelCanvas(96 * 3 + 4 * 2, 34 * 4 + 5 * 2);
        int n = 0;
        void Cell(GrassEvent g)
        {
            g.Canvas.BlitTo(sheet, 2 + (n % 3) * 98, 2 + (n / 3) * 36);
            n++;
        }
        GrassEvent Fresh(double t)
        {
            var g = new GrassEvent();
            g.TrySpawn(pet);
            for (double x = 0; x < t; x += 0.05) g.Tick(0.05, 3, 0);
            return g;
        }
        Cell(Fresh(0.3));                                   // pousse
        Cell(Fresh(1.0));                                   // attend
        Cell(Fresh(5.1));                                   // ondule
        var r = Fresh(1); r.DebugForce(GrassOutcome.Nothing, Item.Bouton); for (int i = 0; i < 6; i++) r.Tick(0.05, 3, 0); Cell(r);
        r = Fresh(1); r.DebugForce(GrassOutcome.Object, Item.Champignon); for (int i = 0; i < 24; i++) r.Tick(0.05, 3, 0); Cell(r);
        r = Fresh(1); r.DebugForce(GrassOutcome.Object, Item.Pissenlit); for (int i = 0; i < 24; i++) r.Tick(0.05, 3, 0); Cell(r);
        foreach (var an in new[] { Item.Coccinelle, Item.Escargot, Item.Grenouille, Item.Papillon })
        {
            r = Fresh(1); r.DebugForce(GrassOutcome.Critter, an); for (int i = 0; i < 50; i++) r.Tick(0.05, 3, 0); Cell(r);
        }
        sheet.SavePng(Path.Combine(dir, "herbe.png"), 4, Bg);

        // bowling : piste prête, puis le lancer en quatre images
        var b = new Bowling(new Overlay());
        var bs = new Senses { Work = work, CursorOnSameMonitor = true };
        var bp = new Pet(mood, bs) { Scale = 3, X = 600, Y = 700, Bowl = b };
        if (!b.TryStart(bp)) { Console.WriteLine("pas de place pour la piste"); return 1; }
        b.Begin();
        double dt = 1 / 30.0;
        for (double t = 0; t < 3 && b.Phase != BowlPhase.Wait; t += dt) b.Tick(dt, bp, 3, 0);
        var strip = new PixelCanvas(70 * 4 + 10, 68);
        int k = 0;
        void Frame() { b.Canvas.BlitTo(strip, 2 + k * 72, 0); k++; }
        Frame();
        var (sx, sy) = b.Start;
        bp.X = sx; bp.Y = sy;
        bp.Switch(new Thrown(1200));
        bp.VX = b.Dir * 1100; bp.VY = 40; bp.Z = 0; bp.VZ = 0;
        b.NoteThrow();
        double[] at = [0.35, 0.6, 1.2];
        int ai = 0;
        for (double t = 0; t < 2 && ai < at.Length; t += dt)
        {
            bp.Update(dt);
            b.Tick(dt, bp, 3, 0);
            if (t >= at[ai]) { Frame(); ai++; }
        }
        strip.SavePng(Path.Combine(dir, "bowling.png"), 4, Bg);
        Console.WriteLine($"quilles tombées : {b.Fell} (lancers : {b.Throws}), phase {b.Phase}");

        // vrais lancers : même chemin que la souris (OnRelease), à plusieurs vitesses et avec un peu d'angle
        Console.WriteLine("lancer (px/s, angle) -> quilles tombées");
        foreach (double speed in new[] { 350.0, 500, 800, 1200, 1800, 2500 })
            foreach (double dy in new[] { 0.0, 0.08, -0.15 })
            {
                var gb = new Bowling(new Overlay());
                var gp = new Pet(mood, new Senses { Work = work, CursorOnSameMonitor = true }, new Idle()) { Scale = 3, X = 600, Y = 700, Bowl = gb };
                gb.TryStart(gp);
                gb.Begin();
                for (double t = 0; t < 3 && gb.Phase != BowlPhase.Wait; t += dt) gb.Tick(dt, gp, 3, 0);
                var (gx, gy) = gb.Start;
                gp.X = gx; gp.Y = gy;
                gp.OnGrab();
                gp.OnRelease(gb.Dir * speed, speed * dy);
                for (double t = 0; t < 6; t += dt) { gp.Update(dt); gb.Tick(dt, gp, 3, 0); if (gb.Phase == BowlPhase.Over) break; }
                Console.WriteLine($"  {speed,5:0} {dy,5:0.00} -> {gb.Fell} (phase {gb.Phase}, lancers {gb.Throws})");
            }
        // une partie entière : deux lancers de suite, comme à la souris, puis (ou pas) le rangement
        Console.WriteLine("partie complète (2 lancers) :");
        foreach (double s2 in new[] { 1500.0, 600 })
        {
            var gb = new Bowling(new Overlay());
            var gp = new Pet(mood, new Senses { Work = work, CursorOnSameMonitor = true }, new Idle()) { Scale = 3, X = 600, Y = 700, Bowl = gb };
            gp.Switch(new BowlSetup());
            double now = 0;
            void Run(double secs, Action? each = null)
            {
                for (double t = 0; t < secs; t += dt) { now += dt; gp.Update(dt); gb.Tick(dt, gp, 3, 0); each?.Invoke(); }
            }
            Run(6);
            Console.WriteLine($"  [{s2}] prête : phase {gb.Phase}, comportement {gp.Current.GetType().Name}");
            for (int throwNo = 1; throwNo <= (s2 == 600 ? 1 : 2) && gb.Active; throwNo++)   // à 600 on ne rejoue pas : le tour doit être rangé tout seul
            {
                var (gx, gy) = gb.Start;
                gp.X = gx; gp.Y = gy;                       // l'utilisatrice la ramène au point de lancer
                gp.OnGrab();
                gp.OnRelease(gb.Dir * s2, throwNo == 1 ? 60 : -90);
                Run(10);
                Console.WriteLine($"  [{s2}] après lancer {throwNo} : tombées {gb.Fell}, phase {gb.Phase}, comportement {gp.Current.GetType().Name}, actif {gb.Active}");
            }
            if (gb.Active) { Console.WriteLine($"  [{s2}] tour non fini ({gb.Fell} tombées) : on n'y touche plus"); Run(60); }
            Run(10);
            Console.WriteLine($"  [{s2}] plus tard : phase {gb.Phase}, actif {gb.Active}, quilles à l'écran {(gb.Fps > 0 ? "oui" : "non")}");
        }

        Console.WriteLine($"écrit dans {Path.GetFullPath(dir)}");
        return 0;
    }
}
