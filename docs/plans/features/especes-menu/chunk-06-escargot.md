# Chunk 06 — Escargot

**Statut** : done (2026-10-01) — planche `docs/apercus/planches/escargot.png` + récapitulatif `docs/apercus/especes.png` envoyés à Louann (enchaînement demandé : pas d'attente de validation entre espèces) ; `--simulate 1 --species escargot --guest escargot` : 0 anomalie, visite complète — Parent : [plan.md](plan.md) — Prérequis : chunk 01 (gabarit : [chunk-04-herisson.md](chunk-04-herisson.md))

## L'escargot
- **Silhouette** : coquille en spirale (tons caramel, spirale foncée, reflet), corps beige-rosé allongé au sol,
  deux antennes avec les yeux au bout (les yeux de `ArtKit.Face` sont placés au bout des antennes), pas de pattes.
- Marche = ondulation du pied (`LegPhase` → vague qui parcourt le dessous du corps).
- **Repli** (`InShell`) : corps rentré, seule la coquille posée (proche de la tortue).
- **Roulé** (`SpinFrame`) : coquille qui tourne sur elle-même (déplacements longs, bowling, lancer).
- Antennes expressives : rentrent à moitié quand il est contrarié ou surpris, s'agitent quand il est content.

## Traits propres
| Trait | Déclencheur | Détail |
|---|---|---|
| Très lent | toute marche | vitesse ×0,4 ; pour les longs trajets il se met en coquille et **glisse** (comme la toupie de la tortue) pour ne pas mettre des minutes |
| Coquille | surprise, lancer, peur, sommeil | rentre dans la coquille, en ressort doucement antennes d'abord |
| Traînée brillante | quand il avance au sol | traînée argentée qui s'efface en quelques secondes, dessinée dans un `Overlay` traversable sous lui (pas dans la toile de l'animal) ; petites étincelles |

## Personnalité
Très calme : `Rest` ×1,3, `Sleep` plus long ; boude moins (`Sulk` ×0,7).

## Points d'attention
- La traînée : un seul `Overlay` réutilisé, mis à jour à faible cadence (5-10 i/s), détruit quand elle a disparu ;
  rien quand il est perché, en visite ou en pause.
- Suivi des yeux (`Pet.Automatisms`) : ancre de tête au bout des antennes.
