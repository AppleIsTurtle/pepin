# src/

Pipeline d'une image : `App.Tick` → (fenêtres, applis, bande) → `Creature.Tick` → `Senses.Update` → `Pet.Update`
(stimuli → vie → comportement → automatismes) → `TurtleArt.Draw(Visual)` → `PixelCanvas` → gommage éventuel
(`Pet.Occluder`) → DIB → `UpdateLayeredWindow`.

Plans : chunks 01-06 dans `docs/plans/`, v2 dans `docs/plans/features/` (mise-a-jour, fenetres, evolution, bande).

| Fichier | Rôle | Dépend de | Plans |
|---|---|---|---|
| `Program.cs` | point d'entrée : DPI per-monitor, instance unique (mutex, attente après mise à jour), outils de dev `--sheet`, `--frames`, `--simulate`, `--windows`, `--bandtest` | App, SheetRenderer, Simulator, WinDebug | 01, mise-a-jour |
| `Native.cs` | P/Invoke Win32 de base (fenêtre, GDI, texte GDI, notification) | — | 01, bande/02 |
| `NativeWin.cs` | P/Invoke fenêtres/DWM/processus + helpers (`VisibleBounds`, `IsCloaked`, `CoversMonitor`) | — | fenetres/01 |
| `App.cs` | chef d'orchestre : boucle, timer adaptatif, tortue de la maison + visiteur, bulles, perception, bande (évènements, envoi en visite, carnet), mise à jour auto, icône et menu (carnet, bande, taille, pause, démarrage auto) | tout | 01, 05, 06, v2 |
| `Creature.cs` | une tortue affichée : `Pet` + fenêtre layered, rendu, gommage derrière une fenêtre, test de clic, souris (clic, drag, lancer) | Pet, TurtleArt, Native | bande/02 |
| `Label.cs` | bulle de texte en fenêtre layered traversable (nom du visiteur, petit mot, parole, « en visite chez… »), texte GDI | Native | bande/02 |
| `InputDialog.cs` | petite fenêtre de saisie modale Win32 (petit mot, renommer) | Native | bande/02 |
| `PixelCanvas.cs` | grille ARGB prémultipliée + calques, contour, export PNG (transparent possible) | — | 02 |
| `Glyphs.cs` | palette + motifs texte (cœur, colère, Z, notes, objets, larme, étincelles…) ; `ForItem` | PixelCanvas, Life (Item) | 02, evolution |
| `Visual.cs` | description d'une image : pose, yeux, bouche, effets, teinte, rougeur, objet tenu | Life (Item) | 02 |
| `TurtleArt.cs` | dessine la tortue et ses effets ; icône de notification | PixelCanvas, Glyphs, Visual | 02 |
| `Senses.cs` | curseur (position, vitesse, vitesse de lancer), inactivité système, zone de travail | Native | 03 |
| `WindowWorld.cs` | fenêtres réelles visibles (ordre Z, cadre DWM, maximisée/plein écran), perches (`PerchSpot`), recouvrement, `MoveBy` | NativeWin | fenetres/01 |
| `AppWatch.cs` | premier plan (vidéo / créatif), rendu 3D en cours (CPU), série de frappe déduite sans hook ; 100 % local | NativeWin | fenetres/01 |
| `WinDebug.cs` | outil de dev `--windows` | WindowWorld, AppWatch | fenetres/01 |
| `Mood.cs` | jauges + réglages + `LifeData`, sauvegarde JSON, rattrapage hors ligne | Life | 05 |
| `Life.cs` | objets (`Item`), lien et paliers, compteurs du jour/totaux, collection, journal (entrées privées jamais envoyées), identité dans la bande | — | evolution |
| `Pet.cs` | monde, physique, stimuli, vie (gains de lien, paliers, fin de rendu), cerveau pondéré extensible, visiteur, parole, hors écran | Behaviors*, Mood, Senses, Visual, Life, Band | 03-05, v2 |
| `Behaviors.cs` | les 27 comportements de base | Pet, Visual | 04 |
| `Behaviors.Windows.cs` | `Perch`, `Fall`, `HideBehind`, `PushWindow`, `WatchVideo`, `RenderWorry`, `KeyboardDoze` | Pet, WindowWorld, AppWatch | fenetres/02-03 |
| `Behaviors.Life.cs` | `LevelUp`, `FollowYou`, `NapByCursor`, `BringGift`, `SadGoodbye`, `JoyDance` | Pet, Life | evolution |
| `Behaviors.Social.cs` | `LeaveForVisit`, `LeaveScreen`, `AwayOnVisit`, `Arrive`, `ComeBack`, `NewShell`, activités à deux (`Sniff`, `TagGame`, `ShareSnack`, `NapTogether`, `DanceTogether`), `WatchFriend`, `Goodbye` | Pet, Band | bande/02 |
| `Visit.cs` | `HostVisit` : orchestration d'une visite reçue (arrivée, programme d'activités, au revoir, souvenir, fin) | Creature, Band, Label | bande/02 |
| `Band.cs` | client de la bande : thread réseau, inscription, heartbeat + évènements acquittés, visites, renommer, bloquer, carnet ; DTO JSON (source generator) | Updater (Net) | bande/02 |
| `Updater.cs` | `Net` (HttpClient partagé), mise à jour auto : `version.json`, SHA-256, remplacement de l'exe, relance | — | mise-a-jour |
| `SheetRenderer.cs` | outils de dev : planche PNG (`--sheet`), sprites et objets transparents pour le site (`--frames`) | TurtleArt | 02, 06 |
| `Simulator.cs` | outils de dev : vie simulée + visite à deux simulée (`--simulate`), visite réelle de bout en bout (`--bandtest`) | Pet, Band | 04, v2 |
