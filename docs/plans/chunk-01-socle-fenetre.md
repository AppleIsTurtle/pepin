# Chunk 01 — Socle fenêtre natif

**Statut** : done — Parent : [MASTER.md](MASTER.md)

## Fichiers
- `Pepin.csproj` — net10.0-windows, `PublishAot`, `AllowUnsafeBlocks`, `InvariantGlobalization`
- `src/Program.cs` — DPI per-monitor v2, mutex d'instance unique, arguments (`--sheet`)
- `src/Native.cs` — P/Invoke user32/gdi32/shell32/kernel32 (structs blittables, `LibraryImport`)
- `src/App.cs` — classe de fenêtre, boucle de messages, `WndProc`, timer adaptatif, `UpdateLayeredWindow`,
  souris (capture/drag/clic), icône de notification + menu

## Fonctionnalités
- Fenêtre `WS_POPUP` + `WS_EX_LAYERED | TOPMOST | TOOLWINDOW | NOACTIVATE`
- DIB 32 bpp top-down prémultiplié ; seul le déplacement est envoyé si l'image ne change pas
- Timer `SetTimer` dont l'intervalle suit la cadence demandée par le comportement
- Icône de notif dessinée à partir du sprite (pas de fichier .ico)
- Ré-ajout de l'icône si l'explorateur redémarre (`TaskbarCreated`)

## Points d'attention
- NativeAOT : pas de réflexion → JSON via source generator, callbacks via `UnmanagedCallersOnly`
- Les pixels d'alpha 0 laissent passer les clics ; l'ombre (alpha faible) capte les clics → ignorer ces clics
- Réaffirmer `HWND_TOPMOST` périodiquement (certaines apps passent devant)
