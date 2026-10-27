using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dashgate", the user's 10-26 "冲刺过图就会导致重力清零，任何冲刺，任何房间"): Dirtmouth and King's Pass, one
    /// dash of each kind (Tabi's, the Shield of Cthulhu's, Dashmaster's aimed, the Shade Cloak's shadow dash) out through the
    /// side gate between them; in the next scene, once the Knight has control again, his gravity, and how far he falls from
    /// three units up in half a second. TK_DG_BEFORE=1: the gates as before 10-26. Lines "[AutoTest] 冲刺过门 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DashGateTest()
        {
            Player.TerraStats.DevImmortal = true;
            Player.TerraMovement.DevOldGate = System.Environment.GetEnvironmentVariable("TK_DG_BEFORE") == "1";
            yield return GoTo("Town", "left1");
            var inv = mod.Inventory;
            string[] names = { "分趾袜冲刺", "克苏鲁护盾冲刺", "冲刺大师斜冲", "暗影冲刺" };
            int pass = 0;
            for (int k = 0; k < names.Length; k++)
            {
                var hero = HeroController.instance;
                var rb = hero.GetComponent<Rigidbody2D>();
                var gm = GameManager.instance;
                for (int a = 0; a < inv.Accessories.Length; a++) inv.Accessories[a] = default;
                inv.Accessories[0] = ItemStack.Of(k == 1 ? ItemType.ShieldOfCthulhu : k == 3 ? ItemType.ShadeCloak : ItemType.Tabi);
                inv.RefreshEffective();
                if (k == 3) PlayerData.instance.hasShadowDash = true;
                yield return new WaitForSeconds(0.5f);

                // The nearest live side gate, the Knight on the floor two and a half units inside it.
                string scene = gm.sceneName;
                TransitionPoint gate = null;
                float best = float.MaxValue;
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                {
                    if (tp == null || !tp.gameObject.scene.name.StartsWith(scene) || !(tp.name.StartsWith("left") || tp.name.StartsWith("right"))) continue;
                    var c = tp.GetComponent<Collider2D>();
                    if (c == null || !c.enabled || !tp.gameObject.activeInHierarchy) continue;
                    float d = Vector2.Distance(c.bounds.center, hero.transform.position);
                    if (d < best) { best = d; gate = tp; }
                }
                if (gate == null) { Plugin.Log.LogInfo($"[AutoTest] 冲刺过门 {names[k]} {scene} 没有侧门 不通过"); continue; }
                var gc = gate.GetComponent<Collider2D>().bounds;
                string gateName = gate.name;
                int dir = gateName.StartsWith("left") ? -1 : 1;
                var hcol = hero.GetComponent<BoxCollider2D>();
                float offset = hero.transform.position.y - hcol.bounds.min.y;
                hero.transform.position = new Vector3(gc.center.x - dir * 2.5f, gc.min.y + offset + 0.3f, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                if (dir > 0) hero.FaceRight(); else hero.FaceLeft();
                yield return new WaitForSeconds(0.8f);
                float before = rb.gravityScale;

                if (k == 3) AbilityAccessories.DevShadowKey = true;
                else if (k == 2) Player.TerraMovement.RequestDashToward(new Vector2(dir, 0.35f));
                else Player.TerraMovement.RequestDash(dir);
                float dashGravity = float.NaN;
                for (float t = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < t && gm.sceneName == scene && !gm.IsInSceneTransition;)
                {
                    if (Player.TerraMovement.IsDashing) dashGravity = rb.gravityScale;
                    yield return null;
                }
                bool crossed = gm.sceneName != scene || gm.IsInSceneTransition;
                for (float t = Time.realtimeSinceStartup + 20f; Time.realtimeSinceStartup < t && (gm.IsInSceneTransition || gm.sceneName == scene || !GameHooks.PlayerHasControl() || hero.cState.transitioning);) yield return null;
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                rb = hero.GetComponent<Rigidbody2D>();
                float after = rb.gravityScale;
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.up * 3f);
                rb.linearVelocity = Vector2.zero;
                yield return null;
                float y0 = hero.transform.position.y;
                yield return new WaitForSeconds(0.5f);
                float fell = y0 - hero.transform.position.y;
                bool ok = crossed && Mathf.Abs(after) > 0.3f && fell > 0.5f;
                if (ok) pass++;
                Plugin.Log.LogInfo($"[AutoTest] 冲刺过门 {names[k]} {scene} 从 {gateName} → {gm.sceneName}：过门 {crossed}，冲刺前重力 {before:F2}，冲刺中 {dashGravity:F2}，进门后 {after:F2}，半秒下落 {fell:F2} {(ok ? "通过" : "不通过")}");
            }
            for (int a = 0; a < inv.Accessories.Length; a++) inv.Accessories[a] = default;
            inv.RefreshEffective();
            Player.TerraMovement.DevOldGate = false;
            Plugin.Log.LogInfo($"[AutoTest] 冲刺过门 合计 {pass}/{names.Length}");
        }
    }
}
