namespace Pepin;

/// <summary>Objets que la tortue peut tenir, offrir, rapporter de visite (ids partagés avec le serveur).</summary>
public enum Item : byte { Fraise, FraiseDoree, Salade, Coquillage, Caillou, Fleur, Plume, Trefle, Gland, Bouton }

public static class Items
{
    public static readonly string[] Ids =
        ["fraise", "fraise-doree", "salade", "coquillage", "caillou", "fleur", "plume", "trefle", "gland", "bouton"];
    public static readonly string[] Labels =
        ["une fraise", "une fraise dorée", "une feuille de salade", "un coquillage", "un joli caillou",
         "une fleur", "une plume", "un trèfle", "un gland", "un bouton"];

    public static string Id(Item i) => Ids[(int)i];
    public static string Label(Item i) => Labels[(int)i];
    public static Item? Parse(string? id)
    {
        int k = id is null ? -1 : Array.IndexOf(Ids, id);
        return k < 0 ? null : (Item)k;
    }
    /// <summary>Tirage d'un cadeau : les objets rares (fraise dorée) le sont vraiment.</summary>
    public static Item Random(Random r)
    {
        if (r.NextDouble() < 0.05) return Item.FraiseDoree;
        Item i;
        do i = (Item)r.Next(0, Ids.Length); while (i == Item.FraiseDoree);
        return i;
    }
}

public sealed class JournalEntry
{
    public long T { get; set; }
    public string Text { get; set; } = "";
    public bool Private { get; set; }          // jamais envoyé au serveur
}

/// <summary>Mémoire longue de la tortue (persistée dans state.json).</summary>
public sealed class LifeData
{
    public double Bond { get; set; }
    public int Tier { get; set; }
    public Dictionary<string, int> Totals { get; set; } = [];
    public Dictionary<string, int> Today { get; set; } = [];
    public string TodayDate { get; set; } = "";
    public Dictionary<string, int> Collection { get; set; } = [];
    public List<JournalEntry> Journal { get; set; } = [];
    public long BornUnix { get; set; }

    // la bande
    public string? BandId { get; set; }
    public string? BandToken { get; set; }
    public string? Name { get; set; }
    public bool AcceptMessages { get; set; } = true;
    public bool SpontaneousVisits { get; set; } = true;
    public bool MiniGames { get; set; } = true;            // mini-jeux spontanés (bowling…)
    public long LastVisitUnix { get; set; }
}

/// <summary>Le lien avec son humain, les compteurs et le journal.</summary>
public sealed class Life
{
    public static readonly double[] TierAt = [0, 60, 250, 700, 1500];
    public static readonly string[] TierNames = ["Nouvelle tortue", "Copain", "Ami", "Meilleur ami", "Inséparable"];

    public readonly LifeData D;
    public int Tier => D.Tier;
    public int PendingLevelUp;                 // palier atteint, pas encore fêté (0 = rien)
    public bool Dirty;                         // le carnet a changé depuis le dernier envoi

    public Life(LifeData d)
    {
        D = d;
        if (D.BornUnix == 0)
        {
            D.BornUnix = Now;
            Write("A débarqué sur ton bureau.");
        }
        RollDay();
    }

    static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>À appeler au chargement : l'absence effrite un peu le lien.</summary>
    public void CatchUp(double hoursAway)
    {
        if (hoursAway > 24) D.Bond *= Math.Pow(0.97, hoursAway / 24);
        RecomputeTier(silent: true);
    }

    public void AddBond(double points)
    {
        D.Bond = Math.Max(0, D.Bond + points);
        RecomputeTier(silent: false);
    }

    void RecomputeTier(bool silent)
    {
        int t = 0;
        while (t < 4 && D.Bond >= TierAt[t + 1]) t++;
        // on ne redescend que franchement (évite de clignoter entre deux paliers)
        if (t < D.Tier && D.Bond > TierAt[D.Tier] * 0.8) return;
        if (t > D.Tier && !silent)
        {
            PendingLevelUp = t;
            Write($"Nouveau palier : {TierNames[t]} !");
        }
        if (t != D.Tier) Dirty = true;
        D.Tier = t;
    }

    public void Count(string stat, int n = 1)
    {
        RollDay();
        D.Totals[stat] = D.Totals.GetValueOrDefault(stat) + n;
        D.Today[stat] = D.Today.GetValueOrDefault(stat) + n;
        Dirty = true;
    }

    public void AddItem(Item item)
    {
        string id = Items.Id(item);
        D.Collection[id] = D.Collection.GetValueOrDefault(id) + 1;
        Dirty = true;
    }

    public void Write(string text, bool isPrivate = false)
    {
        D.Journal.Add(new JournalEntry { T = Now, Text = text, Private = isPrivate });
        if (D.Journal.Count > 200) D.Journal.RemoveRange(0, D.Journal.Count - 200);
        if (!isPrivate) Dirty = true;
    }

    /// <summary>Changement de jour : résumé de la veille dans le journal.</summary>
    public void RollDay()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (D.TodayDate == today) return;
        if (D.TodayDate != "" && D.Today.Count > 0)
        {
            var parts = new List<string>();
            void P(string k, string one, string many) { int n = D.Today.GetValueOrDefault(k); if (n > 0) parts.Add(n == 1 ? one : $"{n} {many}"); }
            P("snacks", "un goûter", "goûters");
            P("naps", "une sieste", "siestes");
            P("pets", "un câlin", "câlins");
            P("games", "une partie avec la souris", "parties avec la souris");
            P("throws", "un vol plané", "vols planés");
            if (parts.Count > 0) Write("Bilan de la journée : " + string.Join(", ", parts) + ".");
        }
        D.TodayDate = today;
        D.Today.Clear();
    }

    public string Summary()
    {
        int t = D.Tier;
        string next = t < 4 ? $" ({(int)(100 * (D.Bond - TierAt[t]) / (TierAt[t + 1] - TierAt[t]))} % vers {TierNames[t + 1].ToLowerInvariant()})" : "";
        return $"Lien : {TierNames[t]}{next}";
    }

    public string TodayLine()
    {
        int s = D.Today.GetValueOrDefault("snacks"), n = D.Today.GetValueOrDefault("naps"), p = D.Today.GetValueOrDefault("pets");
        return $"Aujourd'hui : {s} goûter{(s > 1 ? "s" : "")}, {n} sieste{(n > 1 ? "s" : "")}, {p} câlin{(p > 1 ? "s" : "")}";
    }
}
