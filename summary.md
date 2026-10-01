# compagnon/

Pépin, compagnon de bureau Windows (tortue pixel art). Plan : [docs/plans/MASTER.md](docs/plans/MASTER.md).

| Élément | Rôle | Dépendances | Plans |
|---|---|---|---|
| `Pepin.csproj` | projet .NET 10, publication NativeAOT optimisée taille | SDK .NET 10, MSVC | chunk-01 |
| `build.ps1` | compile l'exe natif dans `publish/` (ajoute vswhere au PATH, ferme l'instance en cours) | dotnet, VS C++ | chunk-01 |
| `README.md` | utilisation, interactions, compilation, outils de dev | — | chunk-05, 06 |
| `Pepin.ico` | icône de l'exe (générée depuis `TurtleArt.DrawIcon`) | — | chunk-06 |
| `deploy-friend.sh` | publie page + images + exe + `version.json` (lu par la mise à jour auto) sur https://pommetortue.tech/friend/ | ssh VPS | chunk-06, mise-a-jour |
| `server/` | serveur de la bande (FastAPI + SQLite, Docker `louann-bande` sur le VPS) — voir [server/summary.md](server/summary.md) | — | bande/01 |
| `site/` | page de téléchargement — voir [site/summary.md](site/summary.md) | — | chunk-06 |
| `src/` | tout le code du client — voir [src/summary.md](src/summary.md) | — | chunks 01-06, v2 |
| `docs/` | concept et plans — voir [docs/summary.md](docs/summary.md) | — | — |
| `publish/` | sortie de compilation (`Pepin.exe`) | — | — |

Données utilisateur hors du dossier : `%APPDATA%\Pepin\state.json` (jauges, taille, position, lien, journal, collection, identité dans la bande), `error.log` (erreurs éventuelles).
À côté de l'exe pendant une mise à jour : `Pepin.new.exe` / `Pepin.old.exe` (supprimés au lancement suivant).
Démarrage auto : valeur `Pepin` dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, activée d'office au premier lancement, décochable dans le menu.
