using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "crafttele"): the crafting column right after a teleport, as a base is set up: beside a bench, at stage
    /// 0, an Iron Anvil and a chest holding the materials of an anvil recipe past the bench's stage (nothing carried, no
    /// endless inventory). From another room a pylon back there, the Recall, and the Rod of Discord from across the room:
    /// the recipe is offered as soon as the player can move, as it is after walking out of reach and back. Logged each
    /// half second.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CraftTeleTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 传送后制作 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            bool infiniteWas = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            var inv = mod.Inventory;
            inv.Infinite = false;
            Shop.DevStage = 0;
            Pylons.UnlockAllForCreative();
            var target = Pylons.Unlocked.Find(p => p.Scene == "Crossroads_30") ?? Pylons.Unlocked.Find(p => p.Scene != "Town");
            // An anvil recipe the bench alone doesn't give at stage 0.
            var r = Recipes.All.FirstOrDefault(x => !x.IsBought && x.Station == "砧" && x.StationStage > 0 && x.Ingredients.Length <= 2 && x.Ingredients.All(g => ItemDb.Get(g.Type).MaxStack > 1));
            Check("有晶塔和配方", target != null && r != null, $"{target?.Scene}/{target?.Bench}，{r?.Def?.Name}（阶段 {r?.StationStage}）");
            if (target == null || r == null) yield break;

            yield return GoTo(target.Scene, EntryGateOf(target.Scene));
            yield return new WaitForSeconds(2f);
            var h = HeroController.instance;
            Vector2 benchAt = Vector2.zero;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Bench Control") { benchAt = f.transform.position; break; }
            h.transform.position = Clear(h, benchAt + Vector2.right * 3f);
            yield return new WaitForSeconds(1f);
            pd.respawnScene = target.Scene;
            pd.respawnMarkerName = target.Bench;
            foreach (var g in r.Ingredients)
                for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == g.Type) inv.Slots[i] = default;
            var feet = h.GetComponent<BoxCollider2D>().bounds;
            bool anvil = false, chest = false;
            for (float dx = 1.5f; dx < 7f; dx += 0.5f)
            {
                if (!anvil) anvil = Stations.TryPlace(StationKind.IronAnvil, h, new Vector2(feet.center.x + dx, feet.min.y + 0.3f));
                if (!chest) chest = Chests.TryPlace(h, new Vector2(feet.center.x - dx, feet.min.y + 0.3f));
            }
            Chests.DevOpenNear(new Vector2(feet.center.x - 2f, feet.center.y));
            var items = Chests.OpenItems;
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++) items[i] = default;
                for (int i = 0; i < r.Ingredients.Length; i++) items[i] = ItemStack.Of(r.Ingredients[i].Type, r.Ingredients[i].Count * 3);
            }
            Chests.Close(false);
            InventoryGui.Open = true;
            yield return null;
            bool Offered() => InventoryGui.DevOffered().Contains(r.Result);
            string State() => $"场景 {GameManager.instance.sceneName}，铁砧在旁 {Stations.Near("砧", HeroController.instance)}，附近箱子 {Chests.ForCrafting(HeroController.instance).Count}，列出 {Offered()}，位置 {(Vector2)HeroController.instance.transform.position:F1}";
            Check("基地摆好", anvil && chest && items != null && Offered(), State());
            var home = (Vector2)h.transform.position;
            InventoryGui.Open = false;

            IEnumerator Watch(string label, bool open)
            {
                float t0 = Time.time;
                var samples = new List<string>();
                float firstMove = -1f;
                bool atMove = false, benchAtMove = false;
                while (Time.time - t0 < 7f)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (GameManager.instance.IsInSceneTransition || GameManager.instance.sceneName != target.Scene) continue;
                    if (!open) InventoryGui.Open = true;
                    bool now = Offered();
                    samples.Add($"{Time.time - t0:F1}s {(now ? "有" : "无")}{(Stations.Near("砧", HeroController.instance) ? "" : "(铁砧不在旁)")}{(Chests.ForCrafting(HeroController.instance).Count > 0 ? "" : "(无箱子)")}{(Shop.Available(HeroController.instance) ? "(长椅)" : "")}");
                    if (firstMove < 0f && HeroController.instance.acceptingInput) { firstMove = Time.time - t0; atMove = now; benchAtMove = Shop.Available(HeroController.instance); }
                }
                Plugin.Log.LogInfo($"[AutoTest] 传送后制作 {label} 过程：{string.Join("；", samples)}");
                Check(label + " 能动时就列出", atMove, $"{firstMove:F1}s；{State()}");
                // A pylon by a bench: the bench's crafting from the first moment too.
                if (label.StartsWith("晶塔")) Check(label + " 能动时长椅就算数", benchAtMove, $"{firstMove:F1}s");
                // Out of reach and back.
                var hero = HeroController.instance;
                var at = (Vector2)hero.transform.position;
                hero.transform.position = Clear(hero, at + Vector2.left * 16f);
                yield return new WaitForSeconds(1f);
                bool away = Offered();
                hero.transform.position = Clear(hero, at);
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 传送后制作 {label} 走开列出 {away}，走回列出 {Offered()}");
            }

            foreach (bool open in new[] { true, false })
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(2f);
                InventoryGui.Open = open;
                Pylons.Teleport(target);
                yield return Watch(open ? "晶塔（背包开着）" : "晶塔（到了再开背包）", open);
                InventoryGui.Open = false;
            }
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            InventoryGui.Open = true;
            Check("回城", Pylons.Recall());
            yield return Watch("回城", true);
            InventoryGui.Open = false;
            // The Rod of Discord from across the room.
            var hr = HeroController.instance;
            hr.transform.position = Clear(hr, home + Vector2.left * 18f);
            yield return new WaitForSeconds(1f);
            InventoryGui.Open = true;
            bool before = Offered();
            bool rod = Player.TeleportRods.Use(mod, hr, home + new Vector2(0f, 0.5f), true);
            float tr = Time.time;
            bool soon = false;
            while (Time.time - tr < 2f) { yield return null; if (Offered()) { soon = true; break; } }
            Check("混沌传送杖", rod && !before && soon, $"传送 {rod}，之前列出 {before}，{Time.time - tr:F2}s 后列出 {soon}；{State()}");
            InventoryGui.Open = false;
            Shop.DevStage = -1;
            TerraMod.DevAllInfinite = infiniteWas;
            Plugin.Log.LogInfo($"[AutoTest] 传送后制作 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
