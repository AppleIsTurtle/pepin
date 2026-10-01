# Chunk 03 — Déploiement v2

**Statut** : done — Parent : [plan.md](plan.md)

## Étapes
1. Inventaire VPS (ports, conteneurs, vhost) → port libre pour `louann-bande`
2. Envoi de `server/`, `docker compose up -d --build`, test `/api/health`
3. vhost `pommetortue.tech` : blocs `/friend/api/`, `/friend/t/`, `/friend/bande` (sauvegarde, `nginx -t`,
   reload, comparaison des codes HTTP avant/après)
4. Images d'objets (`/friend/img/items/`), page /friend mise à jour (lien vers la bande, nouveautés)
5. v2.0.0 : build, `deploy-friend.sh` (+ `version.json`), mise à jour de la machine de Louann

## Points d'attention
- La v1.0.0 déjà distribuée ne sait pas se mettre à jour : les amis qui l'ont doivent retélécharger une fois
