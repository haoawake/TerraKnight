using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "nailartsboss"): the nail arts on a boss. The Gruz Mother's room, the boss woken and past her start, the
    /// Knight kept beside her: a click, a Great Slash and a Cyclone Slash; each hit reaching her HealthManager logged
    /// (NailItem.DevProbe), her health, BossWake's and the arena guard's say, and why a refused hit was refused.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NailArtsBossTest()
        {
            mod.DevNoFallDamage = true;
            BossArena.DevWorldArena("BigFly", out var gate, out _);
            yield return GoTo("Crossroads_04", gate ?? EntryGateOf("Crossroads_04"));
            yield return new WaitForSeconds(2f);
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
            var boss = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && UI.EnemyHover.RecordOf(h) == "BigFly");
            Plugin.Log.LogInfo($"[AutoTest] 骨钉技艺首领 找到格鲁兹之母：{boss != null} {(boss != null ? "通过" : "不通过")}");
            if (boss == null) yield break;
            BossWake.Rouse(boss);
            float awake = Time.time + 8f;
            while (Time.time < awake && (BossWake.Asleep(boss) || !GameHooks.PlayerHasControl()))
            {
                pd.isInvincible = true;
                yield return null;
            }
            yield return new WaitForSeconds(2f);
            var probes = new List<string>();
            NailItem.DevProbe = s => probes.Add(s);
            try
            {
                foreach (var (label, hold, up) in new[] { ("单击", 0.08f, false), ("强力斩", 2.0f, false), ("旋风斩", 2.0f, true) })
                {
                    if (boss == null || boss.isDead) break;
                    probes.Clear();
                    int hp0 = boss.hp;
                    float until = Time.time + hold;
                    while (Time.time < until)
                    {
                        pd.isInvincible = true;
                        KeysDown(up ? new[] { Key.K, Key.W } : new[] { Key.K });
                        hero.transform.position = boss.transform.position + new Vector3(-1.4f, 0f, 0f);
                        hero.FaceRight();
                        yield return null;
                    }
                    KeysDown(up ? new[] { Key.W } : new Key[0]);
                    float after = Time.time + 1.4f;
                    while (Time.time < after)
                    {
                        pd.isInvincible = true;
                        if (boss != null && Time.time < after - 1.0f) hero.transform.position = boss.transform.position + new Vector3(-1.4f, 0f, 0f);
                        if (Time.time > after - 1.0f) KeysDown();
                        yield return null;
                    }
                    KeysDown();
                    Plugin.Log.LogInfo($"[AutoTest] 骨钉技艺首领 {label}：血 {hp0}→{(boss != null ? boss.hp : -1)}，唤醒 {(boss != null ? BossWake.Hit(boss).ToString() : "-")}，场外 {(boss != null && World.ArenaGuard.Outside(boss))}，没打中的原因 {NailItem.DevLastRefused}，命中记录 {probes.Count} 条：{string.Join(" | ", probes.Take(8))} {(boss != null && boss.hp < hp0 ? "通过" : "不通过")}");
                    yield return new WaitForSeconds(0.8f);
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
