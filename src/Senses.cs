namespace Pepin;

/// <summary>Ce que la tortue perçoit du monde : curseur, activité de l'utilisateur, écran.</summary>
public sealed unsafe class Senses
{
    public double CX, CY;              // curseur (pixels écran)
    public double Speed;               // vitesse lissée du curseur (px/s)
    public double IdleSeconds;         // inactivité clavier/souris de tout le système
    public RECT Work;                  // zone de travail de l'écran où est la tortue
    public bool CursorOnSameMonitor;

    // derniers échantillons, pour la vitesse de lancer
    readonly double[] hx = new double[8], hy = new double[8], ht = new double[8];
    int hi;
    bool first = true;

    public void Update(double now, double dt, double petX, double petY)
    {
        POINT p;
        Native.GetCursorPos(&p);
        if (!first && dt > 0)
        {
            double inst = Math.Sqrt((p.X - CX) * (p.X - CX) + (p.Y - CY) * (p.Y - CY)) / dt;
            double k = Math.Min(1, dt / 0.08);
            Speed += (inst - Speed) * k;
        }
        first = false;
        CX = p.X; CY = p.Y;
        hi = (hi + 1) % hx.Length;
        hx[hi] = CX; hy[hi] = CY; ht[hi] = now;

        LASTINPUTINFO lii = new() { cbSize = (uint)sizeof(LASTINPUTINFO) };
        if (Native.GetLastInputInfo(&lii) != 0)
            IdleSeconds = unchecked((uint)Environment.TickCount - lii.dwTime) / 1000.0;

        var petMon = Native.MonitorFromPoint(new POINT { X = (int)petX, Y = (int)petY }, Native.MONITOR_DEFAULTTONEAREST);
        var curMon = Native.MonitorFromPoint(p, Native.MONITOR_DEFAULTTONEAREST);
        CursorOnSameMonitor = petMon == curMon;
        MONITORINFO mi = new() { cbSize = (uint)sizeof(MONITORINFO) };
        if (Native.GetMonitorInfoW(petMon, &mi) != 0) Work = mi.rcWork;
    }

    /// <summary>Vitesse du curseur sur les ~100 dernières ms (pour lancer la tortue).</summary>
    public (double vx, double vy) RecentVelocity(double now)
    {
        int newest = hi, oldest = hi;
        for (int k = 1; k < hx.Length; k++)
        {
            int j = (hi - k + hx.Length) % hx.Length;
            if (ht[j] == 0 || now - ht[j] > 0.1) break;
            oldest = j;
        }
        double dt = ht[newest] - ht[oldest];
        if (dt < 0.01) return (0, 0);
        return ((hx[newest] - hx[oldest]) / dt, (hy[newest] - hy[oldest]) / dt);
    }
}
