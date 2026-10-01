# Chunk 02 — Comportements avec les fenêtres

**Statut** : done — Parent : [plan.md](plan.md)

## Fichiers
- `src/Behaviors.Windows.cs` (nouveau) — `Perch` (monter, s'installer, balancer les pattes, somnoler, suivre
  la fenêtre, tomber / redescendre), `Fall`, `HideBehind` (cache-cache, jette un œil, réagit au curseur),
  `PushWindow` (pousse ou fait trembler)
- `src/Pet.cs` — `Occluder` (rect à gommer), `PerchHwnd`, poids dans le cerveau, accès à `WindowWorld`
- `src/App.cs` — rafraîchit `WindowWorld`, applique le gommage au rendu et au test de clic

## Points d'attention
- Chute depuis une fenêtre : le sol repasse plus bas, `Z` = hauteur de chute, physique existante
- Recouvrement : si le bord où il est assis passe sous une autre fenêtre, il descend
- Pousser : délai global ≥ 20 min, annulé dès que l'utilisateur bouge
