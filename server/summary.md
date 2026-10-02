# compagnon/server/

Serveur « bande » de Pépin (FastAPI + sqlite3 de la stdlib) : inscription anonyme, heartbeat, visites entre
tortues, pages publiques. Plan : [../docs/plans/features/bande/chunk-01-serveur.md](../docs/plans/features/bande/chunk-01-serveur.md).

| Fichier | Rôle | Dépendances |
|---|---|---|
| `app.py` | app FastAPI : limite de corps 32 Ko, auth Bearer, limites de débit, `/api/*`, `/discord` (redirige vers `DISCORD_INVITE`), routes des pages, horloge `now()` remplaçable en test | db, pages, catalogue |
| `db.py` | schéma, `connect()`, `init()` (WAL + `migrer()` : colonne `species`, v3), `tx()` en `BEGIN IMMEDIATE`, lecture ; `DB_PATH` depuis l'env | sqlite3 |
| `pages.py` | `/t/{id}` (carnet, portrait de l'espèce) et `/bande` (mini-portraits), CSS inline autorisée par empreinte CSP, dates en français (heure de Paris calculée sans base tz) ; `sprite(espece)` : tortue à la racine de `/friend/img/`, les autres dans `/friend/img/<espece>/` | db, catalogue |
| `catalogue.py` | noms aléatoires, souvenirs, activités, paliers (palier 0 « Nouveau venu »), espèces (`ESPECES`), libellés des stats | — |
| `requirements.txt`, `Dockerfile`, `docker-compose.yml`, `.dockerignore` | déploiement (service `louann-bande`, 127.0.0.1:${PORT:-3060}, 128 Mo, 0,5 CPU, uid 1000) | — |
| `tests/` | tests pytest — voir [tests/summary.md](tests/summary.md) | pytest, httpx |
| `.venv/` | environnement local de test (hors image Docker) | — |

## Base
`turtles` (id, token_hash, name, name_key unique casefold, last_seen, status, tier, version, accept_messages, carnet_json, species — défaut `tortue`) ·
`visits` (from/to, message, state pending→active→done|aborted|no_one_home, duration, souvenir, played) ·
`friendships` (paire a<b, points) · `blocks` · `events` (file par destinataire, acquittée par heartbeat).

## Endpoints
`species` (v3, liste blanche `ESPECES`, optionnelle : absente = tortue / inchangée) acceptée par `register` et `heartbeat`, renvoyée par `band`, `turtle`, `public/band`, les visites (`from`, `host`).
`GET /api/health` · `POST /api/register` (10/h/IP) · `POST /api/heartbeat` (1 / 4 s) · `POST /api/visit` (6/h) ·
`POST /api/visit/{id}/accept|end|abort` · `GET /api/band` (avec `blocked`) · `POST /api/rename` · `POST /api/block|unblock` ·
`PUT /api/carnet` · `GET /api/turtle/{id}` (collection et stats d'une autre tortue, 404 si bloquée) · `GET /api/public/band` (sans jeton : même contenu que la page /bande, pour /friend/) ·
pages `GET /t/{id}`, `GET /bande`.
Erreurs : `{"error": ...}` — `auth`, `invalide`, `trop_gros`, `trop_de_requetes`, `trop_de_visites`, `interdit`,
`introuvable`, `etat`, `soi_meme`, `deja_en_visite`, `indisponible`, `personne`, `nom_invalide`, `nom_pris`.

## Tests
```
python -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt pytest httpx
.venv\Scripts\python -m pytest -q tests
```

## Déploiement
`data/` doit appartenir à l'uid 1000 **avant** `docker compose up` ; `PORT=… docker compose up -d --build` ;
nginx : `/friend/api/` → `/api/`, `/friend/t/` → `/t/`, `/friend/bande` → `/bande`, avec `X-Real-IP`.
Images `/friend/img/` (dont `items/<souvenir>.png`) servies par nginx. Sauvegarde : `sqlite3 data/bande.db ".backup …"`.

## Historique
- 2026-10-01 : stats `bowling` / `strikes` / `finds` / `critters`, limite des stats du carnet portée à 40, trouvailles
  (`TROUVAILLES`, `COLLECTION`), `GET /api/turtle/{id}` (feature jeux).
- 2026-09-30 : création (feature bande, chunk 01).
- 2026-10-01 : `GET /api/public/band` pour afficher la bande et les liens vers les carnets sur /friend/.
- 2026-10-02 : visite au hasard à tirage uniforme parmi toutes les tortues en ligne ; plus de limite de visites par heure ni d'hôte « occupé » (un hôte reçoit autant de visiteurs qu'il veut) ; route `/discord`.
