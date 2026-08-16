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

Le mod ajoute également deux raccourcis clavier :

- `M` : vendre au meilleur marchand ;
- `N` : publier au marché aux puces.

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

## Utilisation avec UIFixes

Lorsque UIFixes 2.5 ou plus récent est installé, l’intégration de la sélection multiple est activée automatiquement :

1. Sélectionnez plusieurs objets de la réserve avec `Maj` + clic ou avec une zone de sélection.
2. Faites un clic droit sur l’un des objets sélectionnés, puis choisissez `QuickSell (Trader)` ou `QuickSell (Flea)`.
3. Vous pouvez aussi appuyer sur `M` ou `N` pendant que la sélection est active, sans avoir à survoler un objet.

Une seule confirmation est affichée pour l’ensemble de la sélection, puis les opérations sont exécutées l’une après l’autre.

Pour forcer l’activation ou la désactivation de cette intégration, ajoutez la propriété `EnableUIFixesIntegration` avec la valeur `true` ou `false` dans `config.json`.

## Configuration

Le fichier `BepInEx/plugins/QuickSell/config.json` accepte les options suivantes :

| Option | Valeur par défaut | Description |
| --- | --- | --- |
| `EnableQuickSellFlea` | `true` | Active la vente rapide au marché aux puces. |
| `EnableQuickSellTraders` | `true` | Active la vente rapide aux marchands. |
| `ShowConfirmationDialog` | `true` | Affiche une confirmation avant chaque vente. |
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
