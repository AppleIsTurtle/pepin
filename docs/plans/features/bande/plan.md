# Feature — La bande

Parent : [../../MASTER.md](../../MASTER.md)

## Point de départ
Chaque tortue vit seule sur sa machine.

## Objectif
Toutes les tortues téléchargées depuis /friend forment une seule bande : elles se rendent visite
(spontanément, rarement, ou à la demande), jouent ensemble, rapportent des souvenirs, portent de petits
mots, et chacune a un carnet public.

## Décisions
| Sujet | Choix |
|---|---|
| Serveur | FastAPI + SQLite en Docker sur le VPS (`louann-bande`, 127.0.0.1:port libre), derrière nginx `/friend/api/`, `/friend/t/`, `/friend/bande` |
| Identité | inscription anonyme au 1er lancement : id + jeton secret + nom aléatoire (renommable) |
| Synchro | heartbeat toutes les 60 s (10 s pendant une visite), évènements acquittés |
| Visite | la tortue part vraiment (fenêtre cachée, mot près de l'icône) ; chez l'hôte, une 2e tortue apparaît avec son nom |
| Pendant la visite | activités à deux : se renifler, chat, goûter, sieste, danse ; souvenir rapporté ; petit mot (80 car.) |
| Garde-fous | bloquer une tortue, refuser les mots, limites de débit, échappement HTML, rien sur les applis |

## Chunks
| # | Fichier | Statut |
|---|---|---|
| 01 | [chunk-01-serveur.md](chunk-01-serveur.md) | done |
| 02 | [chunk-02-client-visites.md](chunk-02-client-visites.md) | done |
| 03 | [chunk-03-deploiement-v2.md](chunk-03-deploiement-v2.md) | done |
