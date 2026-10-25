using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "nailartshit"): do the nail arts land? In Dirtmouth before the Target Dummy (World.TargetDummy), the Nail in
    /// hand, Use Item on K: a click, a Great Slash and a Cyclone Slash at it; every hit reaching a HealthManager logged
    /// (NailItem.DevProbe: source, attack type, damage, converted or not) and the dummy's hits and damage counted.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NailArtsHitTest()
        {
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            float wait = Time.time + 6f;
            while (World.TargetDummy.DevAt == null && Time.time < wait) yield return null;
            var at = World.TargetDummy.DevAt;
            Plugin.Log.LogInfo($"[AutoTest] 骨钉技艺命中 木偶：{(at.HasValue ? at.Value.ToString("F1") : "没有放下")} {(at.HasValue ? "通过" : "不通过")}");
            if (!at.HasValue) yield break;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, true);
            inv.Slots[0] = ItemStack.Of(ItemType.KnightNail);
            inv.Selected = 0;
            inv.Override = -1;
            Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, Key.K);
            TerraMod.DevNoRealMouse = false;
            var probes = new List<string>();
            NailItem.DevProbe = s => probes.Add(s);
            // Set down left of the dummy from a little above, landed by itself (not pushed into the ground).
            float standX = at.Value.x - 1.3f;
            hero.transform.position = new Vector3(standX, at.Value.y + 2.5f, hero.transform.position.z);
            yield return new WaitForSeconds(1.2f);
            try
            {
                foreach (var (label, hold, up) in new[] { ("单击", 0.08f, false), ("强力斩", 2.0f, false), ("旋风斩", 2.0f, true) })
                {
                    probes.Clear();
                    int hits0 = World.TargetDummy.DevHits, dmg0 = World.TargetDummy.DevDamage;
                    float until = Time.time + hold;
                    while (Time.time < until)
                    {
                        KeysDown(up ? new[] { Key.K, Key.W } : new[] { Key.K });
                        hero.transform.position = new Vector3(standX, hero.transform.position.y, hero.transform.position.z);
                        hero.FaceRight();
                        yield return null;
                    }
                    KeysDown(up ? new[] { Key.W } : new Key[0]);
                    float after = Time.time + 1.6f;
                    while (Time.time < after)
                    {
                        if (Time.time > after - 1.2f) KeysDown();
                        yield return null;
                    }
                    KeysDown();
                    int hits = World.TargetDummy.DevHits - hits0, dmg = World.TargetDummy.DevDamage - dmg0;
                    Plugin.Log.LogInfo($"[AutoTest] 骨钉技艺命中 {label}：木偶记到 {hits} 下共 {dmg} 伤害，技艺计数 {NailArts.DevArts}，转圈补打 {NailArts.DevSpinHits}（{NailArts.DevSpinState}），命中记录 {probes.Count} 条：{string.Join(" | ", probes.Take(10))} {(hits > 0 ? "通过" : "不通过")}");
                    yield return new WaitForSeconds(0.6f);
                }
            }
            finally
            {
                NailItem.DevProbe = null;
                KeysDown();
            }
        }
    }
}
