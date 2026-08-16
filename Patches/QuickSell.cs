using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Ragfair;
using EFT.Utilities;
using JetBrains.Annotations;
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
    // This is the main patch handling most of the logic
    internal class ContextMenuPatch : ModulePatch
    {
        private static Trader[] traders = null;
        private static readonly FieldInfo InventoryControllerField = typeof(ItemUiContext)
            .GetField("_inventoryController", BindingFlags.NonPublic | BindingFlags.Instance);

        private sealed class BestPriceChoice
        {
            public Item Item;
            public Trader Trader;
            public int TraderPrice;
            public int FleaUnitPrice;
            public int FleaFee;
            public int FleaNetPrice;
            public bool UseFlea;

            public int SelectedNetPrice => UseFlea ? FleaNetPrice : TraderPrice;
        }

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

        [CanBeNull]
        private static ITradingSession GetTraderInteractions(Trader bestTrader)
        {
            var interactions = bestTrader?._trading;
            if (interactions == null)
            {
                Utils.SendError("ITradingSession is null for the provided trader instance");
            }

            return interactions;
        }

        public static void SellBestPrice(Item item, bool showConfirmation)
        {
            var items = GetItemsToSell(item)
                .Where(selectedItem => selectedItem != null)
                .GroupBy(selectedItem => selectedItem.Id)
                .Select(group => group.First())
                .ToList();

            if (items.Count == 0)
            {
                Utils.SendError("No items are selected");
                return;
            }

            try
            {
                var session = GetSession();
                if (session == null) return;

                var choices = items.Select(selectedItem =>
                {
                    var trader = Plugin.EnableQuickSellTraders ? SelectTrader(selectedItem) : null;
                    var traderPrice = trader?.GetUserItemPrice(selectedItem)?.Amount ?? 0;
                    return new BestPriceChoice
                    {
                        Item = selectedItem,
                        Trader = trader,
                        TraderPrice = traderPrice
                    };
                }).ToList();

                var ragFair = session.RagFair;
                var inventoryController = GetInventoryController();
                var stashGrid = GetStashGrid(inventoryController);
                var canUseFlea = Plugin.EnableQuickSellFlea &&
                                  ragFair?.Available == true &&
                                  inventoryController != null &&
                                  stashGrid != null;

                if (!canUseFlea)
                {
                    FinalizeBestPriceSale(choices, session, showConfirmation, null);
                    return;
                }

                var fleaContext = new RagfairNewOfferContext(
                    stashGrid,
                    inventoryController);
                var fleaChoices = choices
                    .Where(choice => fleaContext.HighlightedAtRagfair(choice.Item))
                    .ToList();

                if (fleaChoices.Count == 0)
                {
                    FinalizeBestPriceSale(choices, session, showConfirmation, ragFair);
                    return;
                }

                var pending = fleaChoices.Count;
                var lockObject = new object();
                foreach (var choice in fleaChoices)
                {
                    ragFair.GetMarketPrices(choice.Item.TemplateId, result =>
                    {
                        var completed = false;
                        lock (lockObject)
                        {
                            try
                            {
                                choice.FleaUnitPrice = (int)Math.Ceiling(
                                    result.avg / 100.0 * Plugin.AvgPricePercent);
                                var grossPrice = choice.FleaUnitPrice * choice.Item.StackObjectsCount;
                                choice.FleaFee = (int)Math.Ceiling(PriceCalculator.CalculateTaxPrice(
                                    choice.Item,
                                    choice.Item.StackObjectsCount,
                                    choice.FleaUnitPrice,
                                    false));
                                choice.FleaNetPrice = grossPrice - choice.FleaFee;
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

                        if (completed)
                        {
                            FinalizeBestPriceSale(choices, session, showConfirmation, ragFair);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static void FinalizeBestPriceSale(
            List<BestPriceChoice> choices,
            IEftSession session,
            bool showConfirmation,
            RagFair ragFair)
        {
            var availableFleaSlots = int.MaxValue;
            if (!Plugin.IgnoreFleaCapacity && ragFair != null)
            {
                availableFleaSlots = Math.Max(
                    0,
                    ragFair.GetMaxOffersCount(ragFair.MyRating) - ragFair.MyOffersCount);
            }

            var fleaChoices = new HashSet<BestPriceChoice>(choices
                .Where(choice =>
                    choice.FleaUnitPrice > 0 &&
                    choice.FleaNetPrice > choice.TraderPrice)
                .OrderByDescending(choice => choice.FleaNetPrice - choice.TraderPrice)
                .Take(availableFleaSlots));

            foreach (var choice in choices)
            {
                choice.UseFlea = fleaChoices.Contains(choice);
            }

            var validChoices = choices
                .Where(choice => choice.UseFlea || choice.Trader != null)
                .ToList();
            if (validChoices.Count == 0)
            {
                Utils.SendError("No selected items can be sold");
                return;
            }

            Action execute = () => ExecuteBestPriceSale(validChoices, session);
            if (!showConfirmation)
            {
                execute();
                return;
            }

            var traderCount = validChoices.Count(choice => !choice.UseFlea);
            var fleaCount = validChoices.Count - traderCount;
            var totalNetPrice = validChoices.Sum(choice => choice.SelectedNetPrice);
            var totalFleaFees = validChoices
                .Where(choice => choice.UseFlea)
                .Sum(choice => choice.FleaFee);
            ShowBestPriceConfirmation(
                execute,
                validChoices.Count,
                traderCount,
                fleaCount,
                totalNetPrice,
                totalFleaFees);
        }

        private static void ExecuteBestPriceSale(List<BestPriceChoice> choices, IEftSession session)
        {
            var choicesById = choices.ToDictionary(choice => choice.Item.Id);
            Action<Item> sell = selectedItem =>
            {
                if (selectedItem == null || !choicesById.TryGetValue(selectedItem.Id, out var choice))
                {
                    return;
                }

                if (choice.UseFlea)
                {
                    DoFleaOffer(choice.Item, session, choice.FleaUnitPrice);
                }
                else
                {
                    SellTrader(choice.Item, choice.Trader, choice.TraderPrice);
                }
            };

            if (IsMultiSelectActive())
            {
                MultiSelect.Apply(sell, ItemUiContext.Instance);
                return;
            }

            foreach (var choice in choices)
            {
                sell(choice.Item);
            }
        }

        private static void ShowBestPriceConfirmation(
            Action callback,
            int count,
            int traderCount,
            int fleaCount,
            int totalNetPrice,
            int totalFleaFees)
        {
            var itemUiContext = ItemUiContext.Instance;
            if (itemUiContext == null)
            {
                callback();
                return;
            }

            var itemLabel = count == 1 ? "item" : "items";
            var message = $"Sell {count} {itemLabel} using the best net price?\n\n" +
                          $"Traders: {traderCount}\n" +
                          $"Flea: {fleaCount}\n" +
                          $"Flea fees: {totalFleaFees:N0} ₽\n" +
                          $"Net profit: {totalNetPrice:N0} ₽";
            itemUiContext.ShowMessageWindow(
                message,
                callback,
                () => { },
                null,
                0f,
                false,
                TextAlignmentOptions.Center);
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
            if (item.GetAllParentItems().Any(x => x is InventoryEquipment)) return;
            if (item.Parent?.Container?.ParentItem?.TemplateId == "55d7217a4bdc2d86028b456d") return;

            var dynamicInteractions = contextInteractions._dynamicInteractions;
            if (dynamicInteractions is null) return;

            var unloadSprite = ResourcesCache.Pop<Sprite>("Characteristics/Icons/UnloadAmmo");

            if (Plugin.EnableQuickSellFlea)
            {
                dynamicInteractions["QuickSell (Flea)"] = new DynamicContextInteraction(
                    "QuickSell (Flea)",
                    "QuickSell (Flea)",
                    () => SellToFlea(item),
                    unloadSprite);
            }

            if (Plugin.EnableQuickSellTraders)
            {
                dynamicInteractions["QuickSell (Trader)"] = new DynamicContextInteraction(
                    "QuickSell (Trader)",
                    "QuickSell (Trader)",
                    () => SellToTraders(item),
                    unloadSprite);
            }
        }

        public static void SellToTraders(Item item)
        {
            var items = GetItemsToSell(item);
            var validItems = new List<Item>();
            var total = 0;
            foreach (var i in items)
            {
                var bestTrader = SelectTrader(i);
                if (bestTrader != null && bestTrader.GetUserItemPrice(i) is { } p)
                {
                    validItems.Add(i);
                    total += p.Amount;
                }
            }
            if (validItems.Count == 0)
            {
                Utils.SendError("No items can be sold to traders");
                return;
            }
            var validIds = new HashSet<string>(validItems.Select(i => i.Id));
            ConfirmWindow(
                () =>
                {
                    if (IsMultiSelectActive())
                    {
                        MultiSelect.Apply(i =>
                        {
                            if (i != null && validIds.Contains(i.Id))
                            {
                                SellTrader(i);
                            }
                        }, ItemUiContext.Instance);
                        return;
                    }

                    foreach (var i in validItems)
                    {
                        SellTrader(i);
                    }
                },
                "to the traders",
                validItems.Count,
                total);
        }

        public static void SellTrader(Item item)
        {
            try
            {
                var bestTrader = SelectTrader(item);
                if (bestTrader == null)
                {
                    Utils.SendError("Item cannot be sold traders");
                    return;
                }

                var price = bestTrader.GetUserItemPrice(item).Value.Amount;
                SellTrader(item, bestTrader, price);
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static void SellTrader(Item item, Trader trader, int price)
        {
            try
            {
                Utils.SendNotification($"Profit: {price}");

                var interactions = GetTraderInteractions(trader);
                if (interactions is null)
                {
                    Utils.SendError("Failed to get trader interactions");
                    return;
                }

                interactions.ConfirmSell(
                    trader.Id,
                    [new TradingItemReference { Item = item, Count = item.StackObjectsCount }],
                    price,
                    new Callback(PlaySellSound));
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        public static void SellToFlea(Item item)
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
                var ragFairClass = session.RagFair;
                if (ragFairClass?.Available != true)
                {
                    Utils.SendError("Flea market is not available");
                    return;
                }
                var helper = new RagfairNewOfferContext(stashGrid, inventoryController);
                var validItems = items.Where(i => helper.HighlightedAtRagfair(i)).ToList();
                if (validItems.Count == 0)
                {
                    Utils.SendError("No items can be sold on the flea");
                    return;
                }
                var maxOffers = ragFairClass.GetMaxOffersCount(ragFairClass.MyRating);
                var currentOffers = ragFairClass.MyOffersCount;
                if (!Plugin.IgnoreFleaCapacity && currentOffers + validItems.Count > maxOffers)
                {
                    Utils.SendError("Not enough flea offer slots");
                    return;
                }
                if (validItems.Count == 1)
                {
                    var single = validItems[0];
                    ragFairClass.GetMarketPrices(single.TemplateId, (ItemMarketPrices result) =>
                    {
                        try
                        {
                            var price = (int)Math.Ceiling(result.avg / 100.0 * Plugin.AvgPricePercent);
                            var fee = (int)Math.Ceiling(PriceCalculator.CalculateTaxPrice(single, single.StackObjectsCount, price, false));
                            ConfirmWindow(() => DoFleaOffer(single, session, price), "on the flea", 1, price * single.StackObjectsCount, fee);
                        }
                        catch (Exception ex)
                        {
                            Utils.SendError(ex.ToString());
                            Plugin.LogSource.LogWarning(ex.ToString());
                        }
                    });
                    return;
                }
                var pending = validItems.Count;
                var prices = new Dictionary<string, int>();
                var lockObj = new object();
                foreach (var i in validItems)
                {
                    ragFairClass.GetMarketPrices(i.TemplateId, (ItemMarketPrices result) =>
                    {
                        lock (lockObj)
                        {
                            try
                            {
                                prices[i.Id] = (int)Math.Ceiling(result.avg / 100.0 * Plugin.AvgPricePercent);
                                if (--pending == 0)
                                {
                                    var total = validItems.Sum(v => prices[v.Id] * v.StackObjectsCount);
                                    var totalFee = validItems.Sum(v => (int)Math.Ceiling(PriceCalculator.CalculateTaxPrice(v, v.StackObjectsCount, prices[v.Id], false)));
                                    ConfirmWindow(
                                        () =>
                                        {
                                            if (IsMultiSelectActive())
                                            {
                                                MultiSelect.Apply(selectedItem =>
                                                {
                                                    if (selectedItem != null && prices.TryGetValue(selectedItem.Id, out var price))
                                                    {
                                                        DoFleaOffer(selectedItem, session, price);
                                                    }
                                                }, ItemUiContext.Instance);
                                                return;
                                            }

                                            foreach (var validItem in validItems)
                                            {
                                                if (prices.TryGetValue(validItem.Id, out var price))
                                                {
                                                    DoFleaOffer(validItem, session, price);
                                                }
                                            }
                                        },
                                        "on the flea",
                                        prices.Count,
                                        total,
                                        totalFee);
                                }
                            }
                            catch (Exception ex)
                            {
                                Utils.SendError(ex.ToString());
                                Plugin.LogSource.LogWarning(ex.ToString());
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static void DoFleaOffer(Item item, IEftSession session, int price)
        {
            try
            {
                var list = new List<BarterTemplate>
                {
                    new()
                    {
                        _tpl = (string)CurrencyUtil.GetCurrencyId(ECurrencyType.RUB),
                        count = price,
                        onlyFunctional = true
                    }
                };
                session.RagfairAddOffer(false, [item.Id], [.. list], new Callback(PlaySellSound));
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static SupplyData GetTraderSupplyData()
        {
            var trader = traders?.FirstOrDefault();
            if (trader == null)
            {
                Utils.SendError("No trader found in the collection.");
                return null;
            }

            var supplyData = trader._supplyData;
            if (supplyData == null)
            {
                Utils.SendError("SupplyData is null for the provided trader instance.");
            }

            return supplyData;
        }

        // Returns Trader with best offer of null if unsellable
        internal static Trader SelectTrader(Item item)
        {
            if (traders == null)
            {
                ForceReloadTraders();
            }

            var supplyData = GetTraderSupplyData();
            if (supplyData == null)
            {
                ForceReloadTraders();
            }

            Trader best = null;
            int bestOffer = 0;

            if (traders == null)
            {
                Utils.SendError("Traders is null even after force reloading. Cannot sell.");
                return null;
            }

            foreach (var trader in traders)
            {
                if (Plugin.TradersBlacklist.Contains(trader.LocalizedName)) continue;

                var price = trader.GetUserItemPrice(item);
                if (price == null) continue;

                if (best == null)
                {
                    best = trader;
                    bestOffer = price.Value.Amount;
                    continue;
                }

                if (bestOffer < price.Value.Amount)
                {
                    best = trader;
                    bestOffer = price.Value.Amount;
                }
            }

            return best;
        }

        private static void PlaySellSound(IResult result)
        {
            if (result.Succeed)
            {
                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
            }
        }

        private static void ForceReloadTraders()
        {
            var session = GetSession();
            if (session == null)
            {
                traders = null;
                return;
            }

            var tradingSession = session.RagFair?.TradingSession;
            if (tradingSession == null)
            {
                traders = null;
                return;
            }

            traders = tradingSession.Traders
                .Where(trader => trader?.Settings != null && !trader.Settings.AvailableInRaid)
                .ToArray();
        }

        private static bool IsMultiSelectActive()
        {
            return Plugin.EnableUIFixesIntegration && MultiSelect.Count > 0;
        }

        /// <summary>
        /// Returns the items to operate on: multi-selection if UIFixes has multiple items selected and item is in that selection, otherwise just the single item.
        /// </summary>
        internal static List<Item> GetItemsToSell(Item item)
        {
            if (item == null) return new List<Item>();
            if (!Plugin.EnableUIFixesIntegration) return new List<Item> { item };
            if (MultiSelect.Count > 0) return MultiSelect.Items.ToList();
            return new List<Item> { item };
        }

        public static void ConfirmWindow(Action callback, string source, int count, int? totalPrice = null, int? fee = null)
        {
            if (!Plugin.ShowConfirmationDialog) { callback(); return; }
            var itemUiContext = ItemUiContext.Instance;
            if (itemUiContext == null) { callback(); return; }

            string message;
            string baseMsg = count == 1
                ? $"Are you sure you want to sell this item {source}?".Localized()
                : $"Are you sure you want to sell {count} items {source}?".Localized();

            if (totalPrice.HasValue)
            {
                if (fee.HasValue && fee.Value > 0)
                {
                    var profit = totalPrice.Value - fee.Value;
                    message = $"{baseMsg}\n\nListing Fee: {fee.Value:N0} ₽\nNet Profit: {profit:N0} ₽";
                }
                else
                {
                    message = count == 1
                        ? $"Are you sure you want to sell this item {source} for {totalPrice.Value:N0} ₽?".Localized()
                        : $"Are you sure you want to sell {count} items {source} for {totalPrice.Value:N0} ₽ total?".Localized();
                }
            }
            else
            {
                message = baseMsg;
            }

            itemUiContext.ShowMessageWindow(message, callback, () => { }, null, 0f, false, TextAlignmentOptions.Center);
        }

    }
}
