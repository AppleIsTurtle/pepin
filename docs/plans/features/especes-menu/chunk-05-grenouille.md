# Chunk 05 — Grenouille

**Statut** : done (2026-10-01) — planche `docs/apercus/planches/grenouille.png` + récapitulatif `docs/apercus/especes.png` envoyés à Louann (enchaînement demandé : pas d'attente de validation entre espèces) ; `--simulate 1 --species grenouille --guest grenouille` : 0 anomalie, visite complète — Parent : [plan.md](plan.md) — Prérequis : chunk 01 (gabarit : [chunk-04-herisson.md](chunk-04-herisson.md))

## La grenouille
- **Silhouette** : corps vert arrondi assis, gros yeux en haut de la tête (paupières pour `HalfLid`/`Closed`),
  ventre clair, longues pattes arrière repliées, petites pattes avant. Teinte d'humeur sur tout le vert.
- **Repli** (`InShell`) : accroupie, aplatie, yeux mi-clos, pattes rentrées.
- **Roulé** (`SpinFrame`) : glissade à plat ventre, pattes étendues (bowling, lancer).
- Les yeux sont **sur le dessus** : adapter `ArtKit.Face` via l'ancre de tête (yeux plus hauts, bouche large).

## Traits propres
| Trait | Déclencheur | Détail |
|---|---|---|
| Déplacement par bonds | **tous** les déplacements (marche et longs trajets) | suite de sauts (`ShadowZ` en arc, pattes arrière tendues au décollage, réception accroupie) ; plus de cycle de marche |
| Langue qui gobe une mouche | ambiant (poids ~0,8), plus fréquent quand elle a faim | une mouche (nouvel effet `FxKind.Fly`, 2 px qui zigzaguent) tourne autour ; la grenouille suit des yeux, **langue** rose qui part en ligne droite (nouvel effet `FxKind.Tongue`), mouche gobée, mâche, faim −0,05 |
| Gorge qui gonfle | ambiant (poids ~0,6), contente ou au calme | bulle de gorge qui gonfle/dégonfle 3 fois (`Visual.Puffed`), petites notes « croâ » |

## Personnalité
Plus vive : `Wander` ×1,2, `Rest` ×0,8 ; adore les fenêtres vidéo (`WatchVideo` ×1,3).

## Points d'attention
- Les bonds doivent rester dans la zone de travail (même `ClampToScreen`) et ne pas déclencher les réactions de
  « chute » (`Fall`) : c'est un saut volontaire.
- Les perches de fenêtres : elle y arrive d'un bond, pas en grimpant.
