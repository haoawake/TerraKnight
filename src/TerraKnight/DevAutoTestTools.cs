using System.Collections;
using System.Text;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "tools", sweep R1): every new bow shoots, every hammer, axe, hamaxe and pickaxe
    /// swings into an enemy held in reach; the Hammush's mushrooms, the Bone Pickaxe's bones, The
    /// Axe's guitar note and a lunar tool's glowmask in hand.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ToolsTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            Wear();
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            void Take(ItemType t)
            {
                inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(t);
                inv.Selected = Inventory.HotbarSize - 1;
            }
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float dd = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (dd < best) { best = dd; target = hm; }
            }
            var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            void Hold(Vector2 at)
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 3000);
                Pin(target, trb, at);
            }
            // The bows: one shot each at an enemy 4 tiles off.
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WoodenArrow, 999);
            var far = C() + Vector2.right * side * 4f;
            var sb = new StringBuilder();
            foreach (var bow in new[]
            {
                ItemType.CopperBow, ItemType.TinBow, ItemType.LeadBow, ItemType.SilverBow, ItemType.TungstenBow, ItemType.PlatinumBow, ItemType.EbonwoodBow,
                ItemType.ShadewoodBow, ItemType.RichMahoganyBow, ItemType.PalmWoodBow, ItemType.BorealWoodBow, ItemType.AshWoodBow, ItemType.PearlwoodBow, ItemType.TendonBow,
            })
            {
                Take(bow);
                while (!mod.DevReady) yield return null;
                int a0 = TerraProjectiles.DevMade(ProjKind.WoodenArrow), h0 = TerraProjectiles.DevHit(ProjKind.WoodenArrow);
                mod.DevAim = far;
                mod.DevUseSelected(far);
                for (float t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    Hold(far);
                    yield return null;
                }
                sb.Append($"{ItemDb.Get(bow).Name} 箭{TerraProjectiles.DevMade(ProjKind.WoodenArrow) - a0}中{TerraProjectiles.DevHit(ProjKind.WoodenArrow) - h0} ");
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 工具 弓：{sb}");
            // The tools: two swings each at an enemy just in front.
            var near = C() + Vector2.right * side * 1.1f;
            sb.Clear();
            int missed = 0;
            foreach (var def in ItemDb.All)
            {
                if (def.Channel) continue;
                if (def.HammerPower == 0 && def.AxePower == 0 && !(def.PickPower > 0 && def.Type >= ItemType.TinPickaxe && def.Type <= ItemType.ShroomiteDiggingClaw)
                    && def.Type != ItemType.VortexPickaxe && def.Type != ItemType.NebulaPickaxe && def.Type != ItemType.SolarFlarePickaxe && def.Type != ItemType.StardustPickaxe) continue;
                Take(def.Type);
                MeleeSpecials.DevHits.Clear();
                int shrooms0 = MeleeSpecials.DevHammushShrooms, bones0 = TerraProjectiles.DevMade(ProjKind.BoneSwordBone), notes0 = TerraMod.DevNotes;
                bool glowSeen = false;
                int swings = 0;
                mod.DevAim = near;
                if (def.Type == ItemType.TheAxe) mod.StrumGuitar(near);
                for (float t = 0f; t < 1.4f; t += Time.deltaTime)
                {
                    Hold(near);
                    if (swings < 2 && mod.DevReady && !mod.IsUsingItem)
                    {
                        mod.DevUseSelected(near);
                        swings++;
                    }
                    glowSeen |= mod.DevHeldGlow;
                    yield return null;
                }
                int hits = MeleeSpecials.DevHits.TryGetValue(def.Type, out int h) ? h : 0;
                if (hits == 0) missed++;
                string extra = def.Type == ItemType.Hammush ? $"(蘑菇{MeleeSpecials.DevHammushShrooms - shrooms0})" : def.Type == ItemType.BonePickaxe ? $"(骨头{TerraProjectiles.DevMade(ProjKind.BoneSwordBone) - bones0})"
                    : def.Type == ItemType.TheAxe ? $"(音符{TerraMod.DevNotes - notes0})" : def.Glow != null ? $"(发光={glowSeen})" : "";
                sb.Append($"{def.Name}{hits}{extra} ");
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 工具 挥动命中：{sb}；没打中的 {missed} 件");
            // The drills and chainsaws: held at the enemy 0.7 s each, then let go.
            sb.Clear();
            var nearHold = C() + Vector2.right * side * 1.6f;
            foreach (var def in ItemDb.All)
            {
                if (def.Type < ItemType.CobaltChainsaw || def.Type > ItemType.ButchersChainsaw) continue;
                Take(def.Type);
                TerraProjectiles.Instance.DevClear();
                while (!mod.DevReady) yield return null;
                int h0 = TerraProjectiles.DevHit(def.Shoot), s0 = TerraProjectiles.DevButcherSparks;
                mod.DevAim = nearHold;
                mod.DevHold = true;
                mod.DevUseSelected(nearHold);
                int most = 0;
                bool pictured = def.Type != ItemType.LaserDrill && def.Type != ItemType.VortexDrill && def.Type != ItemType.ButchersChainsaw;
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    Hold(nearHold);
                    most = Mathf.Max(most, TerraProjectiles.Instance.DevCount(def.Shoot));
                    if (!pictured && t > 0.3f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, def.Type == ItemType.LaserDrill ? "tools_laserdrill" : def.Type == ItemType.VortexDrill ? "tools_vortexdrill" : "tools_butcher", hero, 90, 3);
                    }
                    yield return null;
                }
                mod.DevHold = false;
                for (int f = 0; f < 4; f++) yield return null;
                int left = TerraProjectiles.Instance.DevCount(def.Shoot);
                sb.Append($"{def.Name} 头{most}中{TerraProjectiles.DevHit(def.Shoot) - h0}松后{left}" + (def.Type == ItemType.ButchersChainsaw ? $"(火花{TerraProjectiles.DevButcherSparks - s0})" : "") + " ");
            }
            mod.DevAim = null;
            mod.DevHold = null;
            Plugin.Log.LogInfo($"[AutoTest] 工具 钻头链锯：{sb}");
            // A lunar pickaxe in hand: a close-up of its glowmask mid-swing.
            Take(ItemType.NebulaPickaxe);
            while (!mod.DevReady) yield return null;
            mod.DevAim = near;
            mod.DevUseSelected(near);
            for (int f = 0; f < 4; f++) yield return null;
            yield return CloseUp(scene, "tools_glow", hero, 70, 3);
            mod.DevAim = null;
            mod.DevHold = null;
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
