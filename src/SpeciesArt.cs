namespace Pepin;

/// <summary>Point d'entrée du dessin : choisit le dessinateur de l'espèce.</summary>
public static class SpeciesArt
{
    public const int CW = ArtKit.CW, CH = ArtKit.CH, AX = ArtKit.AX, AY = ArtKit.AY;

    public static void Draw(PixelCanvas canvas, Visual vis, Species s)
    {
        switch (s)
        {
            case Species.Herisson: HerissonArt.Draw(canvas, vis); break;
            case Species.Grenouille: GrenouilleArt.Draw(canvas, vis); break;
            case Species.Escargot: EscargotArt.Draw(canvas, vis); break;
            case Species.PandaRoux: PandaRouxArt.Draw(canvas, vis); break;
            case Species.Axolotl: AxolotlArt.Draw(canvas, vis); break;
            default: TurtleArt.Draw(canvas, vis); break;
        }
    }

    public static void DrawIcon(PixelCanvas ic, Species s)
    {
        switch (s)
        {
            case Species.Herisson: HerissonArt.DrawIcon(ic); break;
            case Species.Grenouille: GrenouilleArt.DrawIcon(ic); break;
            case Species.Escargot: EscargotArt.DrawIcon(ic); break;
            case Species.PandaRoux: PandaRouxArt.DrawIcon(ic); break;
            case Species.Axolotl: AxolotlArt.DrawIcon(ic); break;
            default: TurtleArt.DrawIcon(ic); break;
        }
    }
}
