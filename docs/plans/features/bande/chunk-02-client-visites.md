# Chunk 02 — Client : bande et visites

**Statut** : done — Parent : [plan.md](plan.md)

## Fichiers
- `src/Band.cs` (nouveau) — thread réseau : inscription, heartbeat, file d'évènements, appels visite/fin/carnet
- `src/Creature.cs` (nouveau) — une tortue + sa fenêtre layered (extrait de `App`), pour afficher un visiteur
- `src/Label.cs` (nouveau) — petite fenêtre bulle (nom du visiteur, petit mot, « partie chez … »), texte GDI
- `src/Visit.cs` (nouveau) — orchestration d'une visite reçue (arrivée, activités à deux, départ, souvenir)
  et d'une visite rendue (départ, absence, retour)
- `src/Behaviors.Social.cs` (nouveau) — `Arrive`, `LeaveScreen`, `ComeBack`, `Sniff`, `Tag`, `ShareSnack`,
  `NapTogether`, `DanceTogether`
- `src/App.cs` — plusieurs fenêtres, menu (envoyer en visite chez…, renommer, bloquer, refuser les mots, carnet)

## Points d'attention
- Réseau jamais sur le thread UI ; hors ligne = tout continue normalement
- La tortue locale reste pilotable pendant une visite (câlins, lancers) ; l'orchestrateur attend
- Visite spontanée : rare (≥ 2 h d'écart), seulement si réveillée, en forme, et utilisateur présent
