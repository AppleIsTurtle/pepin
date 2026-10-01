# Chunk 10 — Site et publication de la 3.0.0

**Statut** : done (2026-10-01) — `--frames` par espèce + œuf, `index.html` v3, `deploy-friend.sh` (tous les sous-dossiers d'images), README, 3.0.0 compilée. **Publiée** : base sauvegardée dans `~/louann-bande-backup-2026-10-01-v3/` (base + anciens .py), conteneur `louann-bande` seul reconstruit (migration `species` faite, 2 comptes → tortue), `/friend/bande` et `/api/public/band` OK ; `deploy-friend.sh` → `version.json` 3.0.0, SHA-256 en ligne = local, images des espèces et de l'œuf en 200. Menu et œuf pas encore essayés à la main — Parent : [plan.md](plan.md) — Prérequis : chunks 01 à 09 done et validés par Louann

## Site (`site/`)
- `SheetRenderer` `--frames` : exporter les sprites de **chaque espèce** dans `site/img/<espece>/` (neutre, marche,
  dort, câlin…) + l'œuf (entier, fissuré, éclosion) dans `site/img/oeuf/`.
- `site/index.html` — la page de téléchargement présente l'œuf et les 6 espèces (« Qui va sortir de ton œuf ? »),
  sans parler de rareté ; garder l'explication SmartScreen ; mettre à jour la liste des nouveautés.
- `deploy-friend.sh` — copier aussi les sous-dossiers `img/<espece>/` et `img/oeuf/` (aujourd'hui seuls `img/*.png`
  et `img/items/*.png` partent ; le script vide `/var/www/friend` à chaque publication).
- `README.md` — espèces, œuf, nouveau menu.

## Publication (dans cet ordre)
1. `Pepin.csproj` : `<Version>3.0.0</Version>`.
2. `build.ps1` puis vérifications finales : `--sheet-all`, `--preview`, `--simulate 3`, essai en vrai via `explorer.exe`
   (Pistou reste une tortue, pas d'œuf, nouveau menu).
3. **Serveur d'abord** : sauvegarde de la base (`~/louann-bande-backup-<date>/`), envoi des fichiers dans
   `/home/ubuntu/louann-bande`, reconstruction du **seul** conteneur `louann-bande` (vérifier ports/conteneurs avant,
   cf. mémoire « VPS : vérifier avant d'agir ») ; `pytest` passé avant ; vérifier `/bande` et un `/t/{id}`.
4. `bash deploy-friend.sh` ; vérifier `version.json` (3.0.0), le SHA-256 de l'exe en ligne, la page et les images.
5. Commit + push (dépôt `AppleIsTurtle/pepin`), relancer le Pépin local via `explorer.exe`.

## Mise à jour des plans
- `plan.md` : statuts des chunks ; `../../MASTER.md` : statut de la feature ; `../summary.md` ;
  `src/summary.md`, `server/summary.md`, `site/summary.md` ; mémoire projet (`compagnon-pepin`).

## Points d'attention
- La mise à jour auto part chez **tous** les amis dès `deploy-friend.sh` : ne publier qu'avec l'accord explicite de
  Louann, serveur déjà déployé.
- Les installations existantes passent en 3.0.0 **sans œuf** et restent des tortues (vérifié à l'étape 2).
