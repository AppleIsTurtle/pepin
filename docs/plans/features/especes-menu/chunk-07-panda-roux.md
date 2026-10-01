# Chunk 07 — Panda roux

**Statut** : done (2026-10-01) — planche `docs/apercus/planches/panda-roux.png` + récapitulatif `docs/apercus/especes.png` envoyés à Louann (enchaînement demandé : pas d'attente de validation entre espèces) ; `--simulate 1 --species panda-roux --guest panda-roux` : 0 anomalie, visite complète — Parent : [plan.md](plan.md) — Prérequis : chunk 01 (gabarit : [chunk-04-herisson.md](chunk-04-herisson.md))

## Le panda roux
- **Silhouette** : quadrupède roux, masque blanc autour des yeux et du museau, oreilles pointues bordées de blanc,
  pattes et ventre brun foncé, **grosse queue annelée** (roux / ocre) qui dépasse derrière.
  La queue doit tenir dans la grille ~56×54 : relevée en arc au-dessus du dos plutôt qu'à l'horizontale.
- **Repli** (`InShell`) : roulé en boule, enroulé dans sa queue, museau caché dedans.
- **Roulé** (`SpinFrame`) : roulé-boulé (bowling, lancer, déplacements longs = petite course rapide puis roulade).

## Traits propres
| Trait | Déclencheur | Détail |
|---|---|---|
| Se dresse bras levés | surprise, clic brusque, contrariété, souris qui fonce dessus | debout sur les pattes arrière, bras écartés en l'air (nouveau champ `Visual.Stand`), queue gonflée ; 1-2 s puis retombe à quatre pattes |
| Dort enroulé dans sa queue | **tous** ses sommeils | la queue lui couvre le museau, respiration visible (la boule se soulève) |
| Adore se percher | choix des comportements | poids des perches de fenêtres ×2 (`SpeciesTraits`), y reste plus longtemps, laisse pendre la queue du bord |

## Personnalité
Curieux et joueur : `LookAround` ×1,3, jeux avec la souris ×1,2 ; un peu gourmand comme la tortue.

## Points d'attention
- `Stand` est générique (pourra servir à d'autres espèces plus tard), ignoré par celles qui ne le dessinent pas.
- La queue pendante en perche ne doit pas sortir de la toile (sinon test de clic faux).
