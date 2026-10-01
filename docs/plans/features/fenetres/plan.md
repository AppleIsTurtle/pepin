# Feature — Fenêtres et applis

Parent : [../../MASTER.md](../../MASTER.md)

## Point de départ
La tortue vit « par-dessus » l'écran sans rien savoir des fenêtres.

## Objectif
Qu'elle habite le bureau : elle s'assoit sur les fenêtres, joue à cache-cache derrière, les pousse
(rarement), et réagit à ce que tu fais (vidéo, rendu 3D, frappe au clavier).

## Décisions
- Toujours au premier plan : « derrière une fenêtre » = les pixels du sprite qui recouvrent la fenêtre sont gommés
- Perché = le sol de la tortue devient le bord supérieur de la fenêtre ; il suit la fenêtre, tombe si elle
  bouge brutalement / se réduit / se ferme / se maximise
- Pousser : jamais une fenêtre maximisée/plein écran, jamais si l'utilisateur a agi depuis < 2 s, quelques
  pixels seulement ; variante « s'agrippe et fait trembler » qui remet la fenêtre exactement en place
- Détection d'applis 100 % locale (processus + titre au premier plan, CPU des logiciels 3D) ;
  frappe déduite sans hook clavier ; jamais envoyée au serveur ni écrite dans le carnet public

## Sous-parties
| # | Fichier | Statut |
|---|---|---|
| 01 | [chunk-01-monde-fenetres.md](chunk-01-monde-fenetres.md) | done |
| 02 | [chunk-02-comportements-fenetres.md](chunk-02-comportements-fenetres.md) | done |
| 03 | [chunk-03-reactions-applis.md](chunk-03-reactions-applis.md) | done |
