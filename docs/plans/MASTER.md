# MASTER — Pépin, compagnon de bureau

## Point de départ
Rien d'existant. Concept validé le 2026-09-30 : tortue en pixel art (piste A de `docs/concept-pixel.png`).

## Objectif final
Un petit compagnon de bureau Windows, toujours au premier plan, mignon, avec une personnalité
(« paresseux gourmand ») et un comportement riche, qui interagit rarement avec la souris.
Consommation de ressources quasi nulle.

## Décisions
| Sujet | Choix | Pourquoi |
|---|---|---|
| Techno | C# .NET 10, **Win32 pur + NativeAOT** (pas de WinForms) | exe natif ~3 Mo, ~5-10 Mo RAM, démarrage instantané |
| Rendu | Fenêtre *layered* (`UpdateLayeredWindow`), alpha par pixel, sprite dessiné en code dans une grille ~56×54 px agrandie ×2/×3/×4 | pixels transparents = clics qui traversent ; aucune image à charger |
| Fenêtre | Petite fenêtre qui suit la tortue (pas de calque plein écran), `TOPMOST`, `TOOLWINDOW`, `NOACTIVATE` | ne vole jamais le focus, pas d'entrée dans la barre des tâches |
| Cadence | Timer adaptatif : 60 fps en drag, 30 en mouvement, ~15 au repos, 6 en sommeil ; image non renvoyée si identique | CPU ≈ 0 |
| Monde | « Libre partout » : sol virtuel = zone de travail de l'écran courant ; hauteur `z` + ombre pour sauts/lancers | tortue vue de profil qui se déplace dans toutes les directions |
| Déplacements longs | rentre dans sa carapace et glisse en tournoyant | pas de feuille-hélice sans la pomme |
| Humeur visible | la teinte de la tête/pattes vire au rouge (énervé), rose (câlin), pâle (sommeil) | choix utilisateur « couleur selon l'humeur » |
| Persistance | `%APPDATA%\Pepin\state.json` (jauges + réglages) | humeur persistante entre sessions |

## Périmètre
**Dedans** : rendu pixel art + expressions/effets, ~25 comportements (ambiants, sommeil/rêves, interactions
rares avec la souris, réactions), caresses / clics / attraper-lancer, réaction à l'activité (inactivité,
souris brusque, retour de l'utilisateur), jauges énergie/faim/humeur/affection persistées, icône de
notification (état, réveiller/endormir, appeler, taille, pause, démarrage auto, quitter), multi-écran basique.

**Ajout 2026-09-30** : démarrage auto par défaut + page de téléchargement sur pommetortue.tech/friend (chunk 06).

**Ajout 2026-09-30 (v2)** — voir les features ci-dessous :
- mise à jour automatique (prérequis pour que toute la bande ait la même version)
- interactions avec les fenêtres (s'asseoir dessus, se cacher derrière, les pousser) et réactions aux applis
- personnalité qui évolue (attachement par paliers qui débloque des comportements, carnet de vie)
- la bande : toutes les tortues téléchargées se connaissent via un serveur sur le VPS ; visites
  spontanées ou à la demande, jeux à deux, souvenirs, petits mots ; carnets publics en ligne

**Dehors** : sons, nourrir manuellement, installeur, masquage automatique en plein écran
(demande explicite : toujours par-dessus tout), couleur/accessoires personnalisés, codes d'amis
(choix « une seule bande »).

## Features v2
| Feature | Plan | Statut |
|---|---|---|
| Mise à jour automatique | [features/mise-a-jour/plan.md](features/mise-a-jour/plan.md) | done |
| Fenêtres et applis | [features/fenetres/plan.md](features/fenetres/plan.md) | done |
| Personnalité qui évolue | [features/evolution/plan.md](features/evolution/plan.md) | done |
| La bande | [features/bande/plan.md](features/bande/plan.md) | done |
| Mini-jeux, évènements, réseau enrichi | [features/jeux/plan.md](features/jeux/plan.md) | code écrit, à essayer en vrai |

## Chunks
| # | Fichier | Statut |
|---|---|---|
| 01 | [chunk-01-socle-fenetre.md](chunk-01-socle-fenetre.md) | done |
| 02 | [chunk-02-sprites.md](chunk-02-sprites.md) | done |
| 03 | [chunk-03-monde-physique.md](chunk-03-monde-physique.md) | done |
| 04 | [chunk-04-cerveau-comportements.md](chunk-04-cerveau-comportements.md) | done |
| 05 | [chunk-05-humeur-sens-finitions.md](chunk-05-humeur-sens-finitions.md) | done |
| 06 | [chunk-06-distribution.md](chunk-06-distribution.md) | done |
