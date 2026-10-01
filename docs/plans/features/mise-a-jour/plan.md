# Feature — Mise à jour automatique

Parent : [../../MASTER.md](../../MASTER.md)

## Point de départ
v1.0.0 distribuée sur /friend : aucune mise à jour possible sans retélécharger.

## Objectif
Chaque tortue se met à jour seule, sans rien demander, pour que la bande partage le même protocole.

## Périmètre
- `version.json` publié à côté de l'exe (`{version, sha256, url}`) par `deploy-friend.sh`
- vérification au lancement puis toutes les 24 h, en arrière-plan (thread), seulement pour l'exe NativeAOT
- téléchargement → contrôle SHA-256 → renommage de l'exe en cours en `Pepin.old.exe` → nouvel exe à sa place
  → relance avec `--updated` → l'ancien processus se ferme ; le nouveau attend la libération du mutex
- au lancement : suppression de `Pepin.old.exe` ; avec `--updated`, petite animation « nouvelle carapace »
- Dehors : canal bêta, retour arrière, signature de code

## Chunks
| # | Fichier | Statut |
|---|---|---|
| 01 | [chunk-01-updater.md](chunk-01-updater.md) | done |
