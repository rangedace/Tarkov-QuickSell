using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Ragfair;
using EFT.Utilities;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UIFixesInterop;
using UnityEngine;

namespace QuickSell.Patches
{
    internal class ContextMenuPatch : ModulePatch
    {
        private static readonly FieldInfo InventoryControllerField = typeof(ItemUiContext)
            .GetField("_inventoryController", BindingFlags.NonPublic | BindingFlags.Instance);

        private static IEftSession GetSession()
        {
            var session = ItemUiContext.Instance?.Session;
            if (session == null)
            {
                Utils.SendError("IEftSession is null");
            }

            return session;
        }

        private static InventoryController GetInventoryController()
        {
            var itemUiContext = ItemUiContext.Instance;
            if (itemUiContext == null || InventoryControllerField == null)
            {
                return null;
            }

            return InventoryControllerField.GetValue(itemUiContext) as InventoryController;
        }

        private static Grid GetStashGrid(InventoryController inventoryController)
        {
            var grids = inventoryController?.Inventory?.Stash?.Grids;
            return grids != null && grids.Length > 0 ? grids[0] : null;
        }

        protected override MethodBase GetTargetMethod()
        {
            var methodInfo = typeof(SimpleContextMenu)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method =>
                    method.Name == nameof(SimpleContextMenu.Show) &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 4 &&
                    method.GetParameters()[3].ParameterType == typeof(Item));

            if (methodInfo == null)
            {
                Utils.SendError("SimpleContextMenu.Show<T> not found");
                return null;
            }

            return methodInfo.MakeGenericMethod(typeof(EItemInfoButton));
        }

        [PatchPrefix]
        private static void Prefix(
            ContextInteractions<EItemInfoButton> contextInteractions,
            Item item)
        {
            if (contextInteractions is not InventoryItemContextInteractions) return;
            if (item == null)
            {
                Utils.SendError("No item is selected");
                return;
            }

            var itemContext = (contextInteractions as BaseItemContextInteractions)?.ItemContext;
            if (itemContext == null || itemContext.ViewType != EItemViewType.Inventory) return;
            if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance is not HideoutGameWorld) return;

            var menuUi = Singleton<MenuUI>.Instantiated
                ? Singleton<MenuUI>.Instance
                : null;
            if (menuUi?.HideoutAreaTransferItemsScreen?.isActiveAndEnabled == true) return;
            if (item.GetAllParentItems().Any(parentItem => parentItem is InventoryEquipment)) return;
            if (item.Parent?.Container?.ParentItem?.TemplateId == "55d7217a4bdc2d86028b456d") return;

            var dynamicInteractions = contextInteractions._dynamicInteractions;
            if (dynamicInteractions == null || !Plugin.EnableQuickSellFlea) return;

            var unloadSprite = ResourcesCache.Pop<Sprite>("Characteristics/Icons/UnloadAmmo");
            dynamicInteractions["QuickSell (Flea)"] = new DynamicContextInteraction(
                "QuickSell (Flea)",
                "QuickSell (Flea)",
                () => SellToFlea(item),
                unloadSprite);
        }

        public static void SellToFlea(Item item)
        {
            SellToFlea(item, null);
        }

        public static void SellToFlea(Item item, bool showConfirmation)
        {
            SellToFlea(item, (bool?)showConfirmation);
        }

        private static void SellToFlea(Item item, bool? confirmationOverride)
        {
            var items = GetItemsToSell(item);
            try
            {
                var session = GetSession();
                if (session == null) return;

                var inventoryController = GetInventoryController();
                if (inventoryController == null)
                {
                    Utils.SendError("Could not load inventory");
                    return;
                }

                var stashGrid = GetStashGrid(inventoryController);
                if (stashGrid == null)
                {
                    Utils.SendError("Could not load stash grid");
                    return;
                }

                var ragFair = session.RagFair;
                if (ragFair?.Available != true)
                {
                    Utils.SendError("Flea market is not available");
                    return;
                }

                var fleaContext = new RagfairNewOfferContext(stashGrid, inventoryController);
                var validItems = items
                    .Where(selectedItem => selectedItem != null && fleaContext.HighlightedAtRagfair(selectedItem))
                    .GroupBy(selectedItem => selectedItem.Id)
                    .Select(group => group.First())
                    .ToList();
                if (validItems.Count == 0)
                {
                    Utils.SendError("No items can be sold on the flea");
                    return;
                }

                var maxOffers = ragFair.GetMaxOffersCount(ragFair.MyRating);
                if (ragFair.MyOffersCount + validItems.Count > maxOffers)
                {
                    Utils.SendError("Tu as trop d'offres en cours");
                    return;
                }

                if (validItems.Count == 1)
                {
                    SellSingleItemToFlea(validItems[0], session, ragFair, confirmationOverride);
                    return;
                }

                SellMultipleItemsToFlea(validItems, session, ragFair, confirmationOverride);
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static void SellSingleItemToFlea(
            Item item,
            IEftSession session,
            RagFair ragFair,
            bool? confirmationOverride)
        {
            ragFair.GetMarketPrices(item.TemplateId, result =>
            {
                try
                {
                    if (result == null)
                    {
                        Utils.SendError("Could not get flea price");
                        return;
                    }

                    var price = (int)Math.Ceiling(result.avg / 100.0 * Plugin.AvgPricePercent);
                    var fee = (int)Math.Ceiling(PriceCalculator.CalculateTaxPrice(
                        item,
                        item.StackObjectsCount,
                        price,
                        false));
                    ConfirmWindow(
                        () => DoFleaOffer(item, session, price),
                        "on the flea",
                        1,
                        price * item.StackObjectsCount,
                        fee,
                        confirmationOverride);
                }
                catch (Exception ex)
                {
                    Utils.SendError(ex.ToString());
                    Plugin.LogSource.LogWarning(ex.ToString());
                }
            });
        }

        private static void SellMultipleItemsToFlea(
            List<Item> items,
            IEftSession session,
            RagFair ragFair,
            bool? confirmationOverride)
        {
            var pending = items.Count;
            var prices = new Dictionary<string, int>();
            var fees = new Dictionary<string, int>();
            var lockObject = new object();

            foreach (var item in items)
            {
                ragFair.GetMarketPrices(item.TemplateId, result =>
                {
                    var completed = false;
                    lock (lockObject)
                    {
                        try
                        {
                            if (result != null)
                            {
                                var price = (int)Math.Ceiling(result.avg / 100.0 * Plugin.AvgPricePercent);
                                prices[item.Id] = price;
                                fees[item.Id] = (int)Math.Ceiling(PriceCalculator.CalculateTaxPrice(
                                    item,
                                    item.StackObjectsCount,
                                    price,
                                    false));
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.LogSource.LogWarning(ex.ToString());
                        }
                        finally
                        {
                            completed = --pending == 0;
                        }
                    }

                    if (!completed) return;

                    var pricedItems = items.Where(pricedItem => prices.ContainsKey(pricedItem.Id)).ToList();
                    if (pricedItems.Count == 0)
                    {
                        Utils.SendError("Could not get flea prices");
                        return;
                    }

                    var total = pricedItems.Sum(pricedItem =>
                        prices[pricedItem.Id] * pricedItem.StackObjectsCount);
                    var totalFee = pricedItems.Sum(pricedItem => fees[pricedItem.Id]);
                    ConfirmWindow(
                        () => ExecuteFleaSale(pricedItems, prices, session),
                        "on the flea",
                        pricedItems.Count,
                        total,
                        totalFee,
                        confirmationOverride);
                });
            }
        }

        private static void ExecuteFleaSale(
            List<Item> items,
            IReadOnlyDictionary<string, int> prices,
            IEftSession session)
        {
            var validIds = new HashSet<string>(items.Select(item => item.Id));
            Action<Item> sell = selectedItem =>
            {
                if (selectedItem != null &&
                    validIds.Contains(selectedItem.Id) &&
                    prices.TryGetValue(selectedItem.Id, out var price))
                {
                    DoFleaOffer(selectedItem, session, price);
                }
            };

            if (IsMultiSelectActive())
            {
                MultiSelect.Apply(sell, ItemUiContext.Instance);
                return;
            }

            foreach (var item in items)
            {
                sell(item);
            }
        }

        private static void DoFleaOffer(Item item, IEftSession session, int price)
        {
            try
            {
                var requirements = new List<BarterTemplate>
                {
                    new()
                    {
                        _tpl = (string)CurrencyUtil.GetCurrencyId(ECurrencyType.RUB),
                        count = price,
                        onlyFunctional = true
                    }
                };
                session.RagfairAddOffer(
                    false,
                    [item.Id],
                    [.. requirements],
                    new Callback(PlaySellSound));
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static void PlaySellSound(IResult result)
        {
            if (result.Succeed)
            {
                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
            }
        }

        private static bool IsMultiSelectActive()
        {
            return Plugin.EnableUIFixesIntegration && MultiSelect.Count > 0;
        }

        internal static List<Item> GetItemsToSell(Item item)
        {
            if (item == null) return new List<Item>();
            if (!Plugin.EnableUIFixesIntegration) return new List<Item> { item };
            if (MultiSelect.Count > 0) return MultiSelect.Items.ToList();
            return new List<Item> { item };
        }

        public static void ConfirmWindow(
            Action callback,
            string source,
            int count,
            int? totalPrice = null,
            int? fee = null,
            bool? confirmationOverride = null)
        {
            var showConfirmation = confirmationOverride ?? Plugin.ShowConfirmationDialog;
            if (!showConfirmation)
            {
                callback();
                return;
            }

            var itemUiContext = ItemUiContext.Instance;
            if (itemUiContext == null)
            {
                callback();
                return;
            }

            var baseMessage = count == 1
                ? $"Are you sure you want to sell this item {source}?".Localized()
                : $"Are you sure you want to sell {count} items {source}?".Localized();
            string message;
            if (totalPrice.HasValue && fee.HasValue && fee.Value > 0)
            {
                var profit = totalPrice.Value - fee.Value;
                message = $"{baseMessage}\n\nListing Fee: {fee.Value:N0} ₽\nNet Profit: {profit:N0} ₽";
            }
            else if (totalPrice.HasValue)
            {
                message = count == 1
                    ? $"Are you sure you want to sell this item {source} for {totalPrice.Value:N0} ₽?".Localized()
                    : $"Are you sure you want to sell {count} items {source} for {totalPrice.Value:N0} ₽ total?".Localized();
            }
            else
            {
                message = baseMessage;
            }

            itemUiContext.ShowMessageWindow(
                message,
                callback,
                () => { },
                null,
                0f,
                false,
                TextAlignmentOptions.Center);
        }
    }
}
