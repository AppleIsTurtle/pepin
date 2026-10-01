# Chunk 01 — Lien et carnet

**Statut** : done — Parent : [plan.md](plan.md)

## Fichiers
- `src/Life.cs` (nouveau) — `Bond`, palier, compteurs, collection, journal ; persistance dans `state.json`
- `src/Behaviors.Life.cs` (nouveau) — `LevelUp`, `FollowYou`, `NapByCursor`, `BringGift`, `SadGoodbye`, `JoyDance`
- `src/Glyphs.cs` / `src/TurtleArt.cs` — objets (cadeaux, souvenirs), étincelles
- `src/Pet.cs` — gains/pertes, poids selon le palier ; `src/App.cs` — menu Carnet

## Points d'attention
- Les compteurs « aujourd'hui » se remettent à zéro à minuit local
- Journal : phrases courtes en français, datées ; `Private` pour tout ce qui touche aux applis
