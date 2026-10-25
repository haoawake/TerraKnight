using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Goblin Tinkerer's reforging (Main.DrawInventory's InReforgeMenu, Main.ReforgeItemInReforgeSlot): his "Reforge"
    /// button opens the inventory with the reforge slot under it (ItemSlot context 5: only what can have a prefix goes
    /// in), its cost beside it (the item's value with its prefix, times the stack, a third: Prefixes.ReforgeCost), the
    /// hammer under (Reforge_0, Reforge_1 hovered, "重铸"). A click pays and rolls a prefix (Item.Prefix(-2): always one),
    /// the item's new name popping up over the player (PopupText ItemReforge) with Item_37. The best prefix the item can
    /// have (rolledPrefixIsTopTier) pops up in cycling colours with the best_reforge sound and rainbow sparks, and the
    /// hammer holds for a second (reforgeCooldown 60), so one click too many doesn't roll it away. The crafting column
    /// gives way meanwhile. Closing the talk (or saving) puts the item back in the bag.
    /// </summary>
    internal static partial class InventoryGui
    {
        static bool reforgeHover;
        static float reforgeScale = 1f;
        public static int DevReforges, DevReforgesHeld;

        /// <summary>Main.reforgeCooldown: after the best prefix the reforge hammer holds this long (s).</summary>
        public const float BestHold = 1f;
        static float hammerHeldUntil = -1f;

        /// <summary>The reforge hammer holds after a best prefix just rolled.</summary>
        public static bool HammerHeld => Time.unscaledTime < hammerHeldUntil;

        /// <summary>The result over the player: the item's name popping up, the best in cycling colours with its sound and sparks.</summary>
        static void HammerResult(ItemStack item, bool best)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var centre = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            UI.PopupText.Show(item.Def, item.Count, centre, best, noStack: true);
            if (!best) return;
            Sound("Custom/best_reforge");
            // ParticleOrchestraType.RainbowRodHit twice, 16 px over the player's middle.
            var at = Effects.TerraParticles.ToPx(centre + Vector2.up * 0.5f);
            Effects.FxParticles.RainbowRodHit(at);
            Effects.FxParticles.RainbowRodHit(at);
        }

        static void DrawReforge(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind)
        {
            float s = 52f * 0.85f * ui;
            // Where the forge slot is: under the bag's trash slot (Terraria's 270 lay on the bag's last row here).
            var rect = new Rect(view.x + 50f * ui, view.y + UnderBag(ui) + 36f * ui, s, s);
            GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back4"));
            Add(rect, Kind.ReforgeSlot, 0);
            var item = TownNpcs.ReforgeItem;
            if (!item.IsEmpty) DrawIcon(rect, item, false);
            if (rect.Contains(mouse) && !item.IsEmpty)
            {
                hovered = item;
                hoveredKind = Kind.ReforgeSlot;
            }
            var text = new Rect(rect.x + 50f * ui, rect.y, 420f * ui, 24f * ui);
            float n = ChatPulse / 255f;
            var color = new Color(n, n, n, 1f);
            if (item.IsEmpty)
            {
                Outlined(text, TT("LegacyInterface.20", "将物品放到此处重铸"), labelStyle, color);
                reforgeHover = false;
                return;
            }
            long cost = Prefixes.ReforgeCost(item);
            Outlined(text, TT("LegacyInterface.46", "花费") + ": " + Shop.Money(cost), labelStyle, color);
            // The hammer, 70 right and 40 down of the slot, growing while hovered.
            var tex = TerrariaAssets.Texture(reforgeHover && !HammerHeld ? "UI/Reforge_1" : "UI/Reforge_0");
            reforgeScale = Mathf.MoveTowards(reforgeScale, reforgeHover && !HammerHeld ? 1.2f : 1f, 0.02f);
            float w = tex.width * reforgeScale * ui, h = tex.height * reforgeScale * ui;
            var hammer = new Rect(rect.x + 70f * ui - w / 2f, rect.y + 40f * ui - h / 2f, w, h);
            bool over = new Rect(rect.x + 55f * ui, rect.y + 25f * ui, 30f * ui, 30f * ui).Contains(mouse);
            if (over && !reforgeHover) Sound("Menu_Tick");
            reforgeHover = over;
            var prev = GUI.color;
            if (HammerHeld) GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            GUI.DrawTexture(hammer, tex);
            GUI.color = prev;
            Add(new Rect(rect.x + 55f * ui, rect.y + 25f * ui, 30f * ui, 30f * ui), Kind.ReforgeButton, 0);
            if (over) DrawLines(mouse, new List<string> { TT("LegacyInterface.19", "重铸") }, Color.white);
        }

        /// <summary>ItemSlot.Handle for context 5: a click swaps the cursor's item with the slot's; only one that can have a prefix goes in.</summary>
        static void ActReforgeSlot(bool leftPress)
        {
            if (!leftPress || !TownNpcs.ReforgeOpen) return;
            // His forging takes a weapon only; his reforging what can have a prefix.
            if (!inv.Mouse.IsEmpty && (TownNpcs.ForgeMode ? !Forging.Forgeable(ItemDb.Get(inv.Mouse.Type)) : !Prefixes.CanHave(ItemDb.Get(inv.Mouse.Type)))) return;
            var held = inv.Mouse;
            inv.Mouse = TownNpcs.ReforgeItem;
            TownNpcs.ReforgeItem = held;
            Sound("Grab");
        }

        /// <summary>Main.ReforgeItemInReforgeSlot after Player.BuyItem: paid, a new prefix, its name popping up over the player.</summary>
        static bool Reforge()
        {
            ref var item = ref TownNpcs.ReforgeItem;
            if (!TownNpcs.ReforgeOpen || item.IsEmpty) return false;
            // The best prefix just rolled: the hammer holds a second (the click is lost, nothing paid).
            if (HammerHeld) { DevReforgesHeld++; return false; }
            long cost = Prefixes.ReforgeCost(item);
            if (!Shop.Spend(cost))
            {
                Notice(Lang.T("钱不够"));
                return false;
            }
            int p = Prefixes.Roll(ItemDb.Get(item.Type), out bool best);
            if (p <= 0) return false;
            item.Prefix = (byte)p;
            DevReforges++;
            Achievements.AchievementTracker.Reforged(p);
            DevLastBest = best;
            HammerResult(item, best);
            if (best) hammerHeldUntil = Time.unscaledTime + BestHold;
            else Sound("Item_37");
            return true;
        }

        /// <summary>Dev: an item into the reforge slot (as a click with it on the cursor); a reforge; the slot's item.</summary>
        public static bool DevPutReforge(ItemStack s)
        {
            inv.Mouse = s;
            ActReforgeSlot(true);
            return inv.Mouse.IsEmpty;
        }

        public static bool DevReforge() => Reforge();
        public static bool DevLastBest;
        public static void DevReleaseHammer() => hammerHeldUntil = -1f;
    }
}
