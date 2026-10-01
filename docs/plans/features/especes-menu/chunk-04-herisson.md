# Chunk 04 — Hérisson

**Statut** : done (2026-10-01) — planche `docs/apercus/planches/herisson.png` + récapitulatif `docs/apercus/especes.png` envoyés à Louann (enchaînement demandé : pas d'attente de validation entre espèces) ; `--simulate 1 --species herisson --guest herisson` : 0 anomalie, visite complète — Parent : [plan.md](plan.md) — Prérequis : chunk 01

## Gabarit commun aux chunks d'espèce (04 à 08)
Chaque espèce suit les mêmes étapes :
1. **Dessin** `src/<Espece>Art.cs` : toutes les poses de la base commune, lues dans `Visual` —
   debout/marche (`LegPhase` 0-3), pattes repliées (`LegsTuck`), pendu (`LegsDangle`), repli (`InShell`),
   rotation/roulé (`SpinFrame`), tête sortie/rentrée (`HeadOut`), décalages tête/corps, saut (`ShadowZ`).
   Visage, effets, ombre, nourriture tenue : `ArtKit` (partagé), positionnés sur l'ancre de tête de l'espèce.
   Teinte d'humeur (`Tint`, `Flush`, `Blush`) appliquée aux mêmes zones « peau » que la tortue.
2. **Icône** 16×16 pour la zone de notification (`DrawIcon`) : la tête de l'espèce.
3. **Traits propres** dans `src/Behaviors.Species.cs` (un `#region` par espèce) + poids dans `Pet.PickNext`
   conditionnés par l'espèce ; réglages dans `SpeciesTraits` (`src/Species.cs`).
4. **Déplacement long** : remplace la toupie de la tortue par le mode de l'espèce (point d'entrée unique prévu
   au chunk 01).
5. **Bowling et lancer** : pose de repli, même physique.
6. **Planche** `--sheet <espece>.png` envoyée à Louann ; **on ne passe à l'espèce suivante qu'après son accord**.
7. `--simulate 3` avec l'espèce forcée (`--species <espece>` pour les outils de dev) : aucun comportement n'affiche
   de pose cassée (vérifier les images clés de la simulation).

## Le hérisson
- **Silhouette** : dôme de piquants brun-gris (dents en bordure, reflets clairs), museau pointu crème, petite truffe
  noire, petites pattes. Peau (tête, pattes) qui prend la teinte d'humeur.
- **Repli** (`InShell`) : boule de piquants, museau caché.
- **Roulé** (`SpinFrame`) : la boule tourne (4 images avec piquants décalés) — sert aux déplacements longs et au bowling.

## Traits propres
| Trait | Déclencheur | Détail |
|---|---|---|
| Boule roulante | déplacement long, lancer, bowling | se roule en boule, roule (rebonds légers sur le sol), se déroule à l'arrivée en s'ébrouant |
| Piquants hérissés | clic brusque, lancer, souris rapide qui fonce dessus, réveil en sursaut | piquants qui se dressent 1 s (nouveau champ `Visual.Puffed`), yeux ronds, « ! » ; retombent doucement |
| Renifle le sol | comportement ambiant (poids ~1) | museau au sol qui avance par à-coups, petites particules de terre, parfois trouve un objet (même tirage que `BringGift`, plus rare) |

## Personnalité
Un peu plus timide : `HideInShell` (en boule) et `HideBehind` légèrement plus fréquents ; vitesse de marche ×1,1 (trottine).

## Points d'attention
- La boule doit rester lisible à l'échelle 2 (petit) : contraste des piquants.
- `Puffed` est générique (réutilisé par la gorge de la grenouille au chunk 05).
