# Chunk 02 — Sprites pixel art

**Statut** : done — Parent : [MASTER.md](MASTER.md)

## Fichiers
- `src/PixelCanvas.cs` — grille ARGB prémultipliée + calque (tête/carapace/pattes), primitives, contour, export PNG (dev)
- `src/Glyphs.cs` — petits motifs en chaînes (cœur, colère, Z, !, ?, note, étoile, goutte, salade, fraise…)
- `src/Visual.cs` — description d'une image : pose, visage, effets, teinte
- `src/TurtleArt.cs` — dessine la tortue d'après un `Visual`
- `src/SheetRenderer.cs` — `Pepin.exe --sheet out.png` : planche de toutes les poses pour vérification

## Fonctionnalités
- Pose : sortie de tête (0 = rentrée), décalages tête, 4 phases de marche, pattes repliées/écartées,
  rebond du corps, repli complet + 4 images de rotation de carapace
- Yeux : normal (+ regard ±1 px), clignement, fermés, heureux ^^, mi-clos, colère, écarquillés,
  spirale, déterminés, plissés > <, cœurs
- Bouche : aucune, sourire, grand sourire, bâillement, o, zigzag, moue, mâche (2 images), morsure, langue, ondulée
- Effets : Zzz, bulle de nez, cœurs, marque de colère, vapeur, !, ?, notes, sueur, étoiles tournantes,
  nourriture tenue (croquée progressivement), miettes, bulle de rêve, nuage « hmph », poussière, traits de vitesse
- Teinte de la tête/pattes selon l'humeur

## Points d'attention
- Le visage ne se dessine que sur les pixels « tête » visibles (calque) → la tête rentrée cache le visage
- Miroir horizontal pour la direction, sauf les glyphes (un Z ne doit pas être inversé)
