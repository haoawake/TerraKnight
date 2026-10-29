using System.Collections;
using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "divefloor", the user's 10-27 "白色防御者右边下去需要下砸的地方下不去，砸不了"): the dive floors of boss rooms
    /// (Dung Defender's arena down to his sleeping chamber, the chamber itself, Soul Master's), the boss beaten: each floor's
    /// place, whether the room keeps its ground and whether a boss is up, then the Knight set on it and its slam's landing
    /// (DiveFloors.Pound) — the floor breaks. Shots &lt;scene&gt;_divefloor_&lt;n&gt;. Lines "[AutoTest] 下砸地板 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DiveFloorTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            pd.SetBool("defeatedDungDefender", true);
            pd.SetBool("mageLordDefeated", true);
            int ok = 0, all = 0;
            foreach (var scene in new[] { "Waterways_05", "Waterways_15", "Ruins1_24" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(2.5f);
                var hero = HeroController.instance;
                var floors = new List<PlayMakerFSM>();
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if ((f.FsmName == "quake_floor" || f.FsmName == "Detect Quake") && f.gameObject.scene.name == scene && f.gameObject.activeInHierarchy) floors.Add(f);
                Plugin.Log.LogInfo($"[AutoTest] 下砸地板 {scene}：{floors.Count} 块，首领房 {BossRooms.IsRoom(scene)}");
                int k = 0;
                foreach (var f in floors)
                {
                    k++;
                    if (f == null) continue;
                    var col = f.GetComponent<Collider2D>() ?? f.GetComponentInChildren<Collider2D>();
                    if (col == null) { Plugin.Log.LogInfo($"[AutoTest] 下砸地板 {scene} #{k} {f.gameObject.name}：没有碰撞体"); continue; }
                    var b = col.bounds;
                    var top = new Vector2(b.center.x, b.max.y);
                    string state0 = f.ActiveStateName;
                    bool kept = BossRooms.ProtectsNativeArea(top + Vector2.down * 0.25f), up = BossRooms.BossUp(top + Vector2.down * 0.25f);
                    hero.transform.position = Clear(hero, top + Vector2.up * 0.8f);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.6f);
                    string name = f.gameObject.name;
                    int broke = DiveFloors.Pound(hero.GetComponent<BoxCollider2D>().bounds);
                    yield return new WaitForSeconds(1.2f);
                    all++;
                    if (broke > 0) ok++;
                    string now = GameManager.instance.sceneName;
                    Plugin.Log.LogInfo($"[AutoTest] 下砸地板 {scene} #{k} {name} 在 {b.center:F1}（{b.size:F1}），状态 {state0}→{(f != null ? f.ActiveStateName : "没了")}，"
                        + $"保护原生地形 {kept}，首领在场 {up}，砸开 {broke}{(now != scene ? $"，掉进 {now}" : "")} {(broke > 0 ? "通过" : "不通过")}");
                    Shot(now, $"divefloor_{scene}_{k}");
                    // Fallen through to the room below: this room's other floors another time.
                    if (now != scene) break;
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 下砸地板 合计 {ok}/{all}");
        }
    }
}
