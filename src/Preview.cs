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
        DeckView.Render("Clémentine d'Été", "Ami - amitié 12", col, mine, Species.Herisson).SavePng(Path.Combine(dir, "deck.png"), 4, Bg);

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

        Menus(dir);
        Chocs(dir);
        Eggs(dir);
        SpeciesOverview(dir);
        Console.WriteLine($"écrit dans {Path.GetFullPath(dir)}");
        return 0;
    }

    /// <summary>Les six espèces côte à côte : neutre, marche, content, endormi, trait propre, repli.</summary>
    static void SpeciesOverview(string dir)
    {
        var poses = new Action<Visual>[]
        {
            v => { },
            v => { v.LegPhase = 1; v.Eyes = Eyes.Determined; },
            v => { v.Eyes = Eyes.Happy; v.Mouth = Mouth.Grin; v.Blush = true; v.Add(FxKind.Hearts, .3f, 2); },
            v => { v.Eyes = Eyes.Closed; v.HeadOut = .6f; v.LegsTuck = 3; v.Add(FxKind.Zzz, .5f); v.Tint = PixelCanvas.Rgb(160, 190, 200); v.TintAmount = .3f; },
            v => { v.Puffed = true; v.Stand = true; v.Eyes = Eyes.Wide; v.Mouth = Mouth.Oh; v.Add(FxKind.Exclaim); },
            v => { v.Eyes = Eyes.Angry; v.Mouth = Mouth.Zigzag; v.Flush = .6f; v.Add(FxKind.Anger); },
            v => { v.InShell = true; v.SpinFrame = 1; v.Add(FxKind.Speed, .3f); },
        };
        var all = SpeciesInfo.All;
        const int pad = 2;
        var sheet = new PixelCanvas(poses.Length * (ArtKit.CW + pad) + pad, all.Length * (ArtKit.CH + pad) + pad);
        var one = new PixelCanvas(ArtKit.CW, ArtKit.CH);
        var vis = new Visual();
        for (int r = 0; r < all.Length; r++)
            for (int i = 0; i < poses.Length; i++)
            {
                vis.Reset(); vis.FacingRight = true; vis.Tint = 0; vis.TintAmount = 0; vis.AnimFrame = 3;
                poses[i](vis);
                SpeciesArt.Draw(one, vis, all[r]);
                one.BlitTo(sheet, pad + i * (ArtKit.CW + pad), pad + r * (ArtKit.CH + pad));
            }
        sheet.SavePng(Path.Combine(dir, "especes.png"), 3, PixelCanvas.Rgb(236, 240, 245));
    }

    /// <summary>L'œuf : posé, fissuré à chaque tape, puis l'éclosion (gonfle, flash, éclats, révélation).</summary>
    static void Eggs(string dir)
    {
        var frames = new List<PixelCanvas>();
        void Grab(Egg e, bool withPet = false, Visual? pv = null)
        {
            var f = new PixelCanvas(Egg.CW, Egg.CH);
            Array.Copy(e.Canvas.Px, f.Px, f.Px.Length);
            if (withPet)
            {
                var pc = new PixelCanvas(ArtKit.CW, ArtKit.CH);
                SpeciesArt.Draw(pc, pv!, e.Species);
                pc.BlitTo(f, Egg.Cx - ArtKit.AX, Egg.Ground - ArtKit.AY);
            }
            frames.Add(f);
        }
        var eg = new Egg(Simulator.Species, new Random(3));
        eg.Drop(0, 0, new RECT { Top = 0 }, 3);
        for (double t = 0; t < 3; t += 1 / 30.0) eg.Tick(1 / 30.0, 3, 0);
        Grab(eg);
        int guard = 0;
        while (eg.Phase == EggPhase.Idle && guard++ < 10)
        {
            eg.DebugTap();
            if (eg.Phase != EggPhase.Idle) break;
            for (int i = 0; i < 3; i++) eg.Tick(1 / 30.0, 3, 0);       // pendant la secousse
            Grab(eg);
            for (int i = 0; i < 20; i++) eg.Tick(1 / 30.0, 3, 0);
        }
        var pv = new Visual();
        double at = 0;
        foreach (double stop in new[] { 0.35, 0.6, 0.9, 1.6, 2.6 })
        {
            while (at < stop) { eg.Tick(1 / 30.0, 3, 0); at += 1 / 30.0; }
            pv.Reset(); pv.FacingRight = true; pv.LegsTuck = 3;
            pv.Eyes = stop < 1.5 ? Eyes.Wide : Eyes.Happy; pv.Mouth = stop < 1.5 ? Mouth.Oh : Mouth.Grin;
            if (stop < 1.5) pv.Add(FxKind.ShellHat, 0, 0); else pv.Add(FxKind.Sparkles, (float)stop);
            Grab(eg, withPet: stop > 0.56, pv);
        }
        const int gap = 4;
        var sheet = new PixelCanvas(4 * (Egg.CW + gap) + gap, ((frames.Count + 3) / 4) * (Egg.CH + gap) + gap);
        for (int i = 0; i < frames.Count; i++)
            frames[i].BlitTo(sheet, gap + (i % 4) * (Egg.CW + gap), gap + (i / 4) * (Egg.CH + gap));
        sheet.SavePng(Path.Combine(dir, "oeuf.png"), 3, PixelCanvas.Rgb(70, 110, 80));
        var ic = new PixelCanvas(16, 16);
        Egg.DrawIcon(ic);
        ic.SavePng(Path.Combine(dir, "oeuf-icone.png"), 8, PixelCanvas.Rgb(70, 110, 80));
    }

    /// <summary>Les pages du menu, côte à côte, avec un exemple d'état réaliste.</summary>
    static void Menus(string dir)
    {
        var m = new MenuModel
        {
            Name = "Pistou", Species = Simulator.Species, Tier = 2, Affection = 0.72, Energy = 0.45, Belly = 0.8,
            Summary = "Lien : Ami (40 % vers meilleur ami)", Today = "Aujourd'hui : 2 goûters, 1 sieste, 4 câlins",
            CanPlay = true, CanGrass = true, CanSend = true, Autostart = true, Spontaneous = true, Messages = true,
            MiniGames = false, Registered = true, SizeLevel = 3, Version = "3.0.0", BandStatus = "3/7 EN LIGNE",
        };
        m.Portrait.Eyes = Eyes.Happy; m.Portrait.Mouth = Mouth.Smile; m.Portrait.Blush = true;
        m.Band.Add(new BandEntry("a", "Trèfle", true, true, Species.Tortue));
        m.Band.Add(new BandEntry("b", "Clémentine d'Été", true, false, Species.Herisson));
        m.Band.Add(new BandEntry("c", "Noisette", true, true, Species.PandaRoux));
        m.Band.Add(new BandEntry("d", "Bulle", false, false, Species.Axolotl));
        m.Band.Add(new BandEntry("e", "Coquelicot", false, false, Species.Grenouille));
        m.Journal.Add(("12:04", "A trouvé un champignon dans l'herbe."));
        m.Journal.Add(("11:30", "Visite chez Trèfle : reniflage, danse, goûter partagé. Souvenir : un coquillage."));
        m.Journal.Add(("10:12", "A fait une grosse sieste (10 min)."));
        m.Journal.Add(("29/09", "A débarqué sur ton bureau."));

        const int gap = 6;
        var pages = new (MenuPage page, int hover, MenuModel model)[]
        {
            (MenuPage.Home, -1, m), (MenuPage.Home, 1, m), (MenuPage.Band, -1, m), (MenuPage.Carnet, -1, m), (MenuPage.Settings, -1, m),
        };
        var sheet = new PixelCanvas(pages.Length * (MenuPanel.W + gap) + gap, MenuPanel.H + gap * 2);
        for (int i = 0; i < pages.Length; i++)
            MenuPanel.RenderForPreview(pages[i].model, pages[i].page, pages[i].hover).BlitTo(sheet, gap + i * (MenuPanel.W + gap), gap);
        sheet.SavePng(Path.Combine(dir, "menu.png"), 3, PixelCanvas.Rgb(60, 72, 92));

        // état « moins rose » : visite en cours, endormi, jeux bloqués, œuf
        var busy = new MenuModel { Name = "Pistou", Species = Simulator.Species, Tier = 4, Affection = 1, Energy = 0.1, Belly = 0.2,
                                   Asleep = true, Paused = true, Version = "3.0.0", GuestName = "Clémentine d'Été", BandStatus = "1/7 EN LIGNE" };
        busy.Portrait.Eyes = Eyes.Closed; busy.Portrait.HeadOut = .6f; busy.Portrait.LegsTuck = 3;
        busy.Band.AddRange(m.Band);
        var egg = new MenuModel { Name = "???", Egg = true, Version = "3.0.0" };
        var sheet2 = new PixelCanvas(3 * (MenuPanel.W + gap) + gap, MenuPanel.H + gap * 2);
        MenuPanel.RenderForPreview(busy, MenuPage.Home).BlitTo(sheet2, gap, gap);
        MenuPanel.RenderForPreview(busy, MenuPage.Band).BlitTo(sheet2, gap * 2 + MenuPanel.W, gap);
        MenuPanel.RenderForPreview(egg, MenuPage.Home).BlitTo(sheet2, gap * 3 + MenuPanel.W * 2, gap);
        sheet2.SavePng(Path.Combine(dir, "menu-etats.png"), 3, PixelCanvas.Rgb(60, 72, 92));
    }

    /// <summary>Le choc entre deux compagnons (jeu de chat) : image par image, pour deux espèces.</summary>
    static void Chocs(string dir)
    {
        var phases = new[] { 0.04f, 0.12f, 0.22f, 0.35f, 0.5f, 0.65f, 0.8f, 0.92f };
        var species = new[] { Species.Tortue, Species.Herisson };
        var sheet = new PixelCanvas(phases.Length * (SpeciesArt.CW + 2) + 2, species.Length * (SpeciesArt.CH + 2) + 2);
        for (int r = 0; r < species.Length; r++)
            for (int i = 0; i < phases.Length; i++)
            {
                var v = new Visual();
                v.Reset();
                v.Eyes = Eyes.Spiral; v.Mouth = Mouth.Oh;
                v.Add(FxKind.Impact, phases[i]);
                var c = new PixelCanvas(SpeciesArt.CW, SpeciesArt.CH);
                SpeciesArt.Draw(c, v, species[r]);
                c.BlitTo(sheet, 2 + i * (SpeciesArt.CW + 2), 2 + r * (SpeciesArt.CH + 2));
            }
        sheet.SavePng(Path.Combine(dir, "choc.png"), 4, Bg);
    }
}
