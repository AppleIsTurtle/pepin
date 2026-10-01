# Chunk 03 — Œuf et éclosion

**Statut** : done (2026-10-01) — `--preview` → `oeuf.png` (posé, 3 fissures, éclosion, révélation) ; `--egg auto --species herisson` tourne à l'écran sans erreur jusqu'à l'éclosion (instance de test séparée, rien d'écrit) ; **pas encore tapoté à la main**. `--egg` seul = test manuel — Parent : [plan.md](plan.md)

## But
Première installation : un œuf apparaît sur le bureau ; on le tapote pour qu'il éclose ; l'animal tiré au sort
(1/6) est révélé, avec son nom.

## Déroulé
1. Pas de `state.json` → `LifeData.Egg = true`, sauvegardé tout de suite (quitter avant l'éclosion garde l'œuf).
2. L'œuf tombe du haut de l'écran sur la zone de travail (rebond, poussière), se pose, puis **tremble** de temps
   en temps ; bulle « Tapote-moi ! » la première fois.
3. Chaque clic : petit saut + secousse, une **fissure** de plus (4 stades). Nombre de clics tiré entre 3 et 5.
   Sans clic, il tremble de plus en plus (jamais d'éclosion automatique).
4. Éclosion : l'œuf gonfle, **flash blanc**, éclats de coquille projetés (particules avec gravité), rayons de lumière
   qui tournent derrière, puis l'animal apparaît assis avec un morceau de coquille sur la tête qu'il secoue.
5. Bannière pixel « C'est un hérisson ! » puis le nom (tiré au sort par le serveur à l'inscription — déjà le cas ;
   hors ligne : nom provisoire local, remplacé à l'inscription).
6. Journal : « Est sorti de son œuf. » ; `BornUnix` = l'éclosion. Ensuite, vie normale.

## Fichiers
- `src/Egg.cs` (nouveau) — l'œuf : dessin (coquille crème à taches, 4 stades de fissures), tremblement, clics,
  particules de coquille, rayons, flash, bannière ; fenêtre layered dédiée (style `Overlay`, mais cliquable).
- `src/Life.cs` — `LifeData.Egg` ; tirage de l'espèce à l'éclosion ; entrée de journal.
- `src/Mood.cs` — distinguer « premier lancement » (fichier absent) d'un `state.json` illisible (dans ce cas,
  **pas** d'œuf : garder le comportement actuel).
- `src/App.cs` — au démarrage, si `Egg` : afficher l'œuf, pas de créature, pas d'inscription à la bande, menu réduit ;
  à l'éclosion : créer la créature avec son espèce, puis `band.Start()`.
- `src/Behaviors.Life.cs` — `Hatched` : premier comportement (secoue la coquille, regarde autour, saut de joie).
- `src/Program.cs` — `--egg` : joue l'œuf même si un état existe, **sans écrire l'état** (test).
- `src/Preview.cs` — images clés (`egg-1..4.png`, `egg-burst.png`, `egg-reveal.png`).

## Vérification
1. `--preview` : images de l'éclosion, envoyées à Louann.
2. `--egg` en vrai (via `explorer.exe`) : tremble, clics, éclosion, révélation ; l'état réel n'est pas modifié.
3. Simulation d'une première installation : renommer temporairement `%APPDATA%\Pepin` **côté Windows** (script lancé
   via `explorer.exe`, hors conteneur), lancer, faire éclore, vérifier `state.json` (espèce, `Egg` false), puis
   **remettre le dossier d'origine** (Pistou) et supprimer le compte de test côté serveur s'il a été inscrit.

## Points d'attention
- Les installations existantes ne doivent **jamais** voir d'œuf : la seule condition est l'absence du fichier d'état.
- Tirage uniforme (`Random.Shared.Next(6)`), une seule fois, persisté immédiatement.
- Pendant l'œuf : différer une mise à jour auto (`ApplyAndRestart`) pour ne pas relancer au milieu de l'éclosion.
