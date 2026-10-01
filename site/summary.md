# site/

Page de téléchargement publique, servie sur https://pommetortue.tech/friend/ (voir [../docs/plans/chunk-06-distribution.md](../docs/plans/chunk-06-distribution.md)).

| Élément | Rôle | Dépendances |
|---|---|---|
| `index.html` | page autonome (CSS/JS inline, clair/sombre, mobile) : menu du haut (La bande, Tous les carnets), tortue animée, bouton de téléchargement, installation + SmartScreen, galerie, la bande (liste en direct via `/friend/api/public/band`, chaque tortue mène à son carnet `/friend/t/<id>`), vie avec les fenêtres/applis/lien, gestes, désinstallation. Placeholders `{{VERSION}}` et `{{SHA256}}` remplis par `deploy-friend.sh` | `img/` |
| `img/` | sprites PNG transparents générés par `Pepin.exe --frames site/img` (×4) + `icone-256.png` (favicon, source de `Pepin.ico`) | `SheetRenderer.Frames` |
| `img/items/` | les 10 souvenirs (×6), utilisés par les pages carnet du serveur (`/friend/img/items/<id>.png`) | `SheetRenderer.Frames` |
