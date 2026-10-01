# Chunk 01 — Serveur de la bande

**Statut** : done — Parent : [plan.md](plan.md)

## Fichiers
`server/` : `app.py` (+ modules), `requirements.txt`, `Dockerfile`, `docker-compose.yml`, `tests/`, `summary.md`

## API (préfixe /api, jeton Bearer sauf register/health)
register · heartbeat (évènements + ack) · visit · visit/{id}/accept · visit/{id}/end · visit/{id}/abort ·
band · rename · block/unblock · carnet (PUT) · health. Pages : `/t/{id}` (carnet), `/bande`.
Détail : `server/summary.md`.

## Points d'attention
- Visite pending sans réponse de l'hôte > 150 s → « personne à la maison » ; active expirée → terminée d'office
- Tout contenu client échappé ; corps ≤ 32 Ko ; limites de débit (inscription, visites, heartbeat)
