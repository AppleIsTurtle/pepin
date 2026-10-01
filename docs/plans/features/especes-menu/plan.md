# Feature — Espèces, œuf et menu de jeu (v3)

Parent : [../../MASTER.md](../../MASTER.md)

## Point de départ (v2.1.0, 2026-10-01)
- Un seul animal : la tortue, dessinée en code (`src/TurtleArt.cs`, grille ~56×54 px logiques) à partir d'un `Visual`
  dont certains champs sont propres à la tortue (`HeadOut`, `InShell`, `SpinFrame`).
- Plusieurs comportements supposent une carapace : déplacements longs en toupie, `Thrown`, bowling (elle roule au sol).
- Menu = menu Win32 natif (`App.ShowMenu`) au clic droit sur la tortue et sur l'icône de notification : texte,
  sous-menus « La bande » et « Jeux », réglages au même niveau que les actions.
- Premier lancement : la tortue apparaît directement ; le serveur lui donne un nom tiré au sort à l'inscription.
- La bande ne connaît que des tortues : visiteur dessiné en tortue, pages `/bande` et `/t/{id}` avec le sprite tortue.

## Objectif
1. **Menu de jeu** : un panneau pixel art façon écran d'accueil de jeu mobile, au clic droit sur l'animal et sur
   l'icône de la barre des tâches. Le ludique en grand (tuiles), les réglages en petit (rangée d'icônes, page dédiée).
2. **Six espèces**, une chance sur six chacune, **sans rareté ni variante** :
   tortue, hérisson, grenouille, escargot, panda roux, axolotl.
3. **Œuf à la première installation** : il apparaît sur le bureau, on le tapote 3 à 5 fois pour qu'il éclose,
   révélation de l'animal, nom tiré au sort (renommable ensuite, comme aujourd'hui).
4. **La bande multi-espèces** : chacun voit la vraie espèce du visiteur ; serveur et pages web affichent l'espèce.

## Périmètre
**Dedans**
- Base commune à toutes les espèces : tous les comportements existants (marcher, dormir, rêver, manger, se percher,
  être lancé, jouer, bowling, herbe, visites) avec une pose adaptée à chaque espèce.
- 2-3 traits propres par espèce (tableau ci-dessous) + petits écarts de personnalité (poids de `Pet.PickNext`).
- Panneau de menu complet (accueil, page « La bande », page « Réglages ») remplaçant le menu natif.
- Animation d'œuf et d'éclosion, révélation, entrée de carnet.
- Serveur : colonne espèce, API, pages web ; site /friend qui présente les 6 espèces.
- Une seule grosse version publiée à la fin (**3.0.0**), choix explicite de Louann.

**Dehors**
- Rareté, variantes de couleur (« shiny »), paliers gacha : écartés (« chances égales, pas de rareté »).
- Changer d'espèce, avoir plusieurs animaux, œufs supplémentaires à gagner.
- Œuf pour les installations existantes : **Pistou, Trèfle… restent des tortues** (pas d'œuf à la mise à jour).
- Saisie du nom à l'éclosion (nom tiré au sort, renommable depuis le menu).
- Sons.

## Les espèces
| Espèce | Base commune adaptée | Traits propres |
|---|---|---|
| Tortue | inchangée | carapace (repli), glissade en toupie (déplacement long) — déjà là |
| Hérisson | repli = boule de piquants | se roule en boule et **roule** (déplacements longs, bowling) ; piquants hérissés quand il est surpris (clic, lancer, souris brusque) ; renifle le sol |
| Grenouille | repli = accroupie aplatie | se déplace **par bonds** ; **langue** qui gobe une mouche (nouvel effet) ; **gorge qui gonfle** (coasse) |
| Escargot | repli = rentré dans la coquille | **très lent** (vitesse ×0,4, compensée par des glissades en coquille pour les longs trajets) ; antennes ; **traînée brillante** qui s'efface |
| Panda roux | repli = enroulé dans sa queue | se **dresse bras levés** (surprise, contrariété) ; **dort enroulé dans sa queue** ; adore **se percher** (poids des perches ×2) |
| Axolotl | repli = enroulé sur lui-même | **flotte** un peu au-dessus du sol (ondulation, ombre décalée) ; **branchies qui ondulent** (plus vite quand il est content) ; gros sourire |

Au bowling, chaque espèce est lancée dans sa pose de repli et glisse/roule au sol (même physique, même `Pet.FrictionScale`).

## Décisions
| Sujet | Choix | Pourquoi |
|---|---|---|
| Rendu | `SpeciesArt.Draw(canvas, visual, species)` qui délègue à un fichier par espèce ; le visage, les effets, l'ombre et la nourriture tenue sont **partagés** (extraits de `TurtleArt` dans `ArtKit`) | les 6 espèces gardent la même expressivité sans dupliquer ~200 lignes |
| `Visual` | les champs tortue gardent leur nom mais prennent un sens générique documenté : `InShell` = repli, `SpinFrame` = roulé/rotation, `HeadOut` = sortie de la tête/du corps | aucun comportement existant à réécrire |
| Taille de la toile | même grille ~56×54 pour toutes les espèces | ne touche ni la physique, ni le test de clic, ni les fenêtres |
| Persistance | `LifeData.Species` (chaîne : `tortue`, `herisson`, `grenouille`, `escargot`, `panda-roux`, `axolotl`) ; absente = `tortue` | les états existants restent des tortues sans migration ; NativeAOT : pas de réflexion sur l'enum |
| Nouvelle installation | pas de `state.json` → phase « œuf » (`LifeData.Egg = true`, persistée) jusqu'à l'éclosion ; l'inscription à la bande attend l'éclosion | quitter avant d'avoir fait éclore garde l'œuf |
| Tirage | uniforme 1/6, fait **à l'éclosion** | pas de rareté |
| Menu | fenêtre layered **cliquable** (style `DeckView`), dessinée en `PixelCanvas` + `PixelFont`, icônes en glyphes pixel ; se ferme sur Échap, clic dehors (désactivation) ou action | même techno que les cartes, zéro dépendance |
| Bande | `species` envoyée à l'inscription et dans le heartbeat (rattrape les comptes existants) ; défaut serveur `tortue` | anciens clients 2.x restent compatibles jusqu'à leur mise à jour auto |
| Version | **3.0.0** | changement visible majeur ; déclenche la mise à jour auto chez tout le monde |

## Chunks
| # | Fichier | Statut |
|---|---|---|
| 01 | [chunk-01-socle-especes.md](chunk-01-socle-especes.md) | done |
| 02 | [chunk-02-menu-panneau.md](chunk-02-menu-panneau.md) | done (à essayer en vrai) |
| 03 | [chunk-03-oeuf-eclosion.md](chunk-03-oeuf-eclosion.md) | done (à essayer en vrai) |
| 04 | [chunk-04-herisson.md](chunk-04-herisson.md) | done (planche envoyée) |
| 05 | [chunk-05-grenouille.md](chunk-05-grenouille.md) | done (planche envoyée) |
| 06 | [chunk-06-escargot.md](chunk-06-escargot.md) | done (planche envoyée) |
| 07 | [chunk-07-panda-roux.md](chunk-07-panda-roux.md) | done (planche envoyée) |
| 08 | [chunk-08-axolotl.md](chunk-08-axolotl.md) | done (planche envoyée) |
| 09 | [chunk-09-bande-especes.md](chunk-09-bande-especes.md) | done (serveur pas encore déployé) |
| 10 | [chunk-10-site-publication.md](chunk-10-site-publication.md) | done (publiée le 2026-10-01) |

Ordre : 01 d'abord (tout en dépend). 02 et 03 sont indépendants des animaux. 04-08 dans n'importe quel ordre,
chacun validé sur sa planche `--sheet` par Louann avant le suivant. 09 après au moins une nouvelle espèce. 10 en dernier.
**Rien n'est publié avant le chunk 10.**

## Règles pour tous les chunks
- Depuis une session Claude, **toujours lancer Pépin via `explorer.exe "…\publish\Pepin.exe"`** (sinon il tourne dans
  le conteneur MSIX de Claude : état et démarrage auto invisibles pour Windows). `build.ps1` tue Pépin : le relancer après.
- Ne pas piloter la souris de Louann pour tester : `--sheet`, `--preview`, `--simulate` d'abord.
- Garder la consommation : pas d'image renvoyée si identique, cadence adaptative inchangée, panneau détruit à la fermeture.
- Mettre à jour `src/summary.md` (et `server/summary.md`, `site/summary.md`) à chaque fichier ajouté/modifié.
