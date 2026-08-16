using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Linq;
using TMPro;
using UIFixesInterop;
using UnityEngine.EventSystems;

namespace QuickSell.Patches
{
    /// <summary>
    /// Patches ItemUiContext.Update to run keybind checks in the same execution context as the game UI.
    /// </summary>
    public static class KeybindPatches
    {
        public static void Enable()
        {
            new ItemUiContextKeybindPatch().Enable();
        }

        internal static bool TextboxActive()
        {
            return EventSystem.current?.currentSelectedGameObject != null &&
                   EventSystem.current.currentSelectedGameObject.activeInHierarchy &&
                   EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        }

        internal class ItemUiContextKeybindPatch : ModulePatch
        {
            protected override System.Reflection.MethodBase GetTargetMethod()
            {
                return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.Update));
            }

            [PatchPostfix]
            private static void Postfix(ItemUiContext __instance)
            {
                if (Plugin.KeybindBestPrice == null && Plugin.KeybindBestPriceNoConfirmation == null)
                    return;

                if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance is not HideoutGameWorld)
                    return;

                var menuUi = Singleton<MenuUI>.Instantiated
                    ? Singleton<MenuUI>.Instance
                    : null;
                if (menuUi?.HideoutAreaTransferItemsScreen?.isActiveAndEnabled == true)
                    return;

                if (TextboxActive())
                    return;

                var itemContext = __instance.CurrentItemContext;
                var hasMultiSelection = Plugin.EnableUIFixesIntegration && MultiSelect.Count > 0;
                if (itemContext == null && !hasMultiSelection)
                    return;

                if (itemContext != null && itemContext.ViewType != EItemViewType.Inventory)
                    return;

                // If no hovered item exists, allow keybinds to operate on active UIFixes multi-selection.
                var item = itemContext?.Item;
                if (item == null && hasMultiSelection)
                    item = MultiSelect.Items.FirstOrDefault();
                if (item == null)
                    return;

                if (item.GetAllParentItems().Any(x => x is InventoryEquipment))
                    return;
                if (item.Parent?.Container?.ParentItem?.TemplateId == "55d7217a4bdc2d86028b456d")
                    return;

                if (Plugin.KeybindBestPrice != null && Plugin.KeybindBestPrice.Value.IsDown())
                {
                    ContextMenuPatch.SellBestPrice(item, true);
                    return;
                }

                if (Plugin.KeybindBestPriceNoConfirmation != null && Plugin.KeybindBestPriceNoConfirmation.Value.IsDown())
                {
                    ContextMenuPatch.SellBestPrice(item, false);
                }
            }
        }
    }
}
