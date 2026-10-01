# Chunk 01 — Perception des fenêtres et des applis

**Statut** : done — Parent : [plan.md](plan.md)

## Fichiers (modules isolés)
- `src/NativeWin.cs` — P/Invoke fenêtres, DWM, processus
- `src/WindowWorld.cs` — liste des fenêtres réelles (ordre Z, rect DWM, maximisée/plein écran), perches
  (segments visibles des bords supérieurs), test de recouvrement, déplacement d'une fenêtre
- `src/AppWatch.cs` — catégorie du premier plan (vidéo / créatif), rendu en cours, série de frappe
- `src/WinDebug.cs` — `Pepin.exe --windows` : diagnostic console

## Points d'attention
- Filtrer les fenêtres « cloaked » (applis UWP suspendues, autres bureaux virtuels) et les nôtres
- Rect = DWMWA_EXTENDED_FRAME_BOUNDS (sans bordures invisibles) ; déplacement basé sur GetWindowRect
- Coût : Refresh ~2×/s sans allocation ; échantillonnage CPU toutes les 5 s
