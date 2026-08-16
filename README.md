# QuickSell

QuickSell est un mod client pour Single Player Tarkov qui permet de vendre rapidement les objets de la réserve.

## Compatibilité

- SPT 4.1.x, compilé et testé avec SPT 4.1.2
- UIFixes 2.5 ou plus récent (facultatif, pour la sélection multiple)

Les versions SPT 4.0.x et antérieures ne sont pas compatibles avec cette version du mod.

## Fonctionnalités

Une action est ajoutée au menu contextuel des objets de la réserve :

- `QuickSell (Flea)` publie l’objet au marché aux puces au prix moyen affiché dans la fenêtre de mise en vente.

Le mod ajoute également deux raccourcis pour le marché aux puces :

- maintenir `M` + clic molette : publier au marché aux puces après confirmation ;
- maintenir `N` + clic molette : publier immédiatement au marché aux puces, sans confirmation.

Une fenêtre de confirmation affiche le montant total avant la vente. Pour le marché aux puces, elle indique aussi les frais de mise en vente et le bénéfice net estimé.

Si le nombre maximal d’offres est atteint, une notification affiche « Tu as trop d’offres en cours » en bas de l’écran.

## Installation

1. Fermez SPT et le jeu.
2. Copiez le dossier `QuickSell` dans `BepInEx/plugins` à la racine de votre installation SPT.
3. Vérifiez que les fichiers suivants existent :

   ```text
   BepInEx/plugins/QuickSell/QuickSell.dll
   BepInEx/plugins/QuickSell/config.json
   ```

4. Lancez SPT normalement.

Le mod peut être ajouté ou retiré d’un profil existant : il ne modifie pas les données du profil.

## Configuration

Le fichier `BepInEx/plugins/QuickSell/config.json` accepte les options suivantes :

| Option | Valeur par défaut | Description |
| --- | --- | --- |
| `EnableQuickSellFlea` | `true` | Active la vente rapide au marché aux puces. |
| `ShowConfirmationDialog` | `true` | Affiche une confirmation pour les ventes lancées depuis le menu contextuel. `M` + clic molette confirme toujours et `N` + clic molette ne confirme jamais. |
| `AvgPricePercent` | `100` | Pourcentage du prix moyen utilisé au marché aux puces. |
| `DisableKeybinds` | `false` | Désactive les raccourcis `M`/`N` + clic molette. |
| `EnableUIFixesIntegration` | détection automatique | Force l’intégration UIFixes lorsqu’elle est définie. |

## Compilation

Le projet nécessite les DLL d’une installation SPT 4.1.x. Par défaut, il utilise `D:\SPT` :

```powershell
dotnet build .\QuickSell.sln --configuration Release
```

Pour utiliser un autre emplacement :

```powershell
dotnet build .\QuickSell.sln --configuration Release /p:SPTPath="C:\Games\SPT"
```

Pour compiler et installer automatiquement le mod :

```powershell
dotnet build .\QuickSell.sln --configuration Release /p:SPTPath="D:\SPT" /p:InstallAfterBuild=true
```

## Licence

QuickSell est distribué sous licence MIT. Consultez `LICENSE.txt` pour les détails.
