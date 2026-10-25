using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mountride"): sat on every saddled mount (riding legs, the mount's body frame, the height over its middle;
    /// photos), a horse that rams only once run up to its charge, the Chillets' own dash (16 px a tick, bled off, its cooldown)
    /// hurting only inside it, and no worn dash on a mount (the Shade Cloak's, the Knight's, the Crystal Heart's).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MountRideTest()
        {
            int checks = 0, failures = 0, oldStage = Shop.DevStage;
            void Check(string name, bool pass, string detail = "")
            {
                checks++; if (!pass) failures++;
                Plugin.Log.LogInfo("[AutoTest] 骑乘/" + name + " " + detail + (pass ? " 通过" : " 不通过"));
            }
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false); mod.Inventory.Infinite = false; Shop.DevStage = 5;
            // Every mount may be ridden (the stage locks are the mounts suite's).
            bool oldFree = mod.Inventory.FreeMaking;
            mod.Inventory.FreeMaking = true;
            PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            Equip(Array.Empty<ItemType>(), Array.Empty<ItemType>());
            var hero = HeroController.instance; var rb = hero.GetComponent<Rigidbody2D>(); var col = hero.GetComponent<BoxCollider2D>();
            var start = Clear(hero, new Vector2(110f, 13f));
            void Place(Vector2 at) { Mounts.Dismount(); hero.transform.position = at; rb.position = at; rb.linearVelocity = Vector2.zero; }
            var photos = new[] { 62, 64, 40, 10, 3, 7, 23, 46, 0, 17 };
            foreach (var m in Mounts.All)
            {
                if (!Mounts.Seated(m)) continue;
                Place(start);
                yield return new WaitForSeconds(.1f);
                if (!Mounts.TryMount(m, hero)) { Check("上坐骑/" + m.Native, false); continue; }
                yield return new WaitForSeconds(.25f);
                var pose = mod.NetPose;
                int leg = m.Native == 23 || m.Native == 45 || m.Native == 48 ? 0 : 6;
                float rise = (pose.Feet.y - (col.bounds.min.y + TerraMovement.StepOffset)) * 32f;
                float want = Mounts.RiderRise(m, Vector2.zero, true) * 32f;
                Check("坐姿/" + m.Native, pose.LegOverride == leg && pose.BodyOverride == Mounts.SeatOf(m).Body,
                    $"{m.Name} 腿 {pose.LegOverride}（应 {leg}），身体 {pose.BodyOverride}（应 {Mounts.SeatOf(m).Body}）");
                Check("坐高/" + m.Native, Mathf.Abs(rise - want) < 6f && want > -12f && want < 64f, $"脚离地 {rise:F0} px（算出 {want:F0}，鞍高 {m.Lift}）");
                if (photos.Contains(m.Native)) { yield return new WaitForEndOfFrame(); RenderMountPhoto(m.Native, "_seat"); }
            }

            // A horse runs up to its charge: at its run speed it rams nothing, only past dashSpeed - runSpeed / 2.
            Place(start);
            yield return new WaitForSeconds(.2f);
            Mounts.TryMount(Mounts.Get(40), hero);
            yield return new WaitForSeconds(.3f);
            TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(.5f);
            float early = Mathf.Abs(rb.linearVelocity.x) / TerraMovement.PxT;
            float x0 = hero.transform.position.x, late = early;
            for (float t = 0f; t < 3f; t += Time.deltaTime) { late = Mathf.Max(late, Mathf.Abs(rb.linearVelocity.x) / TerraMovement.PxT); yield return null; }
            TerraMovement.DevMove = 0;
            var horse = Mounts.Get(40);
            float past = horse.Dash - horse.Run / 2f;
            Check("马先跑后冲", early < past && early > horse.Run * .8f && late > past,
                $"半秒 {early:F2} px/tick，三秒内最快 {late:F2}（冲撞线 {past:F2}，跑过 {hero.transform.position.x - x0:F1}）");
            yield return new WaitForSeconds(.5f);

            // The Chillets: their dash, the Dash's speed bled off to the dash speed, then 20 ticks before the next.
            foreach (int id in new[] { 62, 64 })
            {
                Place(start);
                yield return new WaitForSeconds(.2f);
                Mounts.TryMount(Mounts.Get(id), hero);
                for (float t = 0f; t < 1f && !hero.cState.onGround; t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(.2f);
                int dashes = Mounts.DevDashes;
                Mounts.RequestDash(1);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                float fast = rb.linearVelocity.x / TerraMovement.PxT;
                bool dashing = Mounts.Dashing;
                float until = Time.time + 2f;
                while (Mounts.Dashing && Time.time < until) yield return null;
                float after = rb.linearVelocity.x / TerraMovement.PxT;
                Mounts.RequestDash(1);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                bool cooled = !Mounts.Dashing;
                Check("疾旋鼬冲刺/" + id, Mounts.DevDashes == dashes + 1 && dashing && fast > 12f && after <= Mounts.Get(id).Dash + .5f && cooled,
                    $"起速 {fast:F1} px/tick，冲刺中 {dashing}，结束 {after:F2}（冲刺速 {Mounts.Get(id).Dash}），结束后马上再冲被冷却挡下 {cooled}");
            }

            // No worn dash on a mount: the Shade Cloak's tap, the Knight's own dash, the Crystal Heart's.
            var inv = mod.Inventory;
            for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
            inv.Accessories[0] = ItemStack.Of(ItemType.HallownestShield);
            inv.RefreshEffective();
            AbilityAccessories.DevDashKey = false;
            AbilityAccessories.DevReset();
            Place(start);
            yield return new WaitForSeconds(.3f);
            Mounts.TryMount(Mounts.Get(62), hero);
            yield return new WaitForSeconds(.3f);
            int shadows = AbilityAccessories.DevShadowDashes, worn = TerraMovement.DevDashes;
            bool tap = AbilityAccessories.TapShadowDash(1);
            TerraMovement.RequestDash(1);
            yield return new WaitForSeconds(.3f);
            bool superDash = true;
            Player.PlayerHooks.DevDuringAbilitySwap(hero, () => superDash = hero.CanSuperDash());
            Check("骑乘时饰品冲刺全关", !tap && AbilityAccessories.DevShadowDashes == shadows && TerraMovement.DevDashes == worn && !superDash && Mounts.Riding,
                $"暗影 {tap}，饰品冲刺 {TerraMovement.DevDashes - worn} 次，超级冲刺 {superDash}，仍在坐骑上 {Mounts.Riding}");
            Mounts.Dismount();
            yield return new WaitForSeconds(.3f);
            Player.PlayerHooks.DevDuringAbilitySwap(hero, () => superDash = hero.CanSuperDash());
            Check("下坐骑后冲刺回来", superDash && !TerraMovement.IsDashing, $"超级冲刺 {superDash}，下坐骑时没有补冲 {!TerraMovement.IsDashing}");
            for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
            inv.RefreshEffective();
            AbilityAccessories.DevDashKey = null;

            // The Chillet hurts only inside its dash; a horse only charging (the enemy's collider held on the mount's).
            yield return GoTo("Crossroads_03", "left1");
            hero = HeroController.instance; rb = hero.GetComponent<Rigidbody2D>(); col = hero.GetComponent<BoxCollider2D>();
            var seed = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .FirstOrDefault(h => !h.isDead && h.gameObject.scene.name == "Crossroads_03" && !h.IsInvincible && h.GetComponent<Collider2D>() is Collider2D c && c.enabled
                    && ProgressionLoot.Eligible(EnemyDrops.EntryFor(UI.EnemyHover.RecordOf(h))));
            Check("测试用敌人", seed != null, seed != null ? seed.name : "");
            if (seed != null)
            {
                foreach (var danger in UnityEngine.Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None)) danger.enabled = false;
                var targetObject = UnityEngine.Object.Instantiate(seed.gameObject);
                foreach (var danger in targetObject.GetComponentsInChildren<DamageHero>()) danger.enabled = false;
                var target = targetObject.GetComponent<HealthManager>(); var tb = target.GetComponent<Rigidbody2D>(); var tc = target.GetComponent<Collider2D>();
                foreach (var fsm in target.GetComponents<PlayMakerFSM>()) fsm.enabled = false;
                // Where the room's door set the player down (a known floor, no hazard).
                for (float t = 0f; t < 6f && !GameHooks.PlayerHasControl(); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(.5f);
                Player.TerraStats.DevImmortal = true;
                Vector2 at = hero.transform.position;
                Vector2 off = (Vector2)tc.bounds.center - (Vector2)target.transform.position;
                foreach (int id in new[] { 62, 40 })
                {
                    // A spot with the mount's headroom (a horse stands a tile taller).
                    bool mounted = false;
                    for (int k = 0; k < 8 && !mounted; k++)
                    {
                        Place(at + Vector2.right * (k * 1.5f));
                        target.hp = 20000; target.isDead = false;
                        yield return new WaitForSeconds(.4f);
                        mounted = Mounts.TryMount(Mounts.Get(id), hero);
                    }
                    yield return new WaitForSeconds(.3f);
                    // Walking (half a second's run from a stand): the enemy inside the mount takes nothing.
                    int hits = Mounts.DevHits;
                    TerraMovement.DevMove = 1;
                    for (float t = 0f; t < .5f; t += Time.deltaTime) { Pin(target, tb, (Vector2)col.bounds.center - off); yield return null; }
                    TerraMovement.DevMove = 0;
                    int walking = Mounts.DevHits - hits;
                    for (float t = 0f; t < .6f; t += Time.deltaTime) { Pin(target, tb, (Vector2)col.bounds.center + Vector2.right * 6f - off); yield return null; }
                    // Charging: the Chillet's dash, the horse at its charge.
                    hits = Mounts.DevHits;
                    int dashes = Mounts.DevDashes;
                    float x1 = hero.transform.position.x, fastest = 0f;
                    string state = $"能动 {hero.acceptingInput} 后退 {hero.cState.recoiling} 地上 {hero.cState.onGround} 状态 {hero.hero_state}";
                    if (id == 62) Mounts.RequestDash(1);
                    else { Mounts.DevVx = Mounts.Get(id).Dash; TerraMovement.DevMove = 1; }
                    for (float t = 0f; t < .4f; t += Time.deltaTime)
                    {
                        Pin(target, tb, (Vector2)col.bounds.center - off);
                        fastest = Mathf.Max(fastest, Mathf.Abs(rb.linearVelocity.x) / TerraMovement.PxT);
                        yield return null;
                    }
                    state += $"，这次冲刺 {Mounts.DevDashes - dashes} 次，最快 {fastest:F1} px/tick，移动 {hero.transform.position.x - x1:F2}";
                    TerraMovement.DevMove = 0;
                    int charging = Mounts.DevHits - hits;
                    Check("只在冲刺时撞伤/" + id, walking == 0 && charging > 0,
                        $"骑上 {mounted}，走着撞到 {walking} 次，冲起来撞到 {charging} 次（{state}，敌人碰撞体 {tc.enabled}，距离 {Vector2.Distance(tc.bounds.center, col.bounds.center):F2}，血 {target.hp}）");
                    Mounts.Dismount();
                }
                UnityEngine.Object.Destroy(targetObject);
            }
            Mounts.Dismount();
            Shop.DevStage = oldStage;
            mod.Inventory.FreeMaking = oldFree;
            Plugin.Log.LogInfo($"[AutoTest] 骑乘 {checks - failures}/{checks} {(failures == 0 ? "全部通过" : "有不通过")}");
        }
    }
}
