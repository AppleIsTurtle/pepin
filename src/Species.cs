namespace Pepin;

/// <summary>Les animaux qui peuvent sortir de l'œuf (une chance sur six chacun).</summary>
public enum Species : byte { Tortue, Herisson, Grenouille, Escargot, PandaRoux, Axolotl }

/// <summary>Comment l'animal fait un long trajet (comportement <c>Travel</c>).</summary>
public enum TravelMode : byte { Spin, Roll, Hop, Glide, Swim }

/// <summary>Réglages propres à une espèce (dessin mis à part).</summary>
public sealed class SpeciesTraits
{
    public required Species Kind;
    public required string Code;          // identifiant stable (état, serveur, images du site)
    public required string Name;          // « tortue »
    public required bool Feminine;
    public required bool Elided;          // l'escargot, l'axolotl
    public double WalkSpeed = 1;          // facteur sur la vitesse de marche
    public double PerchWeight = 1;        // facteur sur l'envie de se percher sur les fenêtres
    public int HeadX = 11, HeadY = 14;    // position de la tête par rapport aux pieds, en pixels logiques
    public TravelMode Travel = TravelMode.Spin;

    /// <summary>« la tortue », « le hérisson », « l'escargot ».</summary>
    public string The => Elided ? "l'" + Name : (Feminine ? "la " : "le ") + Name;

    /// <summary>« une tortue », « un hérisson ».</summary>
    public string A => (Feminine ? "une " : "un ") + Name;
}

public static class SpeciesInfo
{
    public static readonly Species[] All =
        [Species.Tortue, Species.Herisson, Species.Grenouille, Species.Escargot, Species.PandaRoux, Species.Axolotl];

    static readonly SpeciesTraits[] table =
    [
        new() { Kind = Species.Tortue, Code = "tortue", Name = "tortue", Feminine = true, Elided = false },
        new() { Kind = Species.Herisson, Code = "herisson", Name = "hérisson", Feminine = false, Elided = false,
                WalkSpeed = 1.1, Travel = TravelMode.Roll, HeadX = 8, HeadY = 12 },
        new() { Kind = Species.Grenouille, Code = "grenouille", Name = "grenouille", Feminine = true, Elided = false,
                Travel = TravelMode.Hop, HeadX = 5, HeadY = 21 },
        new() { Kind = Species.Escargot, Code = "escargot", Name = "escargot", Feminine = false, Elided = true,
                WalkSpeed = 0.4, Travel = TravelMode.Glide, HeadX = 9, HeadY = 22 },
        new() { Kind = Species.PandaRoux, Code = "panda-roux", Name = "panda roux", Feminine = false, Elided = false,
                PerchWeight = 2, Travel = TravelMode.Roll, HeadX = 7, HeadY = 14 },
        new() { Kind = Species.Axolotl, Code = "axolotl", Name = "axolotl", Feminine = false, Elided = true,
                Travel = TravelMode.Swim, HeadX = 6, HeadY = 14 },
    ];

    public static SpeciesTraits Of(Species s) => table[(int)s];

    public static string Code(Species s) => table[(int)s].Code;

    /// <summary>Code inconnu ou absent → tortue (les états et clients d'avant la v3).</summary>
    public static Species Parse(string? code)
    {
        if (code is not null)
            foreach (var t in table)
                if (t.Code == code) return t.Kind;
        return Species.Tortue;
    }

    public static bool IsKnown(string? code)
    {
        if (code is null) return false;
        foreach (var t in table) if (t.Code == code) return true;
        return false;
    }
}
