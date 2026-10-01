namespace Pepin;

static class Program
{
    static Mutex? single;   // une seule tortue à la fois (gardé vivant toute la session)

    static int Main(string[] args)
    {
        string a0 = args.Length >= 1 ? args[0] : "";
        if (a0 == "--sheet") { SheetRenderer.Render(args.Length >= 2 ? args[1] : "sheet.png"); return 0; }
        if (a0 == "--frames") { SheetRenderer.Frames(args.Length >= 2 ? args[1] : "frames"); return 0; }
        if (a0 == "--windows") return WinDebug.Run();
        if (a0 == "--bandtest") return Simulator.BandTest(args.Length >= 2 ? args[1] : null);
        if (a0 == "--simulate")
            return Simulator.Run(args.Length >= 2 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 3);

        // pixels physiques sur chaque écran : pixel art net, coordonnées exactes
        Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

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
