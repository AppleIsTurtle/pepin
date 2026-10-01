using System.Diagnostics;
using static Pepin.Native;

namespace Pepin;

/// <summary>Outil de dev (Pepin.exe --windows) : ce que la tortue voit des fenêtres et des applis. Sortie console, à rediriger vers un fichier.</summary>
public static unsafe class WinDebug
{
    public static int Run()
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        var world = new WindowWorld();
        world.Refresh(default);
        Console.WriteLine($"== Fenêtres ({world.Windows.Count}), de la plus haute à la plus basse");
        for (int i = 0; i < world.Windows.Count; i++)
        {
            var w = world.Windows[i];
            var r = w.R;
            Console.WriteLine($"{i,2} {w.Hwnd:X8} pid {w.Pid,6} {Proc(w.Pid),-22} \"{Title(w.Hwnd)}\"  [{r.Left},{r.Top} → {r.Right},{r.Bottom}] {r.Right - r.Left}x{r.Bottom - r.Top}"
                              + (w.Maximized ? " MAX" : "") + (w.Fullscreen ? " PLEIN-ÉCRAN" : ""));
        }

        MONITORINFO mi = new() { cbSize = (uint)sizeof(MONITORINFO) };
        GetMonitorInfoW(MonitorFromPoint(default, MONITOR_DEFAULTTOPRIMARY), &mi);
        var work = mi.rcWork;
        Console.WriteLine();
        Console.WriteLine($"== Perches (écran principal, zone de travail [{work.Left},{work.Top} → {work.Right},{work.Bottom}], largeur ≥ 90)");
        foreach (var p in world.Perches(work, 90))
            Console.WriteLine($"   {p.Hwnd:X8} {ProcOfWindow(p.Hwnd),-22} x {p.X0}..{p.X1} ({p.X1 - p.X0} px) à y = {p.Y}");

        POINT c;
        GetCursorPos(&c);
        nint under = world.TopmostAt(c.X, c.Y);
        Console.WriteLine();
        Console.WriteLine($"== Sous le curseur ({c.X},{c.Y}) : {(under == 0 ? "bureau" : $"{under:X8} {ProcOfWindow(under)}")}");
        if (world.Windows.Count > 1)
        {
            var low = world.Windows[^1];
            int cx = (low.R.Left + low.R.Right) / 2, cy = (low.R.Top + low.R.Bottom) / 2;
            Console.WriteLine($"   centre de la plus basse ({ProcOfWindow(low.Hwnd)}) recouvert : {world.IsCovered(cx, cy, low.Hwnd)}");
        }

        // coût d'un Refresh + Perches (doit être ~0 octet alloué en régime établi)
        const int N = 200;
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) { world.Refresh(default); world.Perches(work, 90); }
        sw.Stop();
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Console.WriteLine($"== Refresh+Perches : {sw.Elapsed.TotalMilliseconds / N:F3} ms/appel, {bytes / N} octets alloués/appel");

        Console.WriteLine();
        Console.WriteLine("== AppWatch pendant 12 s");
        var watch = new AppWatch();
        var clock = Stopwatch.StartNew();
        double nextPrint = 1;
        long updBytes = 0; int updCalls = 0;
        while (clock.Elapsed.TotalSeconds < 12)
        {
            double now = clock.Elapsed.TotalSeconds;
            LASTINPUTINFO lii = new() { cbSize = (uint)sizeof(LASTINPUTINFO) };
            double idle = GetLastInputInfo(&lii) != 0 ? unchecked((uint)Environment.TickCount - lii.dwTime) / 1000.0 : 999;
            GetCursorPos(&c);
            long b0 = GC.GetAllocatedBytesForCurrentThread();
            watch.Update(now, idle, c.X, c.Y);
            updBytes += GC.GetAllocatedBytesForCurrentThread() - b0; updCalls++;
            if (now >= nextPrint)
            {
                nextPrint += 1;
                var fr = watch.ForegroundRect;
                Console.WriteLine($"{now,5:F1}s  {watch.Foreground,-8} {watch.ForegroundProcess,-22} [{fr.Left},{fr.Top} → {fr.Right},{fr.Bottom}]  rendu {(watch.Rendering ? watch.RenderApp : "non")}  frappe {watch.TypingStreak:F1} s  (inactif {idle:F1} s)");
            }
            Thread.Sleep(50);
        }
        Console.WriteLine($"== Update : {updCalls} appels, {updBytes / Math.Max(1, updCalls)} octets alloués/appel en moyenne (échantillonnages inclus)");
        return 0;
    }

    static string Proc(uint pid) => AppWatch.QueryProcessName(pid) ?? "?";

    static string ProcOfWindow(nint h)
    {
        uint pid;
        GetWindowThreadProcessId(h, &pid);
        return Proc(pid);
    }

    static string Title(nint h)
    {
        char* buf = stackalloc char[256];
        int n = Math.Max(0, GetWindowTextW(h, buf, 256));
        var s = new string(buf, 0, n);
        return s.Length > 40 ? s[..40] + "…" : s;
    }
}
