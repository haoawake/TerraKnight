using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "candle", the user's 10-25 Water Candle "有些怪根本不刷新多个（先祖山丘）、多个刷新时不要聚在一起"): the
    /// Water Candle in the hotbar at TK_CANDLE_LEVEL (3), each room of TK_CANDLE_ROOMS (the Ancestral Mound and a few
    /// others) entered: per kind, the originals and the copies standing, and the nearest two of a kind (body centres);
    /// the rooms' respawn lines ([刷怪]) say what was left alone and why. One line a kind: [CANDLE].
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CandleTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            int level = int.TryParse(Environment.GetEnvironmentVariable("TK_CANDLE_LEVEL"), out var lv) ? lv : 3;
            var rooms = (Environment.GetEnvironmentVariable("TK_CANDLE_ROOMS") ?? "Crossroads_ShamanTemple;Crossroads_01;Fungus1_01;Fungus2_01;Deepnest_01;Ruins1_01")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var inv = mod.Inventory;
            var kept = inv.Slots[0];
            inv.Slots[0] = ItemStack.Of(ItemType.WaterCandle);
            CarriedFurniture.CandleLevel = level;
            yield return new WaitForSecondsRealtime(0.5f);
            Plugin.Log.LogInfo($"[CANDLE] start level={level} candle={CarriedFurniture.WaterCandle} factor={CarriedFurniture.CandleFactor} multiplier={World.EnemyRespawn.Multiplier}");
            foreach (string room in rooms)
            {
                World.EnemyRespawn.DevGaps.Clear();
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSecondsRealtime(4f);
                var gaps = World.EnemyRespawn.DevGaps.OrderBy(g => g).ToList();
                Plugin.Log.LogInfo($"[CANDLE] {room} 复制体出生间距：{gaps.Count} 只，最小 {(gaps.Count > 0 ? gaps[0].ToString("F1") : "-")}，中位 {(gaps.Count > 0 ? gaps[gaps.Count / 2].ToString("F1") : "-")}，小于 2 格的 {gaps.Count(g => g < 2f)} 只");
                var byKind = new Dictionary<string, List<HealthManager>>();
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != room) continue;
                    string rec = UI.EnemyHover.RecordOf(hm);
                    if (rec == null) continue;
                    if (!byKind.TryGetValue(rec, out var list)) byKind[rec] = list = new List<HealthManager>();
                    list.Add(hm);
                }
                foreach (var kv in byKind.OrderBy(k => k.Key))
                {
                    var list = kv.Value;
                    int copies = list.Count(h => h.name.EndsWith("(TK)"));
                    var at = list.Select(h => (Vector2)(h.GetComponent<Collider2D>()?.bounds.center ?? h.transform.position)).ToList();
                    float nearest = float.PositiveInfinity;
                    for (int i = 0; i < at.Count; i++)
                        for (int j = i + 1; j < at.Count; j++)
                            nearest = Mathf.Min(nearest, Vector2.Distance(at[i], at[j]));
                    Plugin.Log.LogInfo($"[CANDLE] {room} {kv.Key} 原有 {list.Count - copies} 复制 {copies} 最近两只相距 {(float.IsInfinity(nearest) ? "-" : nearest.ToString("F1"))}");
                }
            }
            inv.Slots[0] = kept;
            Plugin.Log.LogInfo($"[CANDLE] done");
        }
    }
}
