using System.Runtime.InteropServices;
using static Pepin.Native;

namespace Pepin;

public enum AppActivity { None, Video, Creative }

/// <summary>
/// Ce que fait l'utilisateur, deviné sans rien espionner : catégorie de l'appli au premier plan,
/// rendu 3D en cours, frappe clavier soutenue (sans hook clavier).
/// Tout reste en mémoire : rien n'est écrit sur disque ni envoyé sur le réseau.
/// </summary>
public sealed unsafe class AppWatch
{
    public AppActivity Foreground;       // catégorie de la fenêtre au premier plan
    public nint ForegroundHwnd;
    public RECT ForegroundRect;
    public string ForegroundProcess = ""; // nom d'exe en minuscules, sans chemin ni ".exe"
    public bool Rendering;                // un rendu 3D semble en cours
    public string? RenderApp;             // "houdini", "blender", "maya", "after effects", "cinema 4d"…
    public double TypingStreak;           // secondes de frappe clavier soutenue

    const double ForegroundPeriod = 2, RenderPeriod = 5;
    const double RenderWindow = 10, RenderRelease = 15;
    const double CreativeCpu = 0.45, HythonCpu = 0.25;

    double lastForeground = double.NegativeInfinity, lastRender = double.NegativeInfinity;

    // ------------------------------------------------------------------ premier plan

    readonly char[] title = new char[256];
    readonly Dictionary<uint, string> names = new();   // pid → nom de processus

    void UpdateForeground()
    {
        nint h = GetForegroundWindow();
        ForegroundHwnd = h;
        if (h == 0 || !VisibleBounds(h, out ForegroundRect))
        {
            Foreground = AppActivity.None;
            ForegroundProcess = "";
            ForegroundRect = default;
            return;
        }
        uint pid;
        GetWindowThreadProcessId(h, &pid);
        string name = NameOf(pid);
        // applis UWP (Films et TV, Netflix…) : le cadre appartient à ApplicationFrameHost, le vrai processus est dans une fenêtre enfant
        if (name == "applicationframehost")
        {
            uint inner = UwpProcess(h, pid);
            if (inner != 0) name = NameOf(inner);
        }
        ForegroundProcess = name;

        int n;
        fixed (char* t = title) n = Math.Max(0, GetWindowTextW(h, t, title.Length));
        Foreground = Classify(name, new ReadOnlySpan<char>(title, 0, n));
    }

    string NameOf(uint pid)
    {
        if (names.TryGetValue(pid, out var s)) return s;
        s = QueryProcessName(pid) ?? "";
        if (s.Length > 0) names[pid] = s;
        return s;
    }

    /// <summary>Nom d'exe en minuscules, sans chemin ni extension (null si inaccessible).</summary>
    internal static string? QueryProcessName(uint pid)
    {
        nint p = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid);
        if (p == 0) return null;
        try
        {
            char* buf = stackalloc char[1024];
            uint size = 1024;
            if (QueryFullProcessImageNameW(p, 0, buf, &size) == 0) return null;
            return ExeName(new ReadOnlySpan<char>(buf, (int)size));
        }
        finally { CloseHandle(p); }
    }

    static string ExeName(ReadOnlySpan<char> path)
    {
        int slash = path.LastIndexOfAny('\\', '/');
        var file = path[(slash + 1)..];
        if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) file = file[..^4];
        return file.ToString().ToLowerInvariant();
    }

    static uint uwpHost, uwpFound;

    static uint UwpProcess(nint frame, uint hostPid)
    {
        uwpHost = hostPid;
        uwpFound = 0;
        EnumChildWindows(frame, &FindUwpChild, 0);
        return uwpFound;
    }

    [UnmanagedCallersOnly]
    static int FindUwpChild(nint h, nint _)
    {
        uint pid;
        GetWindowThreadProcessId(h, &pid);
        if (pid == uwpHost) return 1;
        uwpFound = pid;
        return 0;
    }

    static AppActivity Classify(string exe, ReadOnlySpan<char> title)
    {
        if (IsCreative(exe)) return AppActivity.Creative;
        switch (exe)
        {
            case "vlc" or "mpc-hc64" or "mpc-hc" or "mpc-be64" or "mpv" or "potplayermini64" or "wmplayer"
                or "video.ui" or "microsoft.media.player" or "netflix":
                return AppActivity.Video;
            case "chrome" or "msedge" or "firefox" or "opera" or "brave" or "vivaldi" or "arc":
                return IsVideoTitle(title) ? AppActivity.Video : AppActivity.None;
        }
        return AppActivity.None;
    }

    static bool IsVideoTitle(ReadOnlySpan<char> t)
    {
        const StringComparison ic = StringComparison.OrdinalIgnoreCase;
        return t.Contains("YouTube", ic) || t.Contains("Twitch", ic) || t.Contains("Netflix", ic) || t.Contains("Prime Video", ic)
            || t.Contains("Disney+", ic) || t.Contains("Crunchyroll", ic) || t.Contains("Dailymotion", ic) || t.Contains("Vimeo", ic)
            || ContainsWord(t, "ADN");
    }

    /// <summary>Mot entier (voisins non alphanumériques), insensible à la casse.</summary>
    static bool ContainsWord(ReadOnlySpan<char> t, string word)
    {
        int from = 0;
        while (from < t.Length)
        {
            int i = t[from..].IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            i += from;
            int end = i + word.Length;
            if ((i == 0 || !char.IsLetterOrDigit(t[i - 1])) && (end == t.Length || !char.IsLetterOrDigit(t[end]))) return true;
            from = i + 1;
        }
        return false;
    }

    static bool IsCreative(string exe) =>
        exe.StartsWith("houdini", StringComparison.Ordinal) || exe.StartsWith("nuke", StringComparison.Ordinal) || exe switch
        {
            "hindie" or "happrentice" or "blender" or "maya" or "3dsmax" or "cinema 4d" or "afterfx" or "photoshop"
                or "adobe substance 3d painter" or "substance painter" or "zbrush" or "krita" or "clipstudiopaint" => true,
            _ => false,
        };

    // ------------------------------------------------------------------ rendu 3D

    sealed class Tracked
    {
        public nint Handle;
        public string App = "";
        public bool Seen;
        public double Threshold;                           // part du CPU total au-delà de laquelle on parle de rendu
        public readonly long[] T = new long[4], Cpu = new long[4];   // derniers échantillons (heure système, temps CPU), 100 ns
        public int Count;
    }

    readonly Dictionary<uint, Tracked> tracked = new();
    readonly List<uint> gone = new();
    readonly HashSet<uint> alive = new();
    double lastAboveThreshold = double.NegativeInfinity;

    void SampleRendering(double now)
    {
        nint snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == INVALID_HANDLE_VALUE || snap == 0) return;

        long sysNow;
        GetSystemTimeAsFileTime(&sysNow);
        foreach (var t in tracked.Values) t.Seen = false;
        alive.Clear();

        string? found = null;            // processus de rendu dédié
        string? busyApp = null;          // appli créative au-dessus de son seuil
        double busyCpu = 0;
        char* low = stackalloc char[260];

        PROCESSENTRY32W pe = new() { dwSize = (uint)sizeof(PROCESSENTRY32W) };
        try
        {
            for (int ok = Process32FirstW(snap, &pe); ok != 0; ok = Process32NextW(snap, &pe))
            {
                alive.Add(pe.th32ProcessID);
                var exe = LowerExe(pe.szExeFile, low);
                string? dedicated = DedicatedRenderer(exe);
                if (dedicated != null) { found ??= dedicated; continue; }

                string? app; double threshold;
                if (exe is "hython") { app = "houdini"; threshold = HythonCpu; }
                else if ((app = CpuWatchedApp(exe)) != null) threshold = CreativeCpu;
                else continue;

                uint pid = pe.th32ProcessID;
                if (!tracked.TryGetValue(pid, out var tr))
                {
                    nint h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid);
                    if (h == 0) continue;
                    tracked[pid] = tr = new Tracked { Handle = h, App = app, Threshold = threshold };
                }
                tr.Seen = true;
                double cpu = CpuShare(tr, sysNow);
                if (cpu > tr.Threshold && cpu > busyCpu) { busyCpu = cpu; busyApp = tr.App; }
            }
        }
        finally { CloseHandle(snap); }

        // processus disparus : on rend les handles (et on oublie leurs noms, un pid peut être réutilisé)
        gone.Clear();
        foreach (var kv in tracked) if (!kv.Value.Seen) gone.Add(kv.Key);
        foreach (var pid in gone) { CloseHandle(tracked[pid].Handle); tracked.Remove(pid); }
        gone.Clear();
        foreach (var pid in names.Keys) if (!alive.Contains(pid)) gone.Add(pid);
        foreach (var pid in gone) names.Remove(pid);

        string? active = found ?? busyApp;
        if (active != null)
        {
            Rendering = true;
            RenderApp = active;
            lastAboveThreshold = now;
        }
        else if (Rendering && now - lastAboveThreshold >= RenderRelease)
        {
            Rendering = false;
            RenderApp = null;
        }
    }

    /// <summary>Part moyenne du CPU total (tous cœurs) sur la dernière fenêtre de ~10 s ; -1 tant qu'il manque un échantillon.</summary>
    static double CpuShare(Tracked tr, long sysNow)
    {
        long creation, exit, kernel, user;
        if (GetProcessTimes(tr.Handle, &creation, &exit, &kernel, &user) == 0) return -1;
        int n = tr.T.Length;
        if (tr.Count == n)
        {
            Array.Copy(tr.T, 1, tr.T, 0, n - 1);
            Array.Copy(tr.Cpu, 1, tr.Cpu, 0, n - 1);
            tr.Count--;
        }
        tr.T[tr.Count] = sysNow;
        tr.Cpu[tr.Count] = kernel + user;
        tr.Count++;

        // le plus ancien échantillon qui reste dans la fenêtre (avec un peu de marge pour la gigue du minuteur)
        int last = tr.Count - 1, first = last;
        for (int k = last - 1; k >= 0 && sysNow - tr.T[k] <= (long)((RenderWindow + 1) * 1e7); k--) first = k;
        if (first == last) return -1;
        double wall = (tr.T[last] - tr.T[first]) * Environment.ProcessorCount;
        return wall > 0 ? (tr.Cpu[last] - tr.Cpu[first]) / wall : -1;
    }

    static ReadOnlySpan<char> LowerExe(char* name, char* dst)
    {
        int n = 0;
        while (n < 260 && name[n] != 0) { dst[n] = char.ToLowerInvariant(name[n]); n++; }
        var s = new ReadOnlySpan<char>(dst, n);
        return s.EndsWith(".exe") ? s[..^4] : s;
    }

    static string? DedicatedRenderer(ReadOnlySpan<char> exe) => exe switch
    {
        "husk" or "mantra" or "karma" or "hbatch" => "houdini",
        "aerender" => "after effects",
        "render" => "maya",
        "commandlinerender" => "cinema 4d",
        _ => null,
    };

    static string? CpuWatchedApp(ReadOnlySpan<char> exe)
    {
        if (exe.StartsWith("houdini") || exe is "hindie" or "happrentice") return "houdini";
        return exe switch
        {
            "blender" => "blender",
            "maya" => "maya",
            "3dsmax" => "3ds max",
            "cinema 4d" => "cinema 4d",
            "afterfx" => "after effects",
            _ => null,
        };
    }


    // ------------------------------------------------------------------ frappe clavier

    double lastUpdate = double.NaN;
    double lastCx = double.NaN, lastCy;
    double lastCursorMove = double.NegativeInfinity, lastKey = double.NegativeInfinity;
    readonly double[] moveT = new double[256], moveD = new double[256];   // déplacements du curseur sur les 2 dernières s
    int moveHead, moveCount;
    double moveSum;

    void UpdateTyping(double now, double idleSeconds, double cx, double cy)
    {
        if (double.IsNaN(lastUpdate)) lastCursorMove = now;   // au départ, on ne sait pas depuis quand la souris est immobile
        double dt = double.IsNaN(lastUpdate) ? 0 : Math.Clamp(now - lastUpdate, 0, 0.5);
        lastUpdate = now;

        if (!double.IsNaN(lastCx))
        {
            double d = Math.Abs(cx - lastCx) + Math.Abs(cy - lastCy);
            if (d > 0.5)
            {
                lastCursorMove = now;
                if (moveCount == moveT.Length) { moveSum -= moveD[moveHead]; moveHead = (moveHead + 1) % moveT.Length; moveCount--; }
                int i = (moveHead + moveCount) % moveT.Length;
                moveT[i] = now; moveD[i] = d; moveCount++;
                moveSum += d;
            }
        }
        lastCx = cx; lastCy = cy;
        while (moveCount > 0 && now - moveT[moveHead] > 2)
        {
            moveSum -= moveD[moveHead];
            moveHead = (moveHead + 1) % moveT.Length;
            moveCount--;
        }
        if (moveCount == 0) moveSum = 0;   // évite la dérive des arrondis

        // une entrée récente alors que la souris est immobile depuis 3 s : c'est le clavier
        if (idleSeconds < 1.5 && now - lastCursorMove >= 3) lastKey = Math.Max(lastKey, now - idleSeconds);

        if (moveSum > 200 || now - lastKey > 20) TypingStreak = 0;
        else if (now - lastKey <= 6) TypingStreak += dt;
    }

    // ------------------------------------------------------------------ tick

    /// <summary>Appelé à chaque tick (6 à 60 fois/s) : quasi gratuit, les observations coûteuses sont espacées en interne.</summary>
    public void Update(double now, double idleSeconds, double cursorX, double cursorY)
    {
        UpdateTyping(now, idleSeconds, cursorX, cursorY);
        if (now - lastForeground >= ForegroundPeriod) { lastForeground = now; UpdateForeground(); }
        if (now - lastRender >= RenderPeriod) { lastRender = now; SampleRendering(now); }
    }
}
