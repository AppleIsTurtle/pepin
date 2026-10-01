# Chunk 04 — Cerveau et comportements

**Statut** : done — Parent : [MASTER.md](MASTER.md)

## Fichiers
- `src/Behaviors.cs` — un comportement = une petite classe (`Update` écrit la pose/visage/effets, `Done` quand fini)
- `src/Pet.cs` (partie cerveau) — tirage pondéré du prochain comportement, réactions aux stimuli, clignements, suivi du regard

## Personnalité : paresseux gourmand
Lent, fait beaucoup de pauses et de siestes, grignote souvent, rêve de nourriture, grognon quand on le réveille.
Les interactions avec la souris sont **rares** (délai global de 8 à 15 min) donc surprenantes.

## Comportements
- **Ambiants** : attendre (respire, regarde autour), flâner, se poser (pattes repliées), bâiller, s'étirer,
  regarder à gauche/droite, grignoter (salade ou fraise, bouchées, miettes), fredonner (notes), se secouer,
  voyager en carapace
- **Sommeil** : s'installe, Zzz, bulle de nez qui respire, mâchouille en dormant, rêves (bulle : fraise/salade/cœur),
  réveil naturel (bâillement + étirement), réveil forcé grognon (souvent se rendort)
- **Souris (rares)** : tourner autour du curseur, l'attaquer (se tasse, remue, bondit, mord ; triomphe ou rate
  et s'énerve), le poursuivre, venir réclamer de l'attention, bouder
- **Réactions** : attrapé (gigote, sueur), lancé (carapace qui tourne, rebonds), sonné (yeux spirale, étoiles),
  surpris (!, petit saut), cachette dans la carapace (puis jette un œil), colère (vapeur, rouge, trépigne),
  câlin (yeux ^^ ou cœurs, rougit, cœurs), accueil au retour de l'utilisateur

## Points d'attention
- Poids fonction des jauges, de l'heure (nuit = plus de sommeil) et de l'inactivité de l'utilisateur
- Chaque comportement demande sa cadence (fps) pour économiser le CPU
