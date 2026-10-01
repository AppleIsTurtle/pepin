using System.IO.Compression;

namespace Pepin;

/// <summary>Qui a dessiné un pixel : sert au masque du visage, à la teinte et au test "sur le corps".</summary>
public enum Layer : byte { None, Head, Shell, Leg, Outline, Fx, Shadow }

/// <summary>Petite grille de pixels ARGB prémultipliés (format natif d'un DIB 32 bits).</summary>
public sealed class PixelCanvas
{
    public readonly int W, H;
    public readonly uint[] Px;
    public readonly Layer[] L;

    public PixelCanvas(int w, int h)
    {
        W = w; H = h;
        Px = new uint[w * h];
        L = new Layer[w * h];
    }

    public static uint Rgb(int r, int g, int b) => 0xFF000000u | (uint)(r << 16) | (uint)(g << 8) | (uint)b;

    /// <summary>Couleur semi-transparente, stockée prémultipliée.</summary>
    public static uint Argb(int a, int r, int g, int b) =>
        (uint)(a << 24) | (uint)(r * a / 255 << 16) | (uint)(g * a / 255 << 8) | (uint)(b * a / 255);

    public static uint Lerp(uint a, uint b, float t)
    {
        if (t <= 0) return a;
        if (t >= 1) return b;
        int C(int sh) => (int)(((a >> sh) & 255) + (((int)((b >> sh) & 255) - (int)((a >> sh) & 255)) * t));
        return 0xFF000000u | (uint)(C(16) << 16) | (uint)(C(8) << 8) | (uint)C(0);
    }

    public void Clear()
    {
        Array.Clear(Px);
        Array.Clear(L);
    }

    public bool In(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;

    public void Set(int x, int y, uint c, Layer l)
    {
        if (!In(x, y)) return;
        Px[y * W + x] = c;
        L[y * W + x] = l;
    }

    public Layer LayerAt(int x, int y) => In(x, y) ? L[y * W + x] : Layer.None;

    /// <summary>Contour d'1 px autour de tout ce qui est corps (tête, carapace, pattes).</summary>
    public void Outline(uint c)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (L[y * W + x] != Layer.None) continue;
                if (IsBody(x + 1, y) || IsBody(x - 1, y) || IsBody(x, y + 1) || IsBody(x, y - 1))
                    Set(x, y, c, Layer.Outline);
            }
    }

    bool IsBody(int x, int y)
    {
        var l = LayerAt(x, y);
        return l is Layer.Head or Layer.Shell or Layer.Leg;
    }

    /// <summary>Pixel "solide" (corps ou contour) : ce que la souris peut toucher.</summary>
    public bool IsSolid(int x, int y)
    {
        var l = LayerAt(x, y);
        return l is Layer.Head or Layer.Shell or Layer.Leg or Layer.Outline;
    }

    /// <summary>Copie brute dans une autre toile (planche de dev).</summary>
    public void BlitTo(PixelCanvas dst, int ox, int oy)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                uint c = Px[y * W + x];
                if (c != 0) dst.Set(ox + x, oy + y, c, L[y * W + x]);
            }
    }

    // ---------- Export PNG (outil de dev --sheet) ----------

    public void SavePng(string path, int scale, uint background)
    {
        int w = W * scale, h = H * scale;
        var raw = new byte[h * (w * 4 + 1)];
        int i = 0;
        for (int y = 0; y < h; y++)
        {
            raw[i++] = 0; // filtre "none"
            for (int x = 0; x < w; x++)
            {
                uint c = Px[(y / scale) * W + x / scale];
                uint a = c >> 24;
                if (background == 0)
                {
                    // fond transparent : on dé-prémultiplie
                    uint d = Math.Max(a, 1);
                    raw[i++] = (byte)(((c >> 16) & 255) * 255 / d); raw[i++] = (byte)(((c >> 8) & 255) * 255 / d);
                    raw[i++] = (byte)((c & 255) * 255 / d); raw[i++] = (byte)a;
                    continue;
                }
                // compose sur le fond (couleurs prémultipliées)
                uint r = ((c >> 16) & 255) + (((background >> 16) & 255) * (255 - a) / 255);
                uint g = ((c >> 8) & 255) + (((background >> 8) & 255) * (255 - a) / 255);
                uint b = (c & 255) + ((background & 255) * (255 - a) / 255);
                raw[i++] = (byte)r; raw[i++] = (byte)g; raw[i++] = (byte)b; raw[i++] = 255;
            }
        }
        using var fs = File.Create(path);
        fs.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BeU32(ihdr, 0, (uint)w); BeU32(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 6; // 8 bits, RGBA
        Chunk(fs, "IHDR", ihdr);
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw);
            Chunk(fs, "IDAT", ms.ToArray());
        }
        Chunk(fs, "IEND", []);
    }

    static void BeU32(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BeU32(len, 0, (uint)data.Length);
        s.Write(len);
        var td = new byte[4 + data.Length];
        for (int k = 0; k < 4; k++) td[k] = (byte)type[k];
        data.CopyTo(td, 4);
        s.Write(td);
        var crc = new byte[4]; BeU32(crc, 0, Crc32(td));
        s.Write(crc);
    }

    static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte d in data)
        {
            crc ^= d;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
