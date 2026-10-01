# Chunk 01 — Updater

**Statut** : done — Parent : [plan.md](plan.md)

## Fichiers
- `src/Updater.cs` (nouveau) — vérification, téléchargement, vérification SHA-256, remplacement, relance
- `src/Program.cs` — `--updated` : attente du mutex ; nettoyage de `Pepin.old.exe`
- `src/App.cs` — déclenchement (lancement + 24 h), fermeture propre quand une mise à jour est prête
- `src/Behaviors.cs` — `NewShell` (animation après mise à jour)
- `deploy-friend.sh` — écrit `version.json`

## Points d'attention
- Jamais sur le thread UI (HttpClient) ; résultat remonté par `PostMessageW`
- Ne pas s'auto-mettre à jour en `dotnet run` (JIT) : test `RuntimeFeature.IsDynamicCodeSupported`
- Comparer les versions (`System.Version`) : ne jamais « mettre à jour » vers une version inférieure
- Renommer un exe en cours d'exécution est permis sous Windows ; le supprimer ne l'est pas
- Sauvegarder l'état avant de quitter
