# site/

Page de téléchargement publique, servie sur https://pommetortue.tech/friend/ (voir [../docs/plans/chunk-06-distribution.md](../docs/plans/chunk-06-distribution.md) ; v3 : [../docs/plans/features/especes-menu/chunk-10-site-publication.md](../docs/plans/features/especes-menu/chunk-10-site-publication.md)).

| Élément | Rôle | Dépendances |
|---|---|---|
| `index.html` | page autonome (CSS/JS inline, clair/sombre, mobile) : menu du haut (La bande, Tous les carnets), un compagnon tiré au hasard qui se promène, bouton de téléchargement, « Qui va sortir de ton œuf ? » (œuf + 6 espèces), installation + SmartScreen, galerie mélangée, la bande (liste en direct via `/friend/api/public/band` avec la tête de chaque espèce, chaque compagnon mène à son carnet `/friend/t/<id>`), vie avec les fenêtres/applis/lien, gestes, désinstallation. Placeholders `{{VERSION}}` et `{{SHA256}}` remplis par `deploy-friend.sh` | `img/` |
| `img/` | sprites PNG transparents de la tortue (racine, historique) générés par `Pepin.exe --frames site/img` (×4) + `icone-256.png` (favicon, source de `Pepin.ico`) | `SheetRenderer.Frames` |
| `img/<espece>/` | mêmes sprites pour `herisson`, `grenouille`, `escargot`, `panda-roux`, `axolotl` (utilisés aussi par les pages carnet et la bande du serveur) | `SheetRenderer.Frames` |
| `img/oeuf/` | l'œuf entier et fissuré (×6) | `SheetRenderer.Frames`, `Egg.DrawShell` |
| `img/items/` | les souvenirs et trouvailles (×6), utilisés par les pages carnet du serveur (`/friend/img/items/<id>.png`) | `SheetRenderer.Frames` |
