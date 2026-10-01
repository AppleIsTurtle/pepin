# Feature — Mini-jeux, évènements et réseau enrichi

Parent : [../../MASTER.md](../../MASTER.md)

## Point de départ
La tortue joue seulement avec la souris (cercle, attaque, poursuite), se balade et rend visite aux amis de la bande.
Le lancer (glisser + lâcher) existe déjà. La collection d'objets est envoyée au serveur mais visible seulement sur la
page web du carnet. Aucun évènement dans le monde, aucun décor autre que les fenêtres.

## Objectif
1. **Mini-jeux discrets** : le premier est un bowling où l'on lance la tortue sur des quilles.
2. **Évènements aléatoires** : une touffe d'herbe apparaît, un clic donne une chance d'objet ou d'un autre animal.
3. **Réseau** : voir le « deck » (la collection) des autres tortues de la bande depuis l'appli.

Contrainte transversale : **pas envahissant**. Tout est rare, désactivable (« Jeux » dans le menu), s'abandonne seul
si tu l'ignores, ne passe jamais devant un rendu, une vidéo, une visite ou la pause, et le décor laisse passer la souris.

## Décisions
- Décor = `Overlay` : fenêtre layered qui laisse passer les clics (`WS_EX_TRANSPARENT`), rangée juste sous la tortue
  (elle passe devant). Réutilisé par les évènements.
- **Bowling** : 6 quilles (triangle 1-2-3) posées à ~70 px logiques de la tortue, du côté de l'écran qui a le plus de
  place. Un tour = 2 lancers max ; le lancer normal en carapace (vitesse ≥ 150 px/s) compte. Physique maison :
  la carapace pousse les quilles et perd un peu d'élan, les quilles en renversent d'autres, les tombées sont balayées.
  Bilan : strike / toutes tombées / n sur 6 / raté ; lien +0,5 +0,4 par quille (+2 pour un strike), humeur, carnet
  (`bowling`, `strikes`). Aucune pénalité de lancer pendant la partie (ni vol plané ni rancune).
- Déclenchement : spontané très rare (poids 0,25, ≥ 1h30 à 3h entre deux jeux, tortue en forme, toi actif, ni vidéo
  ni rendu) ou à la demande (menu Jeux). Abandon si pas de lancer en 55 s ou partie > 5 min.
- Évènements (chunk 02) : herbe qui pousse à un endroit libre, clic → tirage (rien / objet / petit animal de passage) ;
  nouveaux objets et animaux ajoutés à la collection **et** à la liste blanche du serveur.
- Réseau (chunk 03) : `GET /api/turtle/{id}` (carnet public d'une autre tortue : palier, stats, collection) ;
  menu La bande → tortue → « Voir sa collection » ; la sienne dans Carnet.

## Chunks
| # | Fichier | Statut |
|---|---|---|
| 01 | [chunk-01-bowling.md](chunk-01-bowling.md) | code écrit, à compiler et tester |
| 02 | chunk-02-evenements.md | à faire |
| 03 | chunk-03-deck-reseau.md | à faire |
