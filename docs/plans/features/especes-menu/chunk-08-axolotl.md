# Chunk 08 — Axolotl

**Statut** : done (2026-10-01) — planche `docs/apercus/planches/axolotl.png` + récapitulatif `docs/apercus/especes.png` envoyés à Louann (enchaînement demandé : pas d'attente de validation entre espèces) ; `--simulate 1 --species axolotl --guest axolotl` : 0 anomalie, visite complète — Parent : [plan.md](plan.md) — Prérequis : chunk 01 (gabarit : [chunk-04-herisson.md](chunk-04-herisson.md))

## L'axolotl
- **Silhouette** : corps rose pâle allongé, grosse tête ronde, **trois paires de branchies** frangées (rose vif) de
  chaque côté de la tête, petits yeux noirs, **grand sourire** par défaut, quatre petites pattes, queue aplatie
  avec nageoire. La teinte d'humeur s'applique au rose du corps (la colère le rend rouge vif).
- **Repli** (`InShell`) : enroulé sur lui-même, queue autour de la tête.
- **Roulé** (`SpinFrame`) : vrille (bowling, lancer).

## Traits propres
| Trait | Déclencheur | Détail |
|---|---|---|
| Flotte | en permanence quand il est éveillé et au sol | 3-6 px logiques au-dessus du sol, ondulation lente (sinus), ombre au sol décalée (`ShadowZ`) ; la marche devient une nage (corps et queue qui ondulent, pattes qui pagaient) ; se pose au sol pour dormir et manger |
| Branchies qui ondulent | toujours (animation de base) | vitesse et ampleur selon l'humeur : rapides et dressées quand il est content/excité, tombantes quand il est triste, plaquées quand il est en colère |
| Déplacement long | trajets longs | nage rapide en vrille courte, petites bulles (`FxKind.Bubble` existant) |

## Personnalité
Placide et souriant : `Idle` ×1,2, colère qui monte moins vite ; adore les caresses (affection +10 %).

## Points d'attention
- Flotter ne doit pas être pris pour une chute ni bloquer les perches (sur une fenêtre il se pose).
- Le sourire par défaut : `Mouth.Smile` dessiné différemment (large) pour cette espèce, les autres bouches inchangées.
