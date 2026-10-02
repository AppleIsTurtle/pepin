# src/

Pipeline d'une image : `App.Tick` → (fenêtres, applis, bande) → `Creature.Tick` → `Senses.Update` → `Pet.Update`
(stimuli → vie → comportement → automatismes) → `SpeciesArt.Draw(Visual, Species)` → `PixelCanvas` → gommage éventuel
(`Pet.Occluder`) → DIB → `UpdateLayeredWindow`.

Plans : chunks 01-06 dans `docs/plans/`, v2 dans `docs/plans/features/` (mise-a-jour, fenetres, evolution, bande, jeux), v3 dans `docs/plans/features/especes-menu/`.

| Fichier | Rôle | Dépend de | Plans |
|---|---|---|---|
| `Program.cs` | point d'entrée : DPI per-monitor, instance unique (mutex, attente après mise à jour), `--egg [auto]` (œuf de test, rien n'est sauvegardé), outils de dev `--sheet [espece]`, `--sheet-all`, `--frames`, `--simulate`, `--windows`, `--bandtest` ; `--species`/`--guest` forcent l'espèce | App, SheetRenderer, Simulator, WinDebug | 01, mise-a-jour |
| `Native.cs` | P/Invoke Win32 de base (fenêtre, GDI, texte GDI, notification) | — | 01, bande/02 |
| `NativeWin.cs` | P/Invoke fenêtres/DWM/processus + helpers (`VisibleBounds`, `IsCloaked`, `CoversMonitor`) | — | fenetres/01 |
| `App.cs` | chef d'orchestre : boucle, timer adaptatif, tortue de la maison + visiteur, bulles, perception, bande (évènements, envoi en visite, carnet), mise à jour auto, icône et menu en panneau (modèle `BuildMenuModel`, actions `OnMenu`) | tout | 01, 05, 06, v2 |
| `Creature.cs` | une tortue affichée : `Pet` + fenêtre layered, rendu, gommage derrière une fenêtre, test de clic, souris (clic, drag, lancer) | Pet, TurtleArt, Native | bande/02 |
| `Label.cs` | bulle de texte en fenêtre layered traversable (nom du visiteur, petit mot, parole, « en visite chez… »), texte GDI | Native | bande/02 |
| `InputDialog.cs` | petite fenêtre de saisie modale Win32 (petit mot, renommer) | Native | bande/02 |
| `PixelCanvas.cs` | grille ARGB prémultipliée + calques, contour, export PNG (transparent possible) | — | 02 |
| `Glyphs.cs` | palette + motifs texte (cœur, colère, Z, notes, objets, larme, étincelles…) ; `ForItem` | PixelCanvas, Life (Item) | 02, evolution |
| `Visual.cs` | description d'une image : pose, yeux, bouche, effets, teinte, rougeur, objet tenu | Life (Item) | 02 |
| `Species.cs` | les 6 espèces (enum, codes stables, noms avec article), `SpeciesTraits` (vitesse, perches, ancre de tête, mode de long trajet) | — | especes-menu/01 |
| `SpeciesArt.cs` | point d'entrée du dessin : délègue au dessinateur de l'espèce (`Draw`, `DrawIcon`) | ArtKit, *Art | especes-menu/01 |
| `ArtKit.cs` | repère sprite partagé, visage, effets, ombre, nourriture tenue, helpers d'icône (extraits de `TurtleArt`) | PixelCanvas, Glyphs, Visual | especes-menu/01 |
| `TurtleArt.cs` | corps de la tortue (pattes, tête, carapace) et son icône | ArtKit | 02, especes-menu/01 |
| `HerissonArt.cs` | hérisson : dôme de piquants, museau, boule roulante, piquants hérissés (`Puffed`) | ArtKit | especes-menu/04 |
| `GrenouilleArt.cs` | grenouille : yeux en bosses, grande bouche, bonds (pattes étirées), glissade à plat ventre, gorge gonflée | ArtKit | especes-menu/05 |
| `EscargotArt.cs` | escargot : coquille en spirale, yeux au bout des antennes (longueur selon l'humeur), pied qui ondule | ArtKit | especes-menu/06 |
| `SnailTrail.cs` | traînée argentée de l'escargot, fenêtre traversable sous lui, s'efface en 7 s | Overlay | especes-menu/06 |
| `PandaRouxArt.cs` | panda roux : masque, oreilles, queue annelée, dressé bras levés (`Stand`), sommeil enroulé dans la queue | ArtKit | especes-menu/07 |
| `AxolotlArt.cs` | axolotl : flotte, branchies dont l'angle et le battement suivent l'humeur, grand sourire, enroulé | ArtKit | especes-menu/08 |
| `Senses.cs` | curseur (position, vitesse, vitesse de lancer), inactivité système, zone de travail | Native | 03 |
| `WindowWorld.cs` | fenêtres réelles visibles (ordre Z, cadre DWM, maximisée/plein écran), perches (`PerchSpot`), recouvrement, `MoveBy` | NativeWin | fenetres/01 |
| `AppWatch.cs` | premier plan (vidéo / créatif), rendu 3D en cours (CPU), série de frappe déduite sans hook ; 100 % local | NativeWin | fenetres/01 |
| `WinDebug.cs` | outil de dev `--windows` | WindowWorld, AppWatch | fenetres/01 |
| `Mood.cs` | jauges + réglages + `LifeData`, sauvegarde JSON, rattrapage hors ligne | Life | 05 |
| `Life.cs` | objets (`Item`), lien et paliers, compteurs du jour/totaux, collection, journal (entrées privées jamais envoyées), identité dans la bande | — | evolution |
| `Pet.cs` | monde, physique, stimuli, vie (gains de lien, paliers, fin de rendu), cerveau pondéré extensible (écarts de personnalité par espèce), réflexes d'espèce, marche par bonds, visiteur, parole, hors écran | Behaviors*, Mood, Senses, Visual, Life, Band | 03-05, v2 |
| `Behaviors.cs` | les 27 comportements de base | Pet, Visual | 04 |
| `Behaviors.Windows.cs` | `Perch`, `Fall`, `HideBehind`, `PushWindow`, `WatchVideo`, `RenderWorry`, `KeyboardDoze` | Pet, WindowWorld, AppWatch | fenetres/02-03 |
| `Behaviors.Life.cs` | `Hatched` (sortie de l'œuf), `LevelUp`, `FollowYou`, `NapByCursor`, `BringGift`, `SadGoodbye`, `JoyDance` | Pet, Life | evolution |
| `Behaviors.Social.cs` | `LeaveForVisit`, `LeaveScreen`, `AwayOnVisit`, `Arrive`, `ComeBack`, `NewShell`, activités à deux (`Sniff`, `TagGame`, `ShareSnack`, `NapTogether`, `DanceTogether`), `WatchFriend`, `Goodbye` | Pet, Band | bande/02 |
| `Overlay.cs` | fenêtre layered en gros pixels (décor traversable, cliquable ou activable avec souris en coordonnées de toile : quilles, herbe, cartes, menu), rangée sous la tortue | Native, PixelCanvas | jeux/01, especes-menu/02 |
| `Bowling.cs` | mini-jeu de bowling : piste, 6 quilles, physique, phases, abandon | Overlay, Pet, Glyphs | jeux/01 |
| `Behaviors.Species.cs` | comportements propres : `SniffGround` (hérisson), `CatchFly`, `Croak` (grenouille) | Pet | especes-menu/04-05 |
| `Behaviors.Games.cs` | `BowlSetup`, `BowlWait`, `BowlReact`, `NoticeTuft`, `WatchCritter` | Pet, Bowling, Grass | jeux/01-02 |
| `Grass.cs` | évènement « touffe d'herbe » : pousse, clic = rien / objet / petite bête | Overlay, Glyphs, Life | jeux/02 |
| `MenuPanel.cs` | menu en panneau pixel art (bandeau Discord, accueil à tuiles groupées jouer / visiter / collection / soins, pages Bande, Carnet, Réglages ; `Bounds`/`Reopen` pour y revenir depuis la collection), survol/appui/pop d'ouverture, se ferme au clic dehors ou Échap ; `MenuModel` = instantané affiché | Overlay, PixelFont, Glyphs, SpeciesArt | especes-menu/02 |
| `DeckView.cs` | carte de collection (la nôtre ou celle d'un autre compagnon), 16 cases, tête de l'espèce dans le coin ; s'ouvre au centre du menu et le rouvre à la fermeture | Overlay, PixelFont, Glyphs | jeux/03 |
| `PixelFont.cs` | police pixel-art en capitales accentuées pour les cartes | PixelCanvas | jeux/03 |
| `Preview.cs` | outil de dev `--preview` : PNG de la carte, de l'herbe, d'un lancer de bowling simulé et des pages du menu (`menu.png`, `menu-etats.png`) | DeckView, Grass, Bowling | jeux |
| `Egg.cs` | première installation : œuf qui tombe, tremble, se fissure à chaque tape (3-5), éclosion (gonfle, flash, éclats, rayons, bannière « C'est un… ») ; `DrawShell`/`DrawIcon` pour le menu et l'icône | Overlay, PixelFont, Glyphs, Species | especes-menu/03 |
| `Visit.cs` | `HostVisit` : visites reçues, sans limite de nombre (`Live` ; au-delà de `MaxShown` fenêtres les visiteurs « font foule » sans fenêtre) ; notre tortue joue avec un seul visiteur à la fois (`holder`), les autres se promènent ; arrivée, programme d'activités, au revoir, souvenir, fin ; le visiteur garde son espèce | Creature, Band, Label | bande/02 |
| `Band.cs` | client de la bande : thread réseau, inscription, heartbeat + évènements acquittés (avec l'espèce, `SpeciesCode`), visites, renommer, bloquer, carnet ; DTO JSON (source generator) | Updater (Net) | bande/02 |
| `Updater.cs` | `Net` (HttpClient partagé), mise à jour auto : `version.json`, SHA-256, remplacement de l'exe, relance | — | mise-a-jour |
| `SheetRenderer.cs` | outils de dev : planche PNG (`--sheet`), sprites et objets transparents pour le site (`--frames`) | TurtleArt | 02, 06 |
| `Simulator.cs` | outils de dev : vie simulée + visite à deux simulée (`--simulate`), visite réelle de bout en bout (`--bandtest`) | Pet, Band | 04, v2 |
