using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pepin;

public sealed class VersionInfo
{
    public string Version { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VersionInfo))]
internal partial class UpdateJson : JsonSerializerContext { }

/// <summary>Réseau partagé (un seul HttpClient pour tout le programme).</summary>
public static class Net
{
    public static readonly string Base = Environment.GetEnvironmentVariable("PEPIN_SITE") ?? "https://pommetortue.tech/friend/";
    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    static Net() => Http.DefaultRequestHeaders.UserAgent.ParseAdd($"Pepin/{Updater.Current}");
}

/// <summary>
/// Mise à jour automatique : version.json → téléchargement → contrôle SHA-256 → l'exe en cours est renommé
/// en Pepin.old.exe (permis même en cours d'exécution), le nouveau prend sa place, puis on relance.
/// </summary>
public static class Updater
{
    public static Version Current => typeof(Updater).Assembly.GetName().Version ?? new Version(1, 0, 0);

    /// <summary>Seulement l'exe natif publié (pas `dotnet run`, qui tourne en JIT).</summary>
    public static bool Enabled => !RuntimeFeature.IsDynamicCodeSupported && Environment.ProcessPath is not null;

    static string Exe => Environment.ProcessPath!;
    static string OldExe => Path.Combine(Path.GetDirectoryName(Exe)!, "Pepin.old.exe");
    static string NewExe => Path.Combine(Path.GetDirectoryName(Exe)!, "Pepin.new.exe");

    public static void CleanupOld()
    {
        if (!Enabled) return;
        try { if (File.Exists(OldExe)) File.Delete(OldExe); } catch { }
        try { if (File.Exists(NewExe)) File.Delete(NewExe); } catch { }
    }

    /// <summary>Vérifie et télécharge une version plus récente. Renvoie le chemin du nouvel exe prêt, ou null.</summary>
    public static async Task<string?> CheckAsync()
    {
        if (!Enabled) return null;
        var json = await Net.Http.GetStringAsync(Net.Base + "version.json");
        var info = JsonSerializer.Deserialize(json, UpdateJson.Default.VersionInfo);
        if (info is null || !System.Version.TryParse(info.Version, out var v) || v <= Current) return null;
        if (!info.Url.StartsWith(Net.Base, StringComparison.Ordinal)) return null;   // jamais d'ailleurs

        var bytes = await Net.Http.GetByteArrayAsync(info.Url);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase)) return null;
        await File.WriteAllBytesAsync(NewExe, bytes);
        return NewExe;
    }

    /// <summary>Vérification en arrière-plan ; `ready` reçoit le chemin du nouvel exe (thread quelconque).</summary>
    public static void CheckInBackground(Action<string> ready) => Task.Run(async () =>
    {
        try { if (await CheckAsync() is string path) ready(path); }
        catch { /* pas de réseau : on réessaiera demain */ }
    });

    /// <summary>Remplace l'exe et relance. En cas d'échec, remet tout en place et renvoie false.</summary>
    public static bool ApplyAndRestart(string newExe)
    {
        try
        {
            if (File.Exists(OldExe)) File.Delete(OldExe);
            File.Move(Exe, OldExe);
            try { File.Move(newExe, Exe); }
            catch { File.Move(OldExe, Exe); throw; }
            Process.Start(new ProcessStartInfo(Exe, "--updated") { UseShellExecute = false });
            return true;
        }
        catch { return false; }
    }
}
