namespace Pepin;

static class Program
{
    static Mutex? single;   // une seule tortue à la fois (gardé vivant toute la session)

    static int Main(string[] args)
    {
        string a0 = args.Length >= 1 ? args[0] : "";
        // outils de dev : espèce forcée (`--species herisson`, `--guest grenouille`)
        Species? forced = null;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--species") forced = Simulator.Species = SpeciesInfo.Parse(args[i + 1]);
            if (args[i] == "--guest") Simulator.Guest = SpeciesInfo.Parse(args[i + 1]);
        }
        if (a0 == "--sheet-all") { SheetRenderer.RenderAll(args.Length >= 2 ? args[1] : "sheets"); return 0; }
        if (a0 == "--sheet") { SheetRenderer.Render(args.Length >= 2 ? args[1] : "sheet.png", forced ?? (args.Length >= 3 ? SpeciesInfo.Parse(args[2]) : Species.Tortue)); return 0; }
        if (a0 == "--frames") { SheetRenderer.Frames(args.Length >= 2 ? args[1] : "frames"); return 0; }
        if (a0 == "--windows") return WinDebug.Run();
        if (a0 == "--preview") return Preview.Run(args.Length >= 2 ? args[1] : "preview");
        if (a0 == "--bandtest") return Simulator.BandTest(args.Length >= 2 ? args[1] : null);
        if (a0 == "--simulate")
            return Simulator.Run(args.Length >= 2 && double.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h) ? h : 3);

        // pixels physiques sur chaque écran : pixel art net, coordonnées exactes
        Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        // `--egg` : joue l'œuf et l'éclosion sans toucher à l'état ni à la bande (test, à côté du vrai Pépin)
        if (a0 == "--egg")
        {
            single = new Mutex(true, @"Local\PepinOeufDeTest", out bool alone);
            if (!alone) return 0;
            return new App(updated: false, eggTest: true, forced: forced) { AutoTap = args.Contains("auto") }.Run();
        }

        // après une mise à jour, l'ancienne version met un instant à se fermer : on l'attend
        bool updated = a0 == "--updated";
        for (int i = 0; ; i++)
        {
            single = new Mutex(true, @"Local\PepinCompagnonDeBureau", out bool created);
            if (created) break;
            single.Dispose();
            if (!updated || i >= 50) return 0;
            Thread.Sleep(200);
        }

        return new App(updated).Run();
    }
}
