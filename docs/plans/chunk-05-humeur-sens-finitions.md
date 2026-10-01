# Chunk 05 — Humeur, sens, finitions

**Statut** : done — Parent : [MASTER.md](MASTER.md)

## Fichiers
- `src/Mood.cs` — jauges + réglages, sauvegarde JSON (source generator), rattrapage du temps hors ligne
- `src/Pet.cs` (stimuli) — caresses, clics, sursaut, inactivité
- `src/App.cs` (menu) — état, réveiller/endormir, appeler, taille, pause, démarrage auto (HKCU\…\Run), quitter
- `README.md`, `summary.md`

## Jauges (0..1)
| Jauge | Monte | Descend |
|---|---|---|
| Énergie | sommeil | éveil (plus vite la nuit) |
| Faim | le temps (~1 h) | grignoter |
| Humeur | caresses, repas, attraper le curseur | lancers, clics en rafale, réveil forcé, négligence |
| Affection | caresses, jeux | lancers, longue absence (lente) |

## Stimuli
- **Caresse** : curseur qui bouge doucement sur la tortue ; tolérance = 6 s + 30 s × affection, au-delà il en a marre
- **Clic** : 1 = surpris (ou content si affection haute) ; 2-3 = « hé ! » ; ≥ 4 en 2,5 s = colère (et peut mordre le curseur)
- **Souris brusque** à proximité : sursaut ou cachette ; s'il dort, peut se réveiller grognon
- **Inactivité** : > 3 min ennui (bâille, se pose), > 8 min s'endort ; au retour de l'utilisateur il le remarque

## Points d'attention
- Sauvegarde toutes les 60 s + à la fermeture ; fichier corrompu → valeurs par défaut
- Hors ligne : considéré endormi (énergie pleine), faim plafonnée, affection −0,05/jour
