using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dreamwake"): a dream seal used where the room has no dream-return door (Crossroads_01) and where it has
    /// one (Dirtmouth); in the dream, the dream left as the game leaves it after its boss (the Knight's "Dream Nail" to
    /// Leave Dream, returning); a timeline until the player is back where the seal was used, standing with control (jump
    /// pressed now and then, as waking from a dream asks).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DreamWakeTest()
        {
            int ok = 0, all = 0;
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            PlayerData.instance.hasDreamNail = true;
            var s = BossSummons.Find(ItemType.BossSigilFailedChampion);
            int stage = Shop.DevStage;
            foreach (var origin in new[] { "Crossroads_01", "Town" })
            {
                yield return SealTravel(origin, EntryGateOf(origin));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(1f);
                for (float t = Time.realtimeSinceStartup + 5f; !HeroController.instance.cState.onGround && Time.realtimeSinceStartup < t;) yield return null;
                var gm = GameManager.instance;
                Vector2 from = HeroController.instance.transform.position;
                Bestiary.Kills[s.Key] = 1;
                Shop.DevStage = s.Stage;
                inv.Slots[0] = ItemStack.Of(s.Type, 2);
                inv.Selected = 0;
                inv.Override = -1;
                mod.DevUseSelected(HeroController.instance.transform.position);
                for (float t = Time.realtimeSinceStartup + 20f; !BossArena.Arrived(s.Key) && Time.realtimeSinceStartup < t;) yield return null;
                yield return new WaitForSeconds(2f);
                // Out of the dream as its boss's end sends the player out.
                var hero = HeroController.instance;
                var nail = hero.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Nail");
                var ret = hero.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Return");
                if (nail?.FsmVariables.GetFsmBool("Dream Returning") != null) nail.FsmVariables.GetFsmBool("Dream Returning").Value = true;
                if (ret?.FsmVariables.GetFsmBool("Dream Returning") != null) ret.FsmVariables.GetFsmBool("Dream Returning").Value = true;
                string door = nail?.FsmVariables.GetFsmString("Return Door")?.Value;
                Plugin.Log.LogInfo($"[AutoTest] 梦醒 {origin}：梦里 {gm.sceneName}，返回 {PlayerData.instance.dreamReturnScene}/{door}");
                nail?.SetState("Leave Dream");
                var timeline = new List<string>();
                string last = null;
                float t0 = Time.realtimeSinceStartup, nextTap = 0f;
                bool back = false;
                while (Time.realtimeSinceStartup - t0 < 40f)
                {
                    var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                    var h = HeroController.SilentInstance;
                    string st = $"{gm.sceneName} 转场={gm.IsInSceneTransition} 淡入淡出={(fade != null ? fade.ActiveStateName : "?")} 可操作={GameHooks.PlayerHasControl()} 梦醒={(h != null ? h.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Return")?.ActiveStateName : "?")} 梦钉={(h != null ? h.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Nail")?.ActiveStateName : "?")} 位置={(h != null ? ((Vector2)h.transform.position).ToString("F1") : "?")}";
                    if (st != last) { timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s {st}"); last = st; }
                    if (gm.sceneName == origin && !gm.IsInSceneTransition && !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup >= nextTap && Time.realtimeSinceStartup - t0 > 4f)
                    {
                        nextTap = Time.realtimeSinceStartup + 2f;
                        mod.StartCoroutine(Tap(Key.Space));
                    }
                    if (gm.sceneName == origin && !gm.IsInSceneTransition && GameHooks.PlayerHasControl() && fade != null && fade.ActiveStateName == "Normal") { back = true; break; }
                    yield return null;
                }
                var hb = HeroController.instance.transform.position;
                all++;
                bool near = Vector2.Distance(hb, from) < 4f;
                if (back && near) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 梦醒 {origin}：{string.Join(" | ", timeline)}");
                Plugin.Log.LogInfo($"[AutoTest] 梦醒 {origin} 回到出发处 {back}，离出发点 {Vector2.Distance(hb, from):F1} {(back && near ? "通过" : "不通过")}");
                Shot(gm.sceneName, "dreamwake_" + origin);
                yield return new WaitForSeconds(0.3f);
            }
            Shop.DevStage = stage;
            System.Array.Copy(saved, inv.Slots, saved.Length);
            Plugin.Log.LogInfo($"[AutoTest] 梦醒 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
