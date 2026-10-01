# Pépin — compagnon de bureau

Une petite tortue en pixel art qui vit par-dessus toutes tes fenêtres. Paresseuse et gourmande :
elle se promène, grignote, fait la sieste, rêve de fraises, et de temps en temps (rarement)
s'en prend à ta souris.

Exe natif de ~5 Mo, ~7 Mo de RAM privée, quasi aucun CPU au repos. Se met à jour tout seul.

## Lancer
```
publish\Pepin.exe
```
Une seule instance à la fois. L'icône de tête de tortue près de l'horloge donne accès au menu.
Au premier lancement il s'inscrit au démarrage de Windows (décochable dans le menu).

Page de téléchargement publique : https://pommetortue.tech/friend/

## Interagir
| Geste | Réaction |
|---|---|
| Frotter doucement la souris sur lui | câlin : yeux ^^ (ou cœurs s'il t'adore), rougit, cœurs. Trop longtemps → il en a marre |
| 1 clic | sursaute (ou rigole s'il t'aime beaucoup) |
| 2-3 clics rapides | « hé ! » |
| 4 clics rapides ou plus | colère (rouge qui monte, vapeur) — et parfois il mord ta souris |
| Glisser | tu le prends dans tes mains (il gigote) |
| Glisser + lâcher en lançant | il rentre dans sa carapace, tournoie, rebondit sur les bords… puis il est sonné |
| Cliquer pendant qu'il dort | réveil grognon (souvent il se rendort) |
| Clic droit sur lui | même menu que l'icône |

Il remarque aussi : quand tu es inactif (> 3 min il s'ennuie, > 8 min il dort), quand tu reviens
(il te dit bonjour), et quand la souris file à toute vitesse près de lui (sursaut ou cachette).

## Menu
État et jauges · **Carnet** (lien, journée, dernières entrées, carnet en ligne) · **La bande** (envoyer en visite au hasard
ou chez quelqu'un, avec un petit mot, visites spontanées, accepter les petits mots, renommer, bloquer un visiteur, voir la
bande en ligne) · réveiller / mettre au lit · l'appeler ici · taille (petit / moyen / grand, adaptée au zoom Windows) ·
pause (le cacher) · lancer au démarrage de Windows · version · quitter.

## Les fenêtres et tes applis
Il grimpe sur le bord des fenêtres (et tombe si tu les secoues, réduis ou fermes), joue à cache-cache derrière (ses pixels
sont gommés : il reste toujours au premier plan), et pousse rarement une fenêtre de quelques pixels — jamais une fenêtre
maximisée, et il s'arrête dès que tu bouges. Il regarde tes vidéos, stresse pendant un rendu Houdini/Blender/…, pique du nez
quand tu tapes longtemps. Détection 100 % locale : rien n'est envoyé, rien d'applicatif n'apparaît dans le carnet public.

## Le lien et le carnet
Le lien se construit sur des semaines (câlins, temps passé ensemble, jeux, cadeaux, visites ; les lancers et les clics en
rafale l'abîment ; une longue absence l'effrite un peu). Paliers : Nouvelle tortue → Copain → Ami → Meilleur ami →
Inséparable, qui débloquent : te suivre, dormir contre ta souris, t'apporter des cadeaux, fêter ton retour, être triste
quand tu pars. Le journal note les moments marquants et un bilan quotidien.

## La bande
Toutes les tortues téléchargées depuis /friend se connaissent (serveur `server/` sur le VPS). Inscription anonyme au
premier lancement avec un nom tiré au sort. Visites spontanées (rares) ou à la demande : la tortue quitte vraiment ton
écran, joue chez l'ami (reniflage, chat, goûter, sieste, danse) et rentre avec un souvenir. Carnets publics :
`/friend/t/<id>`, liste : `/friend/bande`.

## Personnalité et jauges
Énergie, faim, humeur, affection (0-100 %) évoluent en continu et sont sauvegardées dans
`%APPDATA%\Pepin\state.json`. Elles pondèrent ses choix : fatigué → siestes ; faim → grignote ;
humeur basse → boude ; affection haute → vient réclamer des câlins. La nuit, il dort plus.

## Compiler
Prérequis : SDK .NET 10 + Visual Studio avec la charge « Développement Desktop en C++ » (pour NativeAOT).
```
.\build.ps1
```

## Publier une nouvelle version
1. Monter `<Version>` dans `Pepin.csproj`
2. `.\build.ps1`
3. `bash deploy-friend.sh` (page + images + exe + `version.json` vers `/var/www/friend` sur le VPS)

Les tortues installées récupèrent la nouvelle version d'elles-mêmes (vérification au lancement puis toutes les 24 h).
Serveur de la bande : voir [server/summary.md](server/summary.md).

Les images de la page se régénèrent avec `dotnet run -- --frames site/img`.

## Outils de dev
```
dotnet run -- --sheet planche.png   # toutes les poses et expressions sur une image
dotnet run -- --frames site/img     # sprites et souvenirs transparents pour le site
dotnet run -- --simulate 3          # 3 h de vie simulée + une visite à deux simulée : comportements, anomalies
dotnet run -- --windows             # fenêtres vues, perches, applis détectées (diagnostic)
dotnet run -- --bandtest <id>       # une tortue de test rend visite à <id> (instance réelle) et attend son retour
```

## Structure
Voir [summary.md](summary.md) et le plan dans [docs/plans/MASTER.md](docs/plans/MASTER.md).
