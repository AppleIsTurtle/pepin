# Chunk 03 — Monde et physique

**Statut** : done — Parent : [MASTER.md](MASTER.md)

## Fichiers
- `src/Pet.cs` (partie physique) — position au sol (X, Y), hauteur Z, vitesses, direction
- `src/Senses.cs` — curseur (position, vitesse lissée), inactivité système (`GetLastInputInfo`), zone de travail de l'écran

## Fonctionnalités
- Sol virtuel = zone de travail du moniteur où se trouve la tortue ; changement d'écran en la déposant ailleurs
- Marche vers une cible (lente : c'est une tortue paresseuse), glissade en carapace pour les longues distances
- Sauts (attaque) : arc en Z avec gravité ; l'ombre reste au sol et rétrécit avec la hauteur
- Attraper / lancer : suit le curseur, vitesse de lâcher = vitesse récente du curseur ; rebonds sur le sol (Z)
  et sur les bords de l'écran (« bonk » + poussière), friction au sol

## Points d'attention
- Coordonnées en pixels physiques (DPI per-monitor) : pas de flou, pas de décalage
- Borner la position à la zone de travail (barre des tâches exclue)
