using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_magiccadence[_Item1.Item2]" for magic weapons, "fx_hitcadence[_Item1.Item2]" for every weapon): each
    /// weapon used for a while on one enemy held still in front of the player (its life topped up, mana and ammo there).
    /// Every blow it takes is noted (MeleeHits.HitWith, whatever dealt it) with the projectile whose update dealt it and
    /// the rule its kind keeps (Terraria's Projectile.Damage: a single-hit shot once; its own cooldown; one per kind;
    /// or the enemy's 10 ticks after any of the player's piercing shots). A blow sooner than its rule lets is counted
    /// against the weapon: that is damage every frame where Terraria has none.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        [HarmonyPatch]
        internal static class CadenceTrace
        {
            public static HealthManager Target;
            public static readonly List<(float T, int Damage, ProjKind Kind, int Id, int MaxPen)> Hits = new List<(float, int, ProjKind, int, int)>();

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MeleeHits), nameof(MeleeHits.HitWith))]
            static void After(Collider2D col, int damage, bool __result)
            {
                if (Target == null || !__result || col == null || col.GetComponentInParent<HealthManager>() != Target) return;
                Hits.Add((Time.time, damage, TerraProjectiles.DevCurrentKind, TerraProjectiles.DevCurrentId, TerraProjectiles.DevCurrentMaxPen));
            }
        }

        IEnumerator MagicCadenceTest(string only) => HitCadenceTest(only, true, 3f);

        IEnumerator HitCadenceTest(string only, bool magicOnly, float seconds)
        {
            mod.DevNoFallDamage = true;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            mod.Stats.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != "Crossroads_01") continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            if (target == null) { FxCheck("命中节奏 找得到敌人", false, ""); done = true; yield break; }
            var trb = target.GetComponent<Rigidbody2D>();
            var heroCol = hero.GetComponent<BoxCollider2D>();
            Vector2 heroAt = hero.transform.position;
            var weapons = ItemDb.All.Where(d => d.IsWeapon && d.Damage > 0 && !d.IsSummon && !d.IsAmmo && !d.HkItem
                && (magicOnly ? d.Class == DamageClass.Magic && d.Shoot != ProjKind.None : d.Class != DamageClass.Summon || d.IsWhip)).Select(d => d.Type).Distinct().ToList();
            if (only != null) { var want = new HashSet<string>(only.Split('.')); weapons = weapons.Where(t => want.Contains(t.ToString())).ToList(); }
            // One of each ammo, in the last slots.
            var ammo = new Dictionary<AmmoKind, ItemType>();
            foreach (var d in ItemDb.All) if (d.IsAmmo && d.Ammo != AmmoKind.None && !ammo.ContainsKey(d.Ammo)) ammo[d.Ammo] = d.Type;
            int last = Inventory.Size - 1;
            foreach (var a in ammo.Values) if (last > Inventory.HotbarSize) inv.Slots[last--] = ItemStack.Of(a, 999);
            int bad = 0;
            var badRows = new List<string>();
            foreach (var weapon in weapons)
            {
                inv.Slots[0] = ItemStack.Of(weapon);
                inv.Selected = 0;
                inv.Override = -1;
                var def = ItemDb.Get(weapon);
                Vector2 centre = heroCol.bounds.center;
                Vector2 aim = centre + Vector2.right * (def.Class == DamageClass.Melee && def.Shoot == ProjKind.None ? 1.2f : 3f);
                CadenceTrace.Hits.Clear();
                CadenceTrace.Target = target;
                mod.DevAim = aim;
                mod.DevHold = def.Channel ? true : (bool?)null;
                for (float until = Time.time + seconds; Time.time < until;)
                {
                    if (target == null) break;
                    hero.transform.position = heroAt;
                    target.hp = 99999;
                    target.isDead = false;
                    Pin(target, trb, aim);
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    mod.DevAim = aim;
                    if (mod.DevReady) mod.DevUseSelected(aim);
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
                CadenceTrace.Target = null;
                var hits = new List<(float T, int Damage, ProjKind Kind, int Id, int MaxPen)>(CadenceTrace.Hits);
                yield return new WaitForSeconds(0.4f);
                TerraProjectiles.Instance.DevClear();
                if (target == null) break;
                // Against each blow's rule.
                var lastOf = new Dictionary<int, float>();
                var lastKind = new Dictionary<ProjKind, float>();
                float lastShared = -100f;
                var why = new Dictionary<string, int>();
                void Count(string w) => why[w] = (why.TryGetValue(w, out int n) ? n : 0) + 1;
                foreach (var h in hits)
                {
                    if (h.Kind == ProjKind.None) continue;
                    string rule = TerraProjectiles.DevImmunityOf(h.Kind, h.MaxPen);
                    float tick = h.T * 60f;
                    var parts = rule.Split(' ');
                    int cd = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 0;
                    switch (parts[0])
                    {
                        case "single":
                        case "once":
                            if (lastOf.ContainsKey(h.Id)) Count($"{h.Kind}（{rule}）同一发打了两次");
                            break;
                        case "local":
                            if (lastOf.TryGetValue(h.Id, out float lt) && tick - lt < cd - 1.5f) Count($"{h.Kind}（{rule}）同一发间隔 {tick - lt:F0} 帧");
                            break;
                        case "kind":
                            if (lastKind.TryGetValue(h.Kind, out float kt) && tick - kt < cd - 1.5f) Count($"{h.Kind}（{rule}）同类间隔 {tick - kt:F0} 帧");
                            break;
                        case "none":
                            // Terraria's own exceptions (javelins stick and stop hitting, nails blow up): never twice in a row.
                            if (lastOf.TryGetValue(h.Id, out float nt) && tick - nt < 1.5f) Count($"{h.Kind}（{rule}）同一发连着两帧");
                            break;
                        case "shared":
                            if (tick - lastShared < cd - 1.5f) Count($"{h.Kind}（{rule}）共用无敌内间隔 {tick - lastShared:F0} 帧");
                            lastShared = tick;
                            break;
                    }
                    lastOf[h.Id] = tick;
                    lastKind[h.Kind] = tick;
                }
                int over = why.Values.Sum();
                int fromProj = hits.Count(h => h.Kind != ProjKind.None);
                string row = $"{def.Name}（{weapon}，{def.Class}）：{seconds:F0} 秒命中 {hits.Count} 下（弹幕 {fromProj}），平均 {(hits.Count > 0 ? (int)hits.Average(x => x.Damage) : 0)}；比规则快的 {over} 下{(over > 0 ? "：" + string.Join("，", why.Select(kv => $"{kv.Key}×{kv.Value}")) : "")}";
                Plugin.Log.LogInfo("[AutoTest] 修复1016 命中节奏 " + row);
                if (over > 0 && only != null)
                {
                    float t0 = hits.Count > 0 ? hits[0].T : 0f;
                    Plugin.Log.LogInfo("[AutoTest] 修复1016 命中节奏 每一下：" + string.Join(" ", hits.Select(h => $"{(h.T - t0) * 60f:F1}帧/{h.Kind}#{h.Id % 10000}/{h.Damage}")));
                }
                if (over > 0) { bad++; badRows.Add(row); }
            }
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            done = true;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 命中节奏 共 {weapons.Count} 把，比自己的无敌规则打得快的 {bad} 把：\n  " + string.Join("\n  ", badRows));
            FxCheck("武器没有比无敌规则更快的命中", bad == 0, $"{bad}/{weapons.Count} 把");
        }
    }
}
