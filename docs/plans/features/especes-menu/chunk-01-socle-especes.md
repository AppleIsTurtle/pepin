# Chunk 01 — Socle des espèces

**Statut** : done (2026-10-01) — planche, sprites du site et aperçus carte/herbe identiques au pixel près ; `--simulate 2` sans erreur — Parent : [plan.md](plan.md)

## But
Rendre le code « multi-espèces » sans rien changer à l'écran : la tortue doit sortir **pixel pour pixel identique**.
Après ce chunk, ajouter une espèce = un fichier de dessin + quelques poids et comportements.

## Fichiers
- `src/Species.cs` (nouveau) — `enum Species { Tortue, Herisson, Grenouille, Escargot, PandaRoux, Axolotl }`,
  conversion chaîne ↔ enum (`tortue`, `herisson`, `grenouille`, `escargot`, `panda-roux`, `axolotl`), noms affichés
  avec article et genre (« la tortue », « le hérisson », « l'axolotl »…), réglages par espèce (`SpeciesTraits` :
  facteur de vitesse de marche, poids des perches, ancre de la tête, mode du déplacement long (`Travel`) = toupie / roulé / bonds /
  glissade / flotte).
- `src/ArtKit.cs` (nouveau) — extrait de `TurtleArt` tout ce qui n'est pas la tortue : `Face`, `AngryBrows`,
  `Effects`, `Bubble`, `Crumb`, `FoodHeld`, `Shadow`, helpers `MX/MY/B/Rect/F/FRect` paramétrés par l'ancre et le sens.
- `src/TurtleArt.cs` — ne garde que le corps (pattes, tête, carapace, icône) et appelle `ArtKit`.
- `src/SpeciesArt.cs` (nouveau) — `Draw(PixelCanvas, Visual, Species)` et `DrawIcon(PixelCanvas, Species)` qui délèguent ;
  tant qu'une espèce n'a pas son fichier, elle retombe sur la tortue.
- `src/Visual.cs` — commentaires : sens générique de `InShell` (repli), `SpinFrame` (roulé/rotation), `HeadOut` ;
  nouveaux champs génériques si besoin des chunks suivants (`Puffed` pour gorge/piquants, `Stand` pour dressé)
  — ajoutés ici, ignorés par la tortue.
- `src/Life.cs` — `LifeData.Species` (string?, absent = tortue) + propriété `Species` typée.
- `src/Creature.cs`, `src/App.cs` (icône), `src/SheetRenderer.cs`, `src/Simulator.cs`, `src/Pet.cs`,
  `src/Behaviors*.cs` — remplacer les appels `TurtleArt.*` par `SpeciesArt.*` avec l'espèce de la créature
  (prévoir `Creature.Species` : la créature visiteuse aura sa propre espèce au chunk 09).
- `src/Program.cs` — `--sheet out.png [espece]` ; `--sheet-all dossier` (une planche par espèce).
- Textes du client : relever les « tortue » codés en dur (`grep -rn "tortue" src`) et passer par le nom d'espèce
  quand ils parlent de **notre** animal (bulles, carnet, journal). Laisser « la bande » et « Pépin ».

## Vérification
1. Avant toute modification : `--sheet avant.png`.
2. Après : `--sheet apres.png` → comparer les pixels (petit script) : **identiques**.
3. `--simulate 3` tourne sans erreur.
4. `build.ps1` passe (NativeAOT : pas de réflexion, pas d'`Enum.Parse` → table explicite).

## Points d'attention
- `TurtleArt` utilise des champs statiques (`c`, `v`, `dy`, ancres `AX/AY/SX/SY`) : `ArtKit` doit recevoir ces
  repères via un petit contexte statique réinitialisé à chaque `Draw` — pas d'allocation par image.
- Position de la tête : `Pet.Automatisms` (suivi des yeux) et `Effects` supposent la tête à (+11, -14) ; la remplacer
  par l'ancre de tête de `SpeciesTraits`.
- Ne pas toucher aux tailles de fenêtre ni au test de clic (grille identique pour toutes les espèces).
