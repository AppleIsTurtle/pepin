# Chunk 02 — Évènements : la touffe d'herbe

**Statut** : code écrit le 2026-10-01, compile, rendu vérifié par `--preview` (herbe.png) — Parent : [plan.md](plan.md)

## Fichiers
- `src/Grass.cs` (nouveau) — `GrassEvent` : une touffe 12×6 pousse en bas de l'écran (loin de la tortue), ondule de
  temps en temps, repart seule au bout de 90 s. Un clic tire : 40 % rien (feuilles qui volent), 45 % un objet qui jaillit,
  15 % une petite bête (coccinelle, escargot, grenouille, papillon) qui sort, se promène et s'en va
- `src/Overlay.cs` — mode `clickable` : seuls les pixels opaques reçoivent le clic ; `App.Handle` route le clic
- `src/Behaviors.Games.cs` — `NoticeTuft` (la tortue la repère, vient à côté et attend), `WatchCritter` (observe la bête)
- `src/Life.cs`, `src/Glyphs.cs` — 6 nouveaux objets/animaux (`Item`), tirages `Items.HerbObject` / `Items.Critter` ;
  les cadeaux et souvenirs de visite restent tirés parmi les 10 premiers
- `src/App.cs` — planification (1re touffe 10 min après le lancement, puis toutes les 40 à 90 min, seulement si tu es
  actif et qu'il n'y a ni vidéo, rendu, visite ni jeu), `OnGrass` (collection, carnet, lien +0,5 / +1), menu Jeux
- `server/catalogue.py`, `server/app.py`, `server/pages.py` — `TROUVAILLES`, `COLLECTION` (souvenirs + trouvailles),
  stats `finds` / `critters`
- `site/img/items/` — images des 6 nouveautés (`--frames`) ; les 10 anciennes passent de 48 à 60 px (toile 10×10)

## Points d'attention
- La fenêtre fait 96×34 px logiques mais seule la touffe est cliquable
- Désactivable : Jeux → « Propose des jeux de temps en temps » (même réglage que le bowling)
- Le serveur doit être redéployé **avant** la sortie du client : sinon les nouveaux ids de collection sont ignorés

## À vérifier en vrai
1. Menu Jeux → « Faire pousser une touffe d'herbe » : elle pousse, la tortue vient à côté
2. Clic : les trois issues ; l'objet apparaît dans « Ma collection » et dans le carnet
3. Ne pas cliquer : elle disparaît après 90 s
