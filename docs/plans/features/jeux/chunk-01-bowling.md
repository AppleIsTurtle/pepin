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

## Correctif du 2026-10-01 (premier essai en vrai : « rien ne se passe »)
Le lancer normal est un vol (saut de 150-450 px/s) : à 800-1800 px/s la tortue passait au-dessus des quilles et
retombait loin derrière (0 quille sur 15 lancers simulés). Corrigé : au bowling elle **roule au sol** (`Pet.OnRelease`,
`Bowling.Ready`) et la collision teste **tout le segment parcouru** depuis l'image précédente (à 40 images/s elle avance
de plus de 40 px par image et traversait une quille sans la toucher). `--preview` simule maintenant 15 vrais lancers
(vitesse × angle) et affiche les quilles tombées : sous ~700 px/s elle s'arrête avant la piste (raté), au-delà ça touche.

## Deuxième correctif (2026-10-01) : « 1 quille tombée, puis impossible, et les quilles restent »
- Les quilles restaient 5 min après un premier lancer : l'abandon (55 s sans lancer) n'existait qu'avant le 1er lancer.
  Maintenant 45 s d'inactivité à n'importe quel moment du tour → rangement, et le carnet note la partie interrompue
- Un lancer « normal » à la souris (400-600 px/s) s'arrêtait avant les quilles (frottement 1400 px/s², quilles à 210 px).
  Maintenant : frottement ×0,35 pendant un lancer de bowling (`Pet.FrictionScale`) et quilles à 55 px logiques ;
  dès ~500 px/s ça touche (`--preview` : 18 lancers simulés + une partie complète + un tour abandonné)

## À vérifier à la première compilation
1. (fait) Ça compile ; `--preview` montre les quilles qui tombent pour un lancer au centre
2. Menu Jeux → Bowling : elle va se placer, les quilles tombent, « Lance-moi ! »
3. Lancer fort vers les quilles : elles tombent, les tombées disparaissent, bilan correct, deuxième lancer
4. Ne rien faire : abandon après ~55 s, quilles disparues, pas de fenêtre résiduelle
5. Pause / visite reçue pendant une partie : tout disparaît proprement
