using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17georock"): the geo rocks pay 150 geo each. Every rock of a few rooms set to 150 (its FSM's flings);
    /// one broken with the pickaxe from afar, the geo lying about and the geo picked up together 150 more than before.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static int GeoLying()
        {
            int sum = 0;
            foreach (var g in Object.FindObjectsByType<GeoControl>(FindObjectsSortMode.None))
                if (g != null && g.gameObject.activeInHierarchy && g.sizes != null && g.type >= 0 && g.type < g.sizes.Length) sum += g.sizes[g.type].value;
            return sum;
        }

        IEnumerator GeoRock17()
        {
            var pays = new System.Collections.Generic.List<string>();
            int wrong = 0, rocksSeen = 0;
            foreach (var scene in new[] { "Crossroads_07", "Fungus1_02", "Mines_02" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(1.5f);
                foreach (var r in Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None))
                {
                    var fsm = r.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Geo Rock");
                    int hits = fsm != null ? fsm.FsmVariables.GetFsmInt("Hits").Value : -1;
                    if (hits <= 0) continue;
                    rocksSeen++;
                    int pay = GeoRockPayout.DevPayout(r);
                    if (pay != GeoRockPayout.Total) wrong++;
                    pays.Add($"{scene}/{r.name} {hits}下 {pay}");
                }
            }
            FxCheck("每块吉欧堆共给 150 吉欧", rocksSeen > 0 && wrong == 0, $"{rocksSeen} 块：{string.Join("，", pays.Take(8))}");

            // One broken from afar (the geo left lying), in the last room.
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            GeoRock rock = null;
            PlayMakerFSM rf = null;
            foreach (var r in Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None))
            {
                var fsm = r.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Geo Rock");
                if (fsm != null && r.gameObject.activeInHierarchy && fsm.FsmVariables.GetFsmInt("Hits").Value > 0) { rock = r; rf = fsm; break; }
            }
            var col = rock != null ? rock.GetComponentInChildren<Collider2D>() : null;
            if (rock == null || col == null)
            {
                FxCheck("敲碎一块吉欧堆", false, "这个房间没有完好的吉欧堆");
                yield break;
            }
            float gravity = rb.gravityScale;
            hero.transform.position = Clear(hero, (Vector2)col.bounds.center + new Vector2(-8f, 1f));
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            yield return new WaitForSeconds(0.5f);
            int lying0 = GeoLying(), geo0 = PlayerData.instance.geo;
            int strikes = 0;
            for (; strikes < 12 && rf.FsmVariables.GetFsmInt("Hits").Value > 0; strikes++)
            {
                mod.TestStrike(col, 1f);
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(2.5f);
            Shot(GameManager.instance.sceneName, "georock_broken");
            int got = GeoLying() - lying0 + PlayerData.instance.geo - geo0;
            rb.gravityScale = gravity;
            FxCheck("敲碎一块吉欧堆掉出 150 吉欧", got == GeoRockPayout.Total, $"{rock.name} 敲了 {strikes} 下，状态 {rf.ActiveStateName}，掉出 {got} 吉欧（地上 {GeoLying() - lying0}，捡到 {PlayerData.instance.geo - geo0}）");
        }
    }
}
