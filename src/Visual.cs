namespace Pepin;

public enum Eyes : byte { Normal, Closed, Happy, HalfLid, Angry, Wide, Spiral, Determined, Squint, Hearts }

public enum Mouth : byte { None, Smile, Grin, Yawn, Oh, Zigzag, Frown, Pout, ChewA, ChewB, Bite, Tongue }

public enum FxKind : byte
{
    Zzz, Bubble, Hearts, Anger, Steam, Exclaim, Question, Notes, Sweat, Stars,
    Food, Crumbs, Dream, Grumble, Dust, Speed, Sparkles, Tear,
}

public enum Snack : byte { Lettuce, Strawberry, Heart }

public struct Effect
{
    public FxKind Kind;
    public float Phase;   // 0..1, anime l'effet (monte, tourne…)
    public float Value;   // sens propre à l'effet (taille, quantité, reste…)
}

/// <summary>Tout ce qu'il faut pour dessiner une image de la tortue. Réécrit à chaque tick.</summary>
public sealed class Visual
{
    // --- pose ---
    public bool FacingRight = true;
    public float HeadOut = 1;      // 1 = sortie, 0 = rentrée dans la carapace
    public int HeadDx, HeadDy;
    public int LegPhase;           // 0..3 : cycle de marche
    public int LegsTuck;           // 0..3 : pattes repliées (corps posé)
    public bool LegsDangle;        // pattes pendantes (tenu en l'air)
    public int BodyDx, BodyDy;
    public bool InShell;           // tête et pattes rentrées
    public int SpinFrame;          // 0..3 : rotation de la carapace
    public int ShadowZ;            // hauteur en pixels logiques (ombre décalée)
    public bool NoShadow;

    // --- visage ---
    public Eyes Eyes;
    public Mouth Mouth;
    public int LookX, LookY;       // -1..1
    public bool Blink;
    public bool Blush;
    public int AnimFrame;          // petite horloge pour les yeux en spirale etc.

    // --- humeur ---
    public uint Tint;
    public float TintAmount;
    public float Flush;            // 0..1 : le rouge de la colère monte depuis le haut du crâne

    // --- effets ---
    public readonly Effect[] Fx = new Effect[8];
    public int FxCount;
    public Item Food;
    public Snack DreamOf;

    public void Reset()
    {
        HeadOut = 1; HeadDx = HeadDy = 0; LegPhase = 0; LegsTuck = 0; LegsDangle = false;
        BodyDx = BodyDy = 0; InShell = false; SpinFrame = 0; ShadowZ = 0; NoShadow = false;
        Eyes = Eyes.Normal; Mouth = Mouth.Smile; LookX = LookY = 0; Blink = false; Blush = false; Flush = 0;
        FxCount = 0;
    }

    public void Add(FxKind kind, float phase = 0, float value = 1)
    {
        if (FxCount >= Fx.Length) return;
        Fx[FxCount++] = new Effect { Kind = kind, Phase = phase - MathF.Floor(phase), Value = value };
    }

    public bool Has(FxKind kind)
    {
        for (int i = 0; i < FxCount; i++) if (Fx[i].Kind == kind) return true;
        return false;
    }
}
