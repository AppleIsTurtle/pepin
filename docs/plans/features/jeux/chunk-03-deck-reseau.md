# Chunk 03 — Collection des autres (deck) et réseau

**Statut** : code écrit le 2026-10-01, compile, serveur testé (53 tests), rendu vérifié par `--preview` (deck.png)
— Parent : [plan.md](plan.md)

## Fichiers
- `server/app.py` — `GET /api/turtle/{id}` (auth) : nom, palier, en ligne, points d'amitié, collection, stats ; jamais le
  journal ; 404 si la tortue n'existe pas ou si l'un des deux a bloqué l'autre
- `server/tests/test_api.py` — `test_carnet_trouvailles_et_consultation`
- `src/Band.cs` — `FetchTurtle` (thread réseau, résultat sur le thread UI), `TurtleDeck`
- `src/DeckView.cs` (nouveau) — carte pixel-art de 16 cases (objets possédés avec leur nombre, les autres en
  silhouette, ★ = « elle l'a, pas toi »), « n/16 découverts », un clic ferme (ou 60 s)
- `src/PixelFont.cs` (nouveau) — police pixel-art en capitales avec accents, pour les cartes
- `src/App.cs` — menus : Carnet → « Ma collection », La bande → « Voir la collection de… » (25 premières tortues)
- `src/Preview.cs` — outil de dev `--preview` (carte, herbe, bowling simulé en PNG)

## Points d'attention
- La collection d'une tortue n'est à jour qu'au dernier envoi de son carnet (toutes les 10 min au plus)
- Pas de liste de blocage côté client : une tortue bloquée répond 404 (« introuvable »)

## À vérifier en vrai
1. Menu La bande → Voir la collection de… → une tortue : la carte s'ouvre au centre de l'écran, un clic la ferme
2. Hors ligne : message « pas de connexion »
