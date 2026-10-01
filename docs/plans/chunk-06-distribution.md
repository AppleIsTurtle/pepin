# Chunk 06 — Démarrage auto et distribution

**Statut** : done — Parent : [MASTER.md](MASTER.md) — ajouté le 2026-09-30 à la demande de Louann

## Fichiers
- `src/App.cs` (`SetupAutostart`), `src/Mood.cs` (`AutostartSet`) — premier lancement : inscription dans
  `HKCU\…\Run` ; lancements suivants : chemin remis à jour si l'exe a bougé ; décochable dans le menu
- `Pepin.ico`, `Pepin.csproj` — icône de l'exe (tête de tortue, tailles en plus-proche voisin) + infos de version
- `src/SheetRenderer.cs` (`Frames`) — `--frames dossier` : sprites transparents pour la page
- `site/index.html`, `site/img/` — page de téléchargement (placeholders `{{VERSION}}`, `{{SHA256}}`)
- `deploy-friend.sh` — assemble page + images + exe, envoie sur le VPS dans `/var/www/friend`

## Hébergement
https://pommetortue.tech/friend/ — bloc `location /friend/` (alias statique, `charset utf-8`, `.exe` en
`attachment` + `no-cache`) dans le vhost `pommetortue.tech`, sauvegarde `pommetortue.tech.bak-avant-friend-20260930`.

## Points d'attention
- Exe non signé : SmartScreen affiche un avertissement au premier lancement (expliqué sur la page)
- Nouvelle version = `build.ps1` puis `bash deploy-friend.sh` (monter `<Version>` dans le csproj)
