using HarmonyLib;
using System;

namespace HapticsPlugin.Patches
{
    // ── Mining / Chopping (A-6 fix) ───────────────────────────────────────────
    // EntityPlayer.MiningActivated and ChoppingActivated almost certainly do not
    // exist in the game assembly. The mining/chopping feedback is triggered through
    // the melee hit path. We hook ItemActionAttack.HitBlock (verify name with
    // FindMethods.csx) and check the block material to distinguish stone/ore from wood.
    //
    // ActionsHolder removed in 7DTD 1.x — access the local player directly via
    // GameManager.Instance.World.GetPrimaryPlayer() instead.

    [HarmonyPatch(typeof(ItemActionAttack), "HitBlock")]
    public class MiningChopPatch
    {
        static void Postfix(ItemActionAttack __instance, Vector3i _blockPos)
        {
            // ActionsHolder removed in 7DTD 1.x — use primary player singleton.
            var player = GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null) return;
            if (PatchHelpers.IsSpectator(player)) return;

            var world = GameManager.Instance?.World;
            if (world == null) return;

            var bv = world.GetBlock(_blockPos);
            if (bv.isair) return;

            // Distinguish wood (chopping) from stone/ore (mining) by material tag.
            string material = bv.Block?.blockMaterial?.SurfaceCategory;
            if (material == null) return;

            // string.Contains(string, StringComparison) not available on .NET 4.8 — use IndexOf.
            if (material.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0)
                HapticsConfig.ChoppingTree.Fire();
            else
                HapticsConfig.Mining.Fire();
        }
    }

    // ── Crafting ──────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(XUiC_RecipeCraftCount), "RecipeCraftingDone")]
    public class CraftCompletePatch
    {
        static void Postfix() => HapticsConfig.CraftComplete.Fire();
    }

    // ── Looting ───────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(XUiC_LootWindow), "Open")]
    public class LootOpenedPatch
    {
        static void Postfix() => HapticsConfig.LootOpened.Fire();
    }

    // NOTE (A-13): EntityPlayer.GrabItem may not be the correct method name.
    // Use FindMethods.csx to search for "AddItem", "GrabItem", "PickupItem" in
    // EntityPlayer and XUiC_ItemStack to find the correct pickup hook.
    // Left as-is; if the method doesn't exist, Harmony logs a warning at startup
    // and the patch is skipped safely.

    [HarmonyPatch(typeof(EntityPlayer), "GrabItem")]
    public class RareLootPatch
    {
        static void Postfix(EntityPlayer __instance, ItemStack _itemStack)
        {
            // isLocalPlayer removed in 7DTD 1.x — use type check instead.
            if (!(__instance is EntityPlayerLocal)) return;
            if (_itemStack?.itemValue == null) return;
            if (_itemStack.itemValue.Quality >= 5)
                HapticsConfig.RareLoot.Fire();
        }
    }

    // ── Progression ───────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(EntityPlayer), "LevelUpStats")]
    public class LevelUpPatch
    {
        static void Postfix(EntityPlayer __instance)
        {
            // isLocalPlayer removed in 7DTD 1.x — use type check instead.
            if (!(__instance is EntityPlayerLocal)) return;
            HapticsConfig.LevelUp.Fire();
        }
    }

    [HarmonyPatch(typeof(QuestEventManager), "CompleteQuest")]
    public class QuestCompletePatch
    {
        static void Postfix() => HapticsConfig.QuestComplete.Fire();
    }

    // ── Eating / Drinking ─────────────────────────────────────────────────────
    // Both code paths are patched:
    //   ExecuteAction        — regular eat/drink (held button, animation delay)
    //   ExecuteInstantAction — instant-use consumables (some stims, some meds)
    //
    // Eat vs Drink distinction: 7DTD consistently prefixes drink items with "drink"
    // (e.g. "drinkJarBoiledWater", "drinkBeer", "drinkCoffee").
    // Anything else is treated as food. Falls back to PlayerEat if name is unavailable.
    //
    // ActionsHolder removed in 7DTD 1.x — use primary player singleton instead.

    [HarmonyPatch(typeof(ItemActionEat), "ExecuteAction")]
    public class EatActionPatch
    {
        static void Postfix(ItemActionEat __instance)
        {
            var player = GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null) return;
            if (PatchHelpers.IsSpectator(player)) return;
            FireConsumeHaptic(player);
        }

        /// <summary>Shared by EatInstantActionPatch — determines eat vs drink by item name.</summary>
        internal static void FireConsumeHaptic(EntityPlayerLocal player)
        {
            string itemName = player.inventory?.holdingItem?.Name ?? "";
            // StartsWith(string, StringComparison) IS available on .NET 4.8.
            if (itemName.StartsWith("drink", StringComparison.OrdinalIgnoreCase))
                HapticsConfig.PlayerDrink.Fire();
            else
                HapticsConfig.PlayerEat.Fire();
        }
    }

    [HarmonyPatch(typeof(ItemActionEat), "ExecuteInstantAction")]
    public class EatInstantActionPatch
    {
        static void Postfix(ItemActionEat __instance)
        {
            var player = GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null) return;
            if (PatchHelpers.IsSpectator(player)) return;
            EatActionPatch.FireConsumeHaptic(player);
        }
    }
}
