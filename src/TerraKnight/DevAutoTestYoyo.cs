using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "yoyo", yoyo/spec-yoyo.md Y.1): the yoyos held out (reach and life against the
    /// table, with and without a string, the endless ones), their hits and children on an enemy
    /// held in place, the Yoyo Glove's second yoyo, a counterweight, the Magic String's copy; and
    /// close-ups of the string and the yoyos' looks.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator YoyoTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            Wear();
            var tp = TerraProjectiles.Instance;

            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float dd = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (dd < best) { best = dd; target = hm; }
            }
            var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            var near = C() + Vector2.right * side * 2.5f;
            // Away from the enemy, for reach and life: the enemy parked far off, the cursor 600 px out.
            var away = C() + Vector2.right * side * 600f * Effects.TerraParticles.Px;
            void Park(Vector2 at)
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 5000);
                Pin(target, trb, at);
            }

            // One throw held for a while (the button held, the cursor fixed), then let go.
            float far = 0f, outFor = 0f, back = 0f;
            int maxOut = 0;
            IEnumerator Throw(ItemType item, ProjKind kind, float hold, Vector2 aim, Vector2 enemyAt, string shot = null, float shotAt = 0.8f)
            {
                inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(item);
                inv.Selected = Inventory.HotbarSize - 1;
                while (!mod.DevReady) yield return null;
                mod.DevAim = aim;
                mod.DevHold = true;
                mod.DevUseSelected(aim);
                far = 0f;
                outFor = -1f;
                maxOut = 0;
                bool pictured = shot == null;
                for (float t = 0f; t < hold; t += Time.deltaTime)
                {
                    Park(enemyAt);
                    mod.DevAim = aim;
                    far = Mathf.Max(far, tp.DevFarthest(kind, C()));
                    maxOut = Mathf.Max(maxOut, tp.DevCount(kind));
                    if (outFor < 0f && t > 0.2f && tp.DevReturning(kind) > 0) outFor = t;
                    if (!pictured && t > shotAt)
                    {
                        pictured = true;
                        yield return CloseUp(scene, shot, hero, 160, 2);
                    }
                    yield return null;
                }
                mod.DevHold = false;
                back = 0f;
                for (float t = 0f; t < 3f && tp.DevCount(kind) > tp.DevLoose(kind); t += Time.deltaTime)
                {
                    Park(enemyAt);
                    back = t;
                    yield return null;
                }
                mod.DevAim = null;
                mod.DevHold = null;
            }
            var parked = C() + Vector2.right * -side * 30f;

            // Reach and life (Y.1.1): the Wooden Yoyo 130 px and 3 s; with a string 192.5 px and 4.5 s.
            yield return Throw(ItemType.WoodYoyo, ProjKind.WoodenYoyo, 4f, away, parked, "y_string", 1f);
            Plugin.Log.LogInfo($"[AutoTest] 悠悠球 木悠悠球：最远 {far:F0}px（原版 130），{outFor:F1} 秒后开始收回（原版 3），收回用 {back:F1} 秒");
            Wear(ItemType.WhiteString);
            yield return null;
            yield return Throw(ItemType.WoodYoyo, ProjKind.WoodenYoyo, 5.5f, away, parked);
            Plugin.Log.LogInfo($"[AutoTest] 悠悠球 木悠悠球+白绳：最远 {far:F0}px（原版 192.5），{outFor:F1} 秒后收回（原版 4.5）");
            Wear();
            yield return null;
            yield return Throw(ItemType.Code2, ProjKind.Code2Yoyo, 4f, away, parked);
            Plugin.Log.LogInfo($"[AutoTest] 悠悠球 代码2球：最远 {far:F0}px（原版 280），按住 4 秒收回={outFor >= 0f}（原版 不收，无限），松手后 {back:F1} 秒收回");

            if (target != null)
            {
                // The hits and children on the held enemy.
                foreach (var (item, kind, child, label, pic) in new[]
                {
                    (ItemType.Cascade, ProjKind.CascadeYoyo, ProjKind.CascadeBurst, "喷流球 烈焰爆炸（每 2 次计数命中）", (string)null),
                    (ItemType.Chik, ProjKind.ChikYoyo, ProjKind.ChikCrystal, "吉克球 水晶（每 3 次 5 颗）", "y_chik"),
                    (ItemType.Kraken, ProjKind.KrakenYoyo, ProjKind.KrakenWave, "克拉肯球 巨浪（每 4 次 3 道）", "y_kraken"),
                    (ItemType.TheEyeOfCthulhu, ProjKind.EyeYoyo, ProjKind.EyeLunge, "克苏鲁之眼 猛冲（每 7 次）", "y_eye"),
                    (ItemType.Terrarian, ProjKind.TerrarianYoyo, ProjKind.TerrarianOrb, "泰拉悠悠球 光球（每 6 tick）", "y_terrarian"),
                    (ItemType.HiveFive, ProjKind.HiveFiveYoyo, ProjKind.Bee, "蜂巢球 蜜蜂（1/3）", (string)null),
                    (ItemType.Gradient, ProjKind.GradientYoyo, ProjKind.None, "渐变球", "y_gradient"),
                    (ItemType.HelFire, ProjKind.HelFireYoyo, ProjKind.None, "狱火球", "y_helfire"),
                })
                {
                    tp.DevClear();
                    EnemyDebuffs.Clear();
                    int made0 = child != ProjKind.None ? TerraProjectiles.DevMade(child) : 0, hits0 = TerraProjectiles.DevHit(kind);
                    yield return Throw(item, kind, 4f, near, near, pic, 1.2f);
                    Plugin.Log.LogInfo($"[AutoTest] 悠悠球 {label}：命中 {TerraProjectiles.DevHit(kind) - hits0}，子弹幕 {(child != ProjKind.None ? TerraProjectiles.DevMade(child) - made0 : 0)}，" +
                                       $"狱火={EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire)} 着火={EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)}");
                }
                foreach (var (item, kind, debuff, label) in new[]
                {
                    (ItemType.Amarok, ProjKind.AmarokYoyo, EnemyDebuffs.Frostbite, "冰雪悠悠球 冻伤（2/3）"),
                    (ItemType.Yelets, ProjKind.YeletsYoyo, EnemyDebuffs.Venom, "叶列茨球 酸性毒液"),
                })
                {
                    EnemyDebuffs.Clear();
                    yield return Throw(item, kind, 1.5f, near, near);
                    Plugin.Log.LogInfo($"[AutoTest] 悠悠球 {label}：减益={EnemyDebuffs.Has(target, debuff)}");
                }

                // The glove's second yoyo; a counterweight; the Magic String's copy.
                tp.DevClear();
                Wear(ItemType.YoYoGlove);
                yield return null;
                yield return Throw(ItemType.Valor, ProjKind.ValorYoyo, 2.5f, near, near, "y_glove", 1.5f);
                Plugin.Log.LogInfo($"[AutoTest] 悠悠球 悠悠球手套：同时 {maxOut} 个（原版 2）");
                tp.DevClear();
                Wear(ItemType.BlackCounterweight);
                yield return null;
                int w0 = TerraProjectiles.DevMade(ProjKind.CounterweightBlack);
                yield return Throw(ItemType.Valor, ProjKind.ValorYoyo, 2.5f, near, near, "y_counterweight", 1.5f);
                Plugin.Log.LogInfo($"[AutoTest] 悠悠球 黑平衡锤：平衡锤 {TerraProjectiles.DevMade(ProjKind.CounterweightBlack) - w0}（每个悠悠球至多 1），命中 {TerraProjectiles.DevHit(ProjKind.CounterweightBlack)}");
                tp.DevClear();
                Wear(ItemType.MagicString);
                yield return null;
                yield return Throw(ItemType.Code2, ProjKind.Code2Yoyo, 1.2f, near, near);
                int loose = tp.DevLoose(ProjKind.Code2Yoyo);
                yield return CloseUp(scene, "y_loose", hero, 160, 2);
                Plugin.Log.LogInfo($"[AutoTest] 悠悠球 魔法绳：收回时离绳副本 {loose}（原版 1，75% 伤害，3-6 次命中）");
                Wear();

                // The flails: spun on the held button (hits round the player), thrown on release (twice the damage), back.
                var close = C() + Vector2.right * side * 1.2f;
                IEnumerator Flail(ItemType item, ProjKind kind, float spin, Vector2 enemyAt, string shot, bool drag = false, float after = 2f)
                {
                    inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(item);
                    inv.Selected = Inventory.HotbarSize - 1;
                    while (!mod.DevReady) yield return null;
                    var aim = C() + Vector2.right * side * 6f;
                    mod.DevAim = aim;
                    mod.DevHold = true;
                    mod.DevUseSelected(aim);
                    far = 0f;
                    maxOut = 0;
                    for (float t = 0f; t < spin; t += Time.deltaTime)
                    {
                        Park(enemyAt);
                        maxOut = Mathf.Max(maxOut, tp.DevCount(kind));
                        if (shot != null && t > spin * 0.6f && t - Time.deltaTime <= spin * 0.6f) yield return CloseUp(scene, shot + "_spin", hero, 100, 3);
                        yield return null;
                    }
                    mod.DevHold = false;
                    bool pictured = shot == null;
                    for (float t = 0f; t < after && tp.DevCount(kind) > 0; t += Time.deltaTime)
                    {
                        Park(enemyAt);
                        far = Mathf.Max(far, tp.DevFarthest(kind, C()));
                        if (drag && t > 0.12f && t < 1.2f) mod.DevHold = true;
                        if (drag && t >= 1.2f) mod.DevHold = false;
                        if (!pictured && t > 0.18f)
                        {
                            pictured = true;
                            yield return CloseUp(scene, shot + "_thrown", hero, 200, 2);
                        }
                        yield return null;
                    }
                    mod.DevHold = null;
                    mod.DevAim = null;
                }
                foreach (var (item, kind, child, label, expect, pic) in new[]
                {
                    (ItemType.BallOHurt, ProjKind.BallOHurtFlail, ProjKind.None, "链球", "15 tick × 14 = 210px", "f_ballohurt"),
                    (ItemType.Sunfury, ProjKind.SunfuryFlail, ProjKind.None, "阳炎之怒", "15 × 17 = 255px，1/2 着火", "f_sunfury"),
                    (ItemType.FlowerPow, ProjKind.FlowerPowFlail, ProjKind.FlowerPowPetal, "花之力（每 20 tick 一片花瓣）", "13 × 23 = 299px", "f_flowerpow"),
                    (ItemType.FlaironFlail, ProjKind.FlaironFlail, ProjKind.FlaironBubble, "猪鲨链球（每 3-5 tick 一个泡泡）", "16 × 23 = 368px", "f_flairon"),
                    (ItemType.DripplerFlail, ProjKind.DripplerFlail, ProjKind.DripplerBall, "滴滴怪致残者（投出后第二个球）", "13 × 22 = 286px", (string)null),
                })
                {
                    tp.DevClear();
                    EnemyDebuffs.Clear();
                    int hits0 = TerraProjectiles.DevHit(kind), made0 = child != ProjKind.None ? TerraProjectiles.DevMade(child) : 0;
                    yield return Flail(item, kind, 1.5f, close, pic);
                    Plugin.Log.LogInfo($"[AutoTest] 链枷 {label}：命中 {TerraProjectiles.DevHit(kind) - hits0}，投出最远 {far:F0}px（原版 {expect}），子弹幕 {(child != ProjKind.None ? TerraProjectiles.DevMade(child) - made0 : 0)}，着火={EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)}");
                }
                // Pressed again after the throw: dragged along the ground, then back.
                tp.DevClear();
                yield return Flail(ItemType.BlueMoon, ProjKind.BlueMoonFlail, 0.6f, parked, "f_drag", drag: true, after: 3f);
                Plugin.Log.LogInfo($"[AutoTest] 链枷 蓝月 再按住拖拽：最远 {far:F0}px，结束时还在={tp.DevCount(ProjKind.BlueMoonFlail)}");
                // Blue Moon with Sunfury in the bag: both thrown at once (1.4.5).
                tp.DevClear();
                inv.Slots[Inventory.HotbarSize - 2] = ItemStack.Of(ItemType.Sunfury);
                yield return Flail(ItemType.BlueMoon, ProjKind.BlueMoonFlail, 1.2f, close, "f_dual");
                Plugin.Log.LogInfo($"[AutoTest] 链枷 蓝月+阳炎之怒：阳炎之怒同时出 {TerraProjectiles.DevMade(ProjKind.SunfuryFlail)}（原版 1，镜像旋转）");
                inv.Slots[Inventory.HotbarSize - 2] = default;
            }

            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = savedSlots[i];
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            inv.Selected = savedSelected;
            mod.DevHold = null;
            mod.DevAim = null;
            mod.DevNoFallDamage = false;
        }
    }
}
