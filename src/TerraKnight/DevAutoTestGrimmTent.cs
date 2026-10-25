using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "grimmtent"): the user's "击杀格林之后让我去猩红帐幕后，但我无法往右走" (10-25). The Troupe Master's tent
    /// entered with the troupe's records as a bestiary rematch leaves them (Grimm unbeaten, the child at level 1), right after
    /// the Troupe Master's world win, and with the third flames gathered (Nightmare King's way open in the game): each time,
    /// the tent's back-room pieces (masks, curtains, gates, Grimm) logged, and a box the player's size slid right along the
    /// floor from the entrance, the first solid in its way named.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly Regex tentPiece = new Regex("back|curtain|gate|grimm|sleep|wall|block|door|right", RegexOptions.IgnoreCase);

        IEnumerator GrimmTentTest()
        {
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var pd = PlayerData.instance;
            var cases = new (string Name, bool Killed, int Level, int Flames)[]
            {
                ("挑战放回后的记录", false, 1, 0),
                ("打赢团长，第三轮火焰未收", true, 3, 0),
                ("打赢团长，第三轮火焰收齐", true, 3, 3),
            };
            for (int i = 0; i < cases.Length; i++)
            {
                var c = cases[i];
                pd.SetBool("nightmareLanternLit", true);
                pd.SetBool("troupeInTown", true);
                pd.SetBool("metGrimm", true);
                pd.SetBool("gotCharm_40", true);
                pd.SetBool("equippedCharm_40", true);
                pd.SetBool("killedGrimm", c.Killed);
                pd.SetInt("grimmChildLevel", c.Level);
                pd.SetInt("flamesCollected", c.Flames);
                pd.SetBool("killedNightmareGrimm", false);
                yield return GoTo("Town", EntryGateOf("Town"));
                yield return new WaitForSeconds(1f);
                yield return GoTo("Grimm_Main_Tent", "left1");
                yield return new WaitForSeconds(2.5f);
                var hero = HeroController.instance;
                Plugin.Log.LogInfo($"[AutoTest] 格林帐篷 {c.Name}：killedGrimm={pd.GetBool("killedGrimm")} grimmChildLevel={pd.GetInt("grimmChildLevel")} flamesCollected={pd.GetInt("flamesCollected")}，玩家 {(Vector2)hero.transform.position}");
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t == null || t.gameObject.scene.name != "Grimm_Main_Tent" || !tentPiece.IsMatch(t.name)) continue;
                    var cols = t.GetComponents<Collider2D>();
                    var fsms = t.GetComponents<PlayMakerFSM>();
                    if (cols.Length == 0 && fsms.Length == 0) continue;
                    string colText = string.Join("；", cols.Select(k => $"{k.GetType().Name} {(k.enabled ? "开" : "关")}{(k.isTrigger ? " 触发" : "")} 层 {k.gameObject.layer} {(k.enabled && t.gameObject.activeInHierarchy ? k.bounds.min.ToString("F1") + "~" + k.bounds.max.ToString("F1") : "")}"));
                    string fsmText = string.Join("；", fsms.Select(f => $"{f.FsmName}:{f.ActiveStateName}"));
                    Plugin.Log.LogInfo($"[AutoTest] 格林帐篷   {TentPath(t)} {(t.gameObject.activeInHierarchy ? "在" : "隐")} @({t.position.x:F1},{t.position.y:F1}) [{colText}] [{fsmText}]");
                }
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (tp != null && tp.gameObject.scene.name == "Grimm_Main_Tent")
                        Plugin.Log.LogInfo($"[AutoTest] 格林帐篷   门 {tp.name} {(tp.gameObject.activeInHierarchy ? "在" : "隐")} @({tp.transform.position.x:F1},{tp.transform.position.y:F1}) → {tp.targetScene}");
                // The player's box slid right along the floor from where they stand.
                var box = hero.GetComponent<Collider2D>().bounds;
                var size = new Vector2(box.size.x * 0.9f, box.size.y * 0.8f);
                var from = new Vector2(box.center.x, box.center.y + 0.1f);
                var hits = new Collider2D[8];
                var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << World.ChunkColliders.TerrainLayer };
                string blocked = "一直走到头没被挡";
                float reached = from.x;
                for (float dx = 0f; dx < 120f; dx += 0.1f)
                {
                    var at = from + Vector2.right * dx;
                    int n = Physics2D.OverlapBox(at, size, 0f, filter, hits);
                    if (n > 0)
                    {
                        var region = World.TerraWorld.Live;
                        blocked = $"在 x={at.x:F1} 被挡：{string.Join("、", Enumerable.Range(0, n).Select(k => TentPath(hits[k].transform) + $"({hits[k].GetType().Name} {hits[k].bounds.min.ToString("F1")}~{hits[k].bounds.max.ToString("F1")})"))}";
                        break;
                    }
                    reached = at.x;
                }
                Plugin.Log.LogInfo($"[AutoTest] 格林帐篷 {c.Name}：从 x={from.x:F1} 往右，{blocked}（最远 {reached:F1}）");
                hero.transform.position = new Vector3(Mathf.Max(from.x, reached - 1f), hero.transform.position.y, hero.transform.position.z);
                yield return new WaitForSeconds(0.6f);
                Shot("Grimm_Main_Tent", $"grimmtent_{i}");
            }
            Plugin.Log.LogInfo("[AutoTest] 格林帐篷 完成");
        }

        static string TentPath(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var x = t; x != null && parts.Count < 4; x = x.parent) parts.Insert(0, x.name);
            return string.Join("/", parts);
        }
    }
}
