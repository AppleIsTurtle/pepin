# Chunk 09 — La bande multi-espèces

**Statut** : done (2026-10-01) — `pytest` 59 verts (6 nouveaux : inscription, espèce inconnue → 422, heartbeat, visiteur, pages, migration) ; visites simulées entre espèces différentes toutes terminées (escargot : vitesse ≥ 0,85 en visite) ; **serveur non déployé** (chunk 10) — Parent : [plan.md](plan.md) — Prérequis : chunk 01 + au moins une nouvelle espèce dessinée

## But
Chacun voit la vraie espèce des autres : visiteur à l'écran, listes, collection, pages web.

## Serveur (`server/`)
- `db.py` — migration idempotente : `ALTER TABLE turtles ADD COLUMN species TEXT NOT NULL DEFAULT 'tortue'`
  (vérifier la présence de la colonne avant ; les lignes existantes deviennent des tortues).
- `catalogue.py` — `ESPECES` : liste blanche des 6 codes + nom affiché.
- `app.py` — `Inscription` et `Battement` acceptent `species` (optionnel, validé contre `ESPECES`) ; le heartbeat
  met à jour la colonne si elle diffère (rattrape les comptes existants, et un client 2.x qui n'envoie rien ne
  change rien) ; `species` ajouté aux réponses : liste de la bande, visites (`from`/`to`), `GET /api/turtle/{id}`.
- `pages.py` — `/bande` et `/t/{id}` : sprite de l'espèce (`/friend/img/<espece>/neutre.png`), nom d'espèce dans la
  fiche ; textes « tortue » génériques revus (« les compagnons de la bande »).
- `tests/test_api.py` — inscription avec/sans espèce, espèce invalide refusée (422), heartbeat qui met à jour,
  espèce présente dans la liste et dans une visite.

## Client (`src/`)
- `Band.cs` — `species` dans `RegisterReq`, `HeartbeatReq`, `TurtleRef`, `BandTurtle`, `VisitDto`, `TurtleDeck`
  (source generator JSON : ajouter les propriétés, rien d'autre).
- `Visit.cs`, `App.cs` — la créature visiteuse est créée avec `Species` du visiteur ; si absente/inconnue → tortue.
- `Behaviors.Social.cs` — vérifier chaque activité à deux (`Sniff`, `TagGame`, `ShareSnack`, `NapTogether`,
  `DanceTogether`) avec deux espèces différentes (positions de tête via l'ancre d'espèce, grenouille qui bondit
  pendant `TagGame`, escargot lent : distances/délais adaptés pour qu'il ne reste pas à la traîne).
- `MenuPanel.cs` (chunk 02) — page Visite : petite tête de l'espèce à côté de chaque nom.
- `DeckView.cs` — portrait de l'espèce sur la carte de collection d'un autre.
- `Simulator.cs` — `--simulate` : visite simulée entre deux espèces différentes (paramètres `--species`, `--guest`).

## Vérification
1. `pytest server/tests` vert.
2. `--simulate` : une visite pour chaque couple « hôte tortue × visiteur de chaque espèce » sans pose cassée.
3. Déploiement serveur fait au chunk 10 (pas avant).

## Points d'attention
- Compatibilité : un client 2.x reçoit `species` en trop dans le JSON → ignoré (vérifier que le source generator
  2.x ne plante pas sur une propriété inconnue : c'est le comportement par défaut de System.Text.Json).
- Ne **jamais** écrire en SQL brut dans `visits` (voir mémoire projet) ; la migration ne touche que `turtles`.
