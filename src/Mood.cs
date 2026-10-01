using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pepin;

public sealed class SaveData
{
    public double Energy { get; set; } = 0.8;
    public double Hunger { get; set; } = 0.3;
    public double Happiness { get; set; } = 0.7;
    public double Affection { get; set; } = 0.35;
    public long SavedAtUnix { get; set; }
    public int Scale { get; set; } = 3;
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public bool AutostartSet { get; set; }
    public LifeData Life { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(SaveData))]
internal partial class SaveJson : JsonSerializerContext { }

/// <summary>Jauges d'humeur (0..1), persistées entre les sessions.</summary>
public sealed class Mood
{
    public double Energy, Hunger, Happiness, Affection;
    public int Scale = 3;
    public double SavedX = double.NaN, SavedY = double.NaN;
    public bool AutostartSet;          // le démarrage auto a déjà été activé une première fois
    public LifeData LifeData = new();
    public double HoursAway;           // temps écoulé depuis la dernière sauvegarde

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pepin");
    static string FilePath => Path.Combine(Dir, "state.json");

    public static Mood Load()
    {
        SaveData d;
        try { d = File.Exists(FilePath) ? JsonSerializer.Deserialize(File.ReadAllText(FilePath), SaveJson.Default.SaveData) ?? new() : new(); }
        catch { d = new(); }

        var m = new Mood
        {
            Energy = d.Energy, Hunger = d.Hunger, Happiness = d.Happiness, Affection = d.Affection,
            Scale = Math.Clamp(d.Scale, 2, 4), SavedX = d.X, SavedY = d.Y, AutostartSet = d.AutostartSet,
            LifeData = d.Life ?? new(),
        };
        if (d.SavedAtUnix > 0)
        {
            // Pendant ton absence il a dormi : énergie pleine, un petit creux, et tu lui as un peu manqué.
            double hours = Math.Clamp((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - d.SavedAtUnix) / 3600.0, 0, 24 * 30);
            m.HoursAway = hours;
            if (hours > 0.25) m.Energy = 1;
            m.Hunger = Math.Min(0.85, m.Hunger + hours / 3);
            m.Happiness += (0.6 - m.Happiness) * Math.Min(1, hours / 12);
            m.Affection = Math.Max(0.1, m.Affection - 0.05 * hours / 24);
        }
        m.Clamp();
        return m;
    }

    public void Save(double x, double y)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var d = new SaveData
            {
                Energy = Energy, Hunger = Hunger, Happiness = Happiness, Affection = Affection,
                SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Scale = Scale, X = x, Y = y,
                AutostartSet = AutostartSet, Life = LifeData,
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(d, SaveJson.Default.SaveData));
        }
        catch { /* pas grave : on réessaiera à la prochaine sauvegarde */ }
    }

    /// <summary>Évolution naturelle des jauges.</summary>
    public void Tick(double dt, bool asleep, bool night)
    {
        if (asleep)
        {
            Energy += dt / (12 * 60);          // ~12 min pour une nuit complète
            Hunger += dt / (3 * 3600);
        }
        else
        {
            Energy -= dt / (90 * 60) * (night ? 1.6 : 1);   // paresseux : fatigué au bout d'1h30
            Hunger += dt / 3600;                              // gourmand : faim au bout d'une heure
        }
        Happiness += (0.6 - Happiness) * dt / 1800;           // revient doucement vers 0,6
        Affection -= dt / (7 * 24 * 3600);                    // s'effiloche très lentement
        Clamp();
    }

    public void Clamp()
    {
        Energy = Math.Clamp(Energy, 0, 1);
        Hunger = Math.Clamp(Hunger, 0, 1);
        Happiness = Math.Clamp(Happiness, 0, 1);
        Affection = Math.Clamp(Affection, 0, 1);
    }

    public string Describe()
    {
        static int P(double v) => (int)Math.Round(v * 100);
        return $"Énergie {P(Energy)} %   Faim {P(Hunger)} %   Humeur {P(Happiness)} %   Affection {P(Affection)} %";
    }
}
