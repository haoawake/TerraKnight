using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "essence"): the Forgotten Crossroads' scenes, and in the one with a Whispering Root
    /// (DreamPlant): its essence orbs before the Dream Nail wakes it (none drawn, as in the game), then
    /// the root struck as by the Dream Nail (a "Dream Attack" trigger), the orbs spread, and the player
    /// walked onto each: how much Essence that gave.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly System.Reflection.MethodInfo plantHit = AccessTools.Method(typeof(DreamPlant), "OnTriggerEnter2D");
        static readonly AccessTools.FieldRef<DreamPlantOrb, bool> orbActive = AccessTools.FieldRefAccess<DreamPlantOrb, bool>("isActive");
        static readonly AccessTools.FieldRef<DreamPlantOrb, bool> orbCanPickup = AccessTools.FieldRefAccess<DreamPlantOrb, bool>("canPickup");

        static string OrbState(DreamPlantOrb o)
        {
            var r = o.GetComponent<Renderer>();
            bool drawn = r != null && r.enabled && !r.forceRenderingOff && o.gameObject.activeInHierarchy;
            return $"{(Vector2)o.transform.position:F1} 画出 {drawn}（enabled {r?.enabled}，mod 藏 {r?.forceRenderingOff}），激活 {orbActive(o)}，能捡 {orbCanPickup(o)}";
        }

        IEnumerator EssenceTest()
        {
            var scenes = new List<string>();
            foreach (var s in UI.WorldMap.DevSceneNames()) if (s.StartsWith("Crossroads_")) scenes.Add(s);
            scenes.Sort(System.StringComparer.Ordinal);
            var gm = GameManager.instance;
            DreamPlant plant = null;
            string where = null;
            foreach (var scene in scenes)
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(1.5f);
                PlayerData.instance.isInvincible = true;
                if (gm.sceneName != scene) continue;
                foreach (var p in Object.FindObjectsByType<DreamPlant>(FindObjectsSortMode.None))
                    if (p.gameObject.scene.name == scene) { plant = p; where = scene; break; }
                if (plant != null) break;
            }
            if (plant == null) { Plugin.Log.LogInfo("[AutoTest] 梦之精华 十字路里没找到低语之根 不通过"); yield break; }
            // As the player comes: from the next room, where this one was loaded beside it in the
            // background, through the door.
            TransitionPoint door = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.gameObject.scene.name == where && !string.IsNullOrEmpty(tp.targetScene) && !string.IsNullOrEmpty(tp.entryPoint)
                    && (tp.name.StartsWith("left") || tp.name.StartsWith("right"))) { door = tp; break; }
            if (door != null)
            {
                string next = door.targetScene, back = door.entryPoint;
                yield return GoTo(next, back);
                yield return new WaitForSeconds(3f);
                PlayerData.instance.isInvincible = true;
                // Loaded beside this room in the background: its orbs as the seamless world left them.
                {
                    int drawn = 0, hidden = 0, all = 0;
                    foreach (var o in Object.FindObjectsByType<DreamPlantOrb>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (o.gameObject.scene.name != where) continue;
                        all++;
                        var r = o.GetComponent<Renderer>();
                        if (r == null) continue;
                        if (r.forceRenderingOff) hidden++;
                        if (r.enabled && !r.forceRenderingOff && o.gameObject.activeInHierarchy && !orbActive(o)) drawn++;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 梦之精华 站在 {next}，{where} 在后台载入着：{all} 个精华球，被无缝世界藏着的 {hidden} 个，没打根就画出来的 {drawn} 个");
                }
                TransitionPoint backDoor = null;
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                    if (tp.gameObject.scene.name == next && tp.name == back) backDoor = tp;
                var h0 = HeroController.instance;
                if (backDoor != null)
                {
                    // Walked across the door line, as the player goes (the seamless world takes the room next door as it is).
                    int dir = back.StartsWith("left") ? -1 : 1;
                    float t0 = Time.time;
                    Player.TerraMovement.DevMove = dir;
                    while (gm.sceneName != where && Time.time - t0 < 8f) yield return null;
                    Player.TerraMovement.DevMove = 0;
                }
                yield return new WaitForSeconds(1.5f);
                Plugin.Log.LogInfo($"[AutoTest] 梦之精华 从 {next} 穿过 {back} 走进 {where}：现在在 {gm.sceneName}");
                plant = null;
                foreach (var p in Object.FindObjectsByType<DreamPlant>(FindObjectsSortMode.None))
                    if (p.gameObject.scene.name == where) plant = p;
                if (plant == null) { Plugin.Log.LogInfo("[AutoTest] 梦之精华 走进去后找不到低语之根 不通过"); yield break; }
            }
            var orbs = new List<DreamPlantOrb>();
            foreach (var o in Object.FindObjectsByType<DreamPlantOrb>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (o.gameObject.scene.name == where) orbs.Add(o);
            int drawnBefore = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var o in orbs)
            {
                var r = o.GetComponent<Renderer>();
                if (r != null && r.enabled && !r.forceRenderingOff && o.gameObject.activeInHierarchy && !orbActive(o)) drawnBefore++;
                if (lines.Length < 3000) lines.Append("\n  ").Append(OrbState(o));
            }
            Plugin.Log.LogInfo($"[AutoTest] 梦之精华 {where} 的低语之根 {(Vector2)plant.transform.position:F1}：{orbs.Count} 个精华球，打根之前画出来的 {drawnBefore} 个{lines}");
            var hero = HeroController.instance;
            Player.TerraCamera.DevLook = plant.transform.position;
            yield return new WaitForSeconds(0.5f);
            Shot(where, "essence_before");
            yield return null;
            Player.TerraCamera.DevLook = null;

            // The Dream Nail's strike: a "Dream Attack" trigger entering the root.
            var strike = new GameObject("Dev Dream Attack") { tag = "Dream Attack" };
            strike.transform.position = plant.transform.position;
            var sc = strike.AddComponent<BoxCollider2D>();
            sc.isTrigger = true;
            int before = PlayerData.instance.GetInt("dreamOrbs");
            plantHit.Invoke(plant, new object[] { sc });
            Object.Destroy(strike);
            yield return new WaitForSeconds(4.5f);
            int ready = 0;
            foreach (var o in orbs) if (orbActive(o) && orbCanPickup(o)) ready++;
            // The player onto each orb in turn.
            int collected = 0;
            foreach (var o in orbs)
            {
                if (!orbActive(o)) continue;
                int n0 = PlayerData.instance.GetInt("dreamOrbs");
                hero.transform.position = o.transform.position;
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.15f);
                if (PlayerData.instance.GetInt("dreamOrbs") > n0) collected++;
            }
            yield return new WaitForSeconds(0.5f);
            int gained = PlayerData.instance.GetInt("dreamOrbs") - before;
            int drawnAfter = 0;
            foreach (var o in orbs)
            {
                var r = o.GetComponent<Renderer>();
                if (r != null && r.enabled && !r.forceRenderingOff && o.gameObject.activeInHierarchy) drawnAfter++;
            }
            bool pass = orbs.Count > 0 && drawnBefore == 0 && ready == orbs.Count && gained == orbs.Count && drawnAfter == 0;
            Plugin.Log.LogInfo($"[AutoTest] 梦之精华 打根后：{ready}/{orbs.Count} 个能捡，人物碰到捡起 {collected} 个，梦之精华 +{gained}，捡完还画着的 {drawnAfter} 个 {(pass ? "通过" : "不通过")}");
            Shot(where, "essence_after");
        }
    }
}
