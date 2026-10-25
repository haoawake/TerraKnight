using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TerraKnight.Effects;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ufolaser"): the Xeno Staff's UFO against the nearest enemy (held beside the player, its life topped up)
    /// for 6 s, and its laser shot across the room every half second besides: every frame each of the lasers' line motes (dust 160) followed from when it shows to when it goes, how long it
    /// lasts and how far it sinks (Terraria px, down), with screenshots: the line should hang where it was shot and fade.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator UfoLaserTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 飞碟激光：场景里没有敌人");
                yield break;
            }
            Vector2 at = Clear(hero, (Vector2)hero.transform.position + new Vector2(-4f, 2.5f));
            var trb = target.GetComponent<Rigidbody2D>();
            int slot = Inventory.HotbarSize - 1;
            inv.Slots[slot] = ItemStack.Of(ItemType.XenoStaff);
            inv.Selected = slot;
            while (!mod.DevReady) yield return null;
            mod.DevAim = at;
            mod.DevUseSelected(at);
            mod.DevAim = null;

            var field = typeof(TerraParticles).GetField("dust", BindingFlags.NonPublic | BindingFlags.Instance);
            var seen = new Dictionary<int, (float startY, float startT, float maxDrop)>();
            var lives = new List<float>();
            var drops = new List<float>();
            float start = Time.time, nextShot = start + 2f, nextLaser = start;
            int hits0 = Minions.DevHits;
            var types = new Dictionary<int, int>();
            int shots = 0;
            while (Time.time - start < 6f)
            {
                // The UFO's own shot (its AI: ShootMinion 433, 4 px an update toward its target), straight across the room
                // from over the player every half second, whether or not the UFO has found an enemy.
                if (Time.time >= nextLaser)
                {
                    nextLaser = Time.time + 0.5f;
                    var from = (Vector2)hero.transform.position + new Vector2(0f, 2.5f);
                    TerraProjectiles.Instance?.ShootMinion(ProjKind.UfoLaser, from, new Vector2(Time.time - start < 3f ? 4f : -4f, 0.4f), 10, 0f);
                }
                target.hp = Mathf.Max(target.hp, 3000);
                target.transform.position = new Vector3(at.x, at.y, target.transform.position.z);
                if (trb != null) trb.linearVelocity = Vector2.zero;
                var all = field?.GetValue(TerraParticles.Instance) as TerraParticles.Dust[];
                if (all != null)
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].Active) types[all[i].Type] = types.TryGetValue(all[i].Type, out var c) ? c + 1 : 1;
                        bool line = all[i].Active && all[i].Type == 160;
                        if (line)
                        {
                            if (!seen.TryGetValue(i, out var s)) seen[i] = (all[i].Position.y, Time.time, 0f);
                            else seen[i] = (s.startY, s.startT, Mathf.Max(s.maxDrop, all[i].Position.y - s.startY));
                        }
                        else if (seen.TryGetValue(i, out var gone))
                        {
                            lives.Add(Time.time - gone.startT);
                            drops.Add(gone.maxDrop);
                            seen.Remove(i);
                        }
                    }
                if (Time.time >= nextShot && shots < 3)
                {
                    nextShot = Time.time + 1.2f;
                    shots++;
                    Shot(scene, $"ufolaser_{shots}");
                }
                yield return null;
            }
            lives.Sort();
            drops.Sort();
            string Pct(List<float> v, float q) => v.Count == 0 ? "-" : v[Mathf.Min(v.Count - 1, (int)(v.Count * q))].ToString("F2");
            Plugin.Log.LogInfo($"[AutoTest] 飞碟激光：光点 {lives.Count} 个（仍在 {seen.Count}），寿命中位 {Pct(lives, 0.5f)} s、最长 {Pct(lives, 1f)} s；下坠中位 {Pct(drops, 0.5f)} px、最大 {Pct(drops, 1f)} px；UFO {Minions.Instance.DevCount(Minions.Kind.UFO)} 个，命中 {Minions.DevHits - hits0} 次，目标 {target.name} 在 {at}；尘埃种类 {string.Join(" ", System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(types, kv => kv.Value), 8))}，数组 {(field == null ? "找不到" : "有")}");
            inv.Slots[slot] = default;
        }
    }
}
