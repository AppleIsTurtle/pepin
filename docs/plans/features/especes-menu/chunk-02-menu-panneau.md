# Chunk 02 — Menu panneau de jeu

**Statut** : done (2026-10-01) — pages rendues en PNG (`--preview` : `menu.png`, `menu-etats.png`), branché à la place du menu natif (supprimé, P/Invoke inutiles retirés), compile ; **pas encore essayé en vrai** (clics, focus, DPI) — Parent : [plan.md](plan.md)

## But
Remplacer le menu Win32 natif (`App.ShowMenu`) par un panneau pixel art façon écran d'accueil de jeu mobile.
Le ludique en grand, les réglages en petit.

## Maquette validée (2026-10-01)
```
┌──────────────────────────┐
│ [portrait] PISTOU   ★★☆  │   portrait animé de l'espèce, nom, palier d'attachement
│ ♥▓▓▓▓░  ⚡▓▓▓░░  🍓▓▓░░░  │   affection, énergie, faim (jauges Mood)
├────────────┬─────────────┤
│  🎳 BOWLING │  🌱 HERBE    │   grosses tuiles colorées
├────────────┼─────────────┤
│  ✉ VISITE  │  📖 COLLEC.  │
├────────────┴─────────────┤
│  💤 dodo   📍 viens ici    │   actions secondaires
├──────────────────────────┤
│ ⚙  ⏸  ↕  🔄  ✕   v3.0.0   │   petites icônes : réglages, pause, taille, démarrage auto, quitter, version
└──────────────────────────┘
```
Les emojis de la maquette deviennent des **glyphes pixel** (dans `Glyphs.cs`), aucune police couleur.

## Pages
- **Accueil** (ci-dessus). Tuile grisée + petit cadenas quand l'action est impossible (mêmes conditions que
  `canPlay` aujourd'hui : partie ou visite en cours, pause, départ).
- **Visite** (tuile ✉) : membres de la bande disponibles (nom, en ligne, amitié), « au hasard »,
  « avec un petit mot… », « voir sa collection » ; bloquer le visiteur actuel ; lien « la bande en ligne ».
- **Collection** (tuile 📖) : ouvre la `DeckView` existante.
- **Réglages** (⚙) : taille (petit/moyen/grand), démarrage auto, visites spontanées, petits mots acceptés,
  jeux spontanés, renommer, version, quitter. Cases à cocher pixel.
- Bouton retour sur les sous-pages.

## Fichiers
- `src/MenuPanel.cs` (nouveau) — fenêtre layered **cliquable** (pas `WS_EX_TRANSPARENT`), `TOPMOST`, `TOOLWINDOW` ;
  rendu `PixelCanvas` + `PixelFont` à l'échelle DPI ; survol (tuile qui s'éclaire et monte d'1 px), appui (descend),
  animation d'ouverture (rebond ~120 ms) ; pages ; renvoie l'action choisie à `App` via callback.
- `src/Glyphs.cs` — icônes ~9×9 / 12×12 : quille, pousse, enveloppe, livre, lune/Z, épingle, engrenage, pause,
  flèches taille, boucle, croix, cadenas, case cochée/vide, flèche retour, étoile pleine/vide, cœur, éclair, fraise.
- `src/PixelFont.cs` — vérifier les caractères nécessaires (chiffres, « … », apostrophe) ; ajouter une taille
  moyenne pour les titres de tuiles si la police actuelle ne suffit pas.
- `src/App.cs` — `ShowMenu` ouvre le panneau : près de l'animal (clic droit sur lui) ou au-dessus de la zone de
  notification (clic sur l'icône), toujours entièrement visible sur l'écran courant ; déplacer chaque branche du
  `switch` actuel dans une méthode nommée et la brancher sur le panneau ; supprimer l'ancien menu natif.
- `src/Preview.cs` — `--preview` génère aussi `menu-accueil.png`, `menu-visite.png`, `menu-reglages.png`.

## Vérification
1. `--preview` : les 3 pages en PNG, lisibles à l'échelle 3 ; envoyées à Louann pour avis **avant** le branchement.
2. En vrai (lancé via `explorer.exe`) : clic droit sur l'animal, clic sur l'icône, Échap, clic dehors, chaque action.
3. Écran 4K à 150 % : le panneau suit le DPI, ne déborde pas, pas flou.

## Points d'attention
- Pour se fermer au clic dehors il faut le focus : `SetForegroundWindow` à l'ouverture, fermeture sur
  `WM_ACTIVATE`/`WA_INACTIVE`. Ne pas garder le focus après fermeture.
- Panneau détruit à la fermeture (RAM) ; aucune image renvoyée sans survol ni animation.
- Le portrait utilise `SpeciesArt` (chunk 01) : vraie espèce, humeur du moment.
- Pendant la phase œuf (chunk 03) : page d'accueil réduite (pas de tuiles de jeu).
