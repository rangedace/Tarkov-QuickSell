# QuickSell

QuickSell est un mod client pour Single Player Tarkov qui permet de vendre rapidement les objets de la réserve.

## Compatibilité

- SPT 4.1.x, compilé et testé avec SPT 4.1.2
- UIFixes 2.5 ou plus récent (facultatif, pour la sélection multiple)

Les versions SPT 4.0.x et antérieures ne sont pas compatibles avec cette version du mod.

## Fonctionnalités

Deux actions sont ajoutées au menu contextuel des objets de la réserve :

- `QuickSell (Trader)` vend l’objet au marchand qui propose le meilleur prix ;
- `QuickSell (Flea)` publie l’objet au marché aux puces au prix moyen affiché dans la fenêtre de mise en vente.

Le mod ajoute également deux raccourcis « meilleur prix ». Ils comparent l’offre du meilleur marchand au bénéfice net du marché aux puces, après déduction des frais :

- `M` : choisir automatiquement la destination la plus rentable, puis demander confirmation ;
- `N` : choisir automatiquement la destination la plus rentable et vendre immédiatement, sans confirmation.

Une fenêtre de confirmation affiche le montant total avant la vente. Pour le marché aux puces, elle indique aussi les frais de mise en vente et le bénéfice net estimé.

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
| `EnableQuickSellTraders` | `true` | Active la vente rapide aux marchands. |
| `ShowConfirmationDialog` | `true` | Affiche une confirmation pour les ventes lancées depuis le menu contextuel. Le raccourci `M` confirme toujours et `N` ne confirme jamais. |
| `TradersBlacklist` | `[]` | Liste des noms de marchands à ignorer. |
| `AvgPricePercent` | `100` | Pourcentage du prix moyen utilisé au marché aux puces. |
| `IgnoreFleaCapacity` | `false` | Ignore la vérification du nombre maximal d’offres. |
| `DisableKeybinds` | `false` | Désactive les raccourcis `M` et `N`. |
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
