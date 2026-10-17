using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "crystalair"): the Crystal Heart in creative's column with its icon; the Guide shown any one of the three
    /// telling (all free or not) that the Hallownest Shield wants all three; the Super Dash from mid-air, held in place
    /// while it charges.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CrystalAirTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 水晶之心 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            pd.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            bool infinite = inv.Infinite;
            var hero = HeroController.instance;
            var spriteNames = new System.Collections.Generic.SortedSet<string>();
            foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sp != null && (sp.name.StartsWith("items__") || sp.name.ToLowerInvariant().Contains("crystal") || sp.name.ToLowerInvariant().Contains("super"))) spriteNames.Add(sp.name);
            Plugin.Log.LogInfo($"[AutoTest] 水晶之心 已载入的相关贴图：{string.Join(" ", spriteNames)}");
            try
            {
                // 1. Creative's column (everything free): the three there, the heart's icon drawn.
                inv.Infinite = true;
                var offered = InventoryGui.DevOffered();
                int ci = offered.IndexOf(ItemType.CrystalHeart), si = offered.IndexOf(ItemType.ShadeCloak), hi = offered.IndexOf(ItemType.HallownestShield);
                var tex = Terraria.TerrariaAssets.Texture(ItemDb.Get(ItemType.CrystalHeart).Texture);
                Check("创造模式列表", ci >= 0 && si >= 0 && hi >= 0 && tex != null && tex.name == "TerraKnight/CrystalHeart",
                    $"水晶之心第 {ci} 个，暗影披风 {si}，圣巢之盾 {hi}，共 {offered.Count}；图标 {tex?.name} {tex?.width}x{tex?.height}");
                InventoryGui.Open = true;
                if (ci >= 0) InventoryGui.DevFocus(ci);
                yield return new WaitForSeconds(0.4f);
                Shot("Town", "crystal_creative");
                yield return null;
                InventoryGui.Open = false;
                yield return new WaitForSeconds(0.2f);

                // 2. The Guide: each of the three in his slot, the shield's recipe asking all three (creative's free column too).
                var at = Town.TownNpcs.DevPos("Guide");
                bool talked = false;
                for (int tries = 0; at.HasValue && tries < 5 && !talked; tries++)
                {
                    at = Town.TownNpcs.DevPos("Guide");
                    float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
                    hero.transform.position = Clear(hero, new Vector2(at.Value.x - 1.5f, at.Value.y - 20f / 32f - bottom + 0.05f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.6f);
                    talked = Town.TownNpcs.TryRightClick(Town.TownNpcs.DevPos("Guide").Value);
                }
                Check("和向导说话", talked, Town.TownNpcs.DevDescribe());
                if (talked)
                {
                    string[] names = { ItemDb.Get(ItemType.ShieldOfCthulhu).Name, ItemDb.Get(ItemType.ShadeCloak).Name, ItemDb.Get(ItemType.CrystalHeart).Name };
                    bool shot = false;
                    foreach (var t in new[] { ItemType.ShieldOfCthulhu, ItemType.ShadeCloak, ItemType.CrystalHeart })
                    {
                        Town.TownNpcs.OpenGuideCraft();
                        yield return new WaitForSeconds(0.2f);
                        inv.Mouse = ItemStack.Of(t);
                        InventoryGui.DevGuideSlot();
                        yield return new WaitForSeconds(0.3f);
                        var list = InventoryGui.DevOffered();
                        int k = list.IndexOf(ItemType.HallownestShield);
                        if (k >= 0) InventoryGui.DevFocus(k);
                        string extra = InventoryGui.DevFocusedExtra() ?? "";
                        Check($"向导放{ItemDb.Get(t).Name}", k >= 0 && System.Array.TrueForAll(names, n => extra.Contains(n)),
                            $"列表 {list.Count} 个，圣巢之盾第 {k} 个；悬停：{extra.Replace('\n', '|')}");
                        if (!shot)
                        {
                            shot = true;
                            yield return new WaitForSeconds(0.3f);
                            Shot("Town", "crystal_guide");
                            yield return null;
                        }
                        inv.Mouse = default;
                        InventoryGui.DevGuideSlot();
                        inv.Mouse = default;
                    }
                    Town.TownNpcs.Close(true);
                    yield return new WaitForSeconds(0.3f);
                }
                inv.Infinite = false;
                InventoryGui.Open = false;

                // 3. The Super Dash from mid-air: the heart worn, high above the street, Dash held, let go.
                for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
                inv.Accessories[0] = ItemStack.Of(ItemType.CrystalHeart);
                inv.RefreshEffective();
                AbilityAccessories.DevReset();
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(1f);
                var rb = hero.GetComponent<Rigidbody2D>();
                var start = (Vector2)hero.transform.position + new Vector2(12f, 7f);
                hero.transform.position = Clear(hero, start);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.15f);
                float y0 = hero.transform.position.y, x0 = hero.transform.position.x;
                bool airborne = !hero.cState.onGround && !hero.cState.wallSliding;
                int supers0 = AbilityAccessories.DevSuperDashes, air0 = AbilityAccessories.DevAirCharges;
                AbilityAccessories.DevDashKey = true;
                var trace = new System.Text.StringBuilder();
                // Held in place from the charge's start (the hold before it, a quarter second, is still a fall: a tap dashes).
                float? yCharge = null;
                for (float t = 0f; t < 1.3f; t += Time.deltaTime)
                {
                    if (yCharge == null && HeroFsm(hero, "Superdash")?.ActiveStateName == "Ground Charge") yCharge = hero.transform.position.y;
                    if (trace.Length < 900 && Time.frameCount % 6 == 0)
                        trace.Append($" [{t:F2} y{hero.transform.position.y - y0:F2} {HeroFsm(hero, "Superdash")?.ActiveStateName}]");
                    yield return null;
                }
                float drop = yCharge.HasValue ? yCharge.Value - hero.transform.position.y : 99f;
                string charged = HeroFsm(hero, "Superdash")?.ActiveStateName;
                yield return CloseUpAt("Town", "crystal_air_charge", (Vector2)hero.transform.position, 260, 2);
                AbilityAccessories.DevDashKey = false;
                float maxSpeed = 0f;
                for (float t = 0f; t < 1f; t += Time.deltaTime)
                {
                    maxSpeed = Mathf.Max(maxSpeed, Mathf.Abs(rb.linearVelocity.x));
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 水晶之心 空中蓄力过程：{trace}；{AbilityAccessories.DevPress}");
                Check("空中长按冲刺键：超级冲刺", airborne && AbilityAccessories.DevSuperDashes > supers0 && maxSpeed > 15f,
                    $"起始在空中 {airborne}，松手时 {charged}，超级冲刺 {AbilityAccessories.DevSuperDashes - supers0} 次，最快 {maxSpeed:F1}，横移 {hero.transform.position.x - x0:F1}");
                Check("空中蓄力时停在原处", AbilityAccessories.DevAirCharges > air0 && drop < 0.1f, $"蓄力期间下落 {drop:F2}");
                // Stopped mid-flight by a fresh press of the Dash key (the open sky has no wall to end it).
                int stopped0 = AbilityAccessories.DevStopped;
                float xStop = hero.transform.position.x;
                AbilityAccessories.DevDashKey = true;
                yield return null;
                yield return null;
                AbilityAccessories.DevDashKey = false;
                var flight = new System.Text.StringBuilder();
                for (float t = 0f, next = 0f; t < 6f && (!GameHooks.PlayerHasControl() || hero.cState.superDashing); t += Time.deltaTime)
                {
                    if (t >= next)
                    {
                        next += 0.3f;
                        flight.Append($" [{t:F1} ({hero.transform.position.x:F1},{hero.transform.position.y:F1}) v{rb.linearVelocity.x:F0} {HeroFsm(hero, "Superdash")?.ActiveStateName}]");
                    }
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 水晶之心 冲出去之后再按冲刺键：{flight}，墙 {AbilityAccessories.DevWallHits}");
                Check("飞行中再按冲刺键停下", AbilityAccessories.DevStopped > stopped0 && Mathf.Abs(hero.transform.position.x - xStop) < 8f,
                    $"停下 {AbilityAccessories.DevStopped - stopped0} 次，按键后又飞了 {hero.transform.position.x - xStop:F1}");
                Check("冲完之后能操作", GameHooks.PlayerHasControl() && !hero.cState.superDashing, $"超级冲刺状态机 {HeroFsm(hero, "Superdash")?.ActiveStateName}");

                // 4. On the ground it still goes (after the cooldown).
                AbilityAccessories.DevReset();
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(1f);
                for (float t = 0f; t < 3f && !hero.cState.onGround; t += Time.deltaTime) yield return null;
                supers0 = AbilityAccessories.DevSuperDashes;
                air0 = AbilityAccessories.DevAirCharges;
                AbilityAccessories.DevDashKey = true;
                yield return new WaitForSeconds(1.3f);
                AbilityAccessories.DevDashKey = false;
                yield return new WaitForSeconds(0.6f);
                Check("地上长按仍然超级冲刺", AbilityAccessories.DevSuperDashes > supers0 && AbilityAccessories.DevAirCharges == air0,
                    $"超级冲刺 {AbilityAccessories.DevSuperDashes - supers0} 次，算空中蓄力 {AbilityAccessories.DevAirCharges - air0} 次");
                for (float t = 0f; t < 6f && (!GameHooks.PlayerHasControl() || hero.cState.superDashing); t += Time.deltaTime) yield return null;
            }
            finally
            {
                AbilityAccessories.DevDashKey = null;
                inv.Infinite = infinite;
                InventoryGui.Open = false;
                mod.DevNoFallDamage = false;
                Plugin.Log.LogInfo($"[AutoTest] 水晶之心 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
            }
        }
    }
}
