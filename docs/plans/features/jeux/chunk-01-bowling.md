# Chunk 01 — Bowling

**Statut** : code écrit le 2026-10-01, compile (SDK .NET 10 utilisateur, sans NativeAOT : pas de VS C++ sur la machine
de dev) ; `dotnet run -- --preview` simule un lancer et en dessine les images ; jamais essayé en vrai à l'écran
— Parent : [plan.md](plan.md)

## Fichiers
- `src/Overlay.cs` (nouveau) — fenêtre layered traversable qui affiche une `PixelCanvas` en gros pixels, placée sous la tortue
- `src/Bowling.cs` (nouveau) — le jeu : piste, 6 quilles (chute en rotation de pixels), physique, phases
  `Off → Prep → Setup → Wait → Throw → Over`, abandon automatique
- `src/Behaviors.Games.cs` (nouveau) — `BowlSetup` (va se placer, quilles tombent du ciel), `BowlWait` (attend le lancer,
  « Lance-moi ! »), `BowlReact` (compte, réagit, bilan et rangement)
- `src/Pet.cs` — `Bowl`, `NextGameAt`, choix spontané (kind 7), `OnRelease` signale le lancer, une partie en cours
  retient la tortue (`BowlWait`)
- `src/Behaviors.cs` — `Thrown` : pendant un lancer de bowling → `BowlReact` au lieu des pénalités
- `src/Life.cs` — `MiniGames` (réglage persistant)
- `src/App.cs` — propriétaire de l'`Overlay` et du `Bowling`, tick, cadence, menu « Jeux », arrêt sur pause / visite
- `server/catalogue.py`, `server/app.py` — stats `bowling` / `strikes` ; limite des stats du carnet 20 → 40

## Points d'attention
- Distances en « u » (= `Scale` pixels écran) ; si la taille change en cours de partie, la piste disparaît
- Le décor est derrière la tortue même quand elle passe « derrière » les quilles (pas de tri en profondeur avec elle)
- Les quilles sont dessinées dans une toile de 70×68 px logiques ; le lancer à ~700 px/s atteint la quille de tête à 100 %

## À vérifier à la première compilation
1. (fait) Ça compile ; `--preview` montre les quilles qui tombent pour un lancer au centre
2. Menu Jeux → Bowling : elle va se placer, les quilles tombent, « Lance-moi ! »
3. Lancer fort vers les quilles : elles tombent, les tombées disparaissent, bilan correct, deuxième lancer
4. Ne rien faire : abandon après ~55 s, quilles disparues, pas de fenêtre résiduelle
5. Pause / visite reçue pendant une partie : tout disparaît proprement
