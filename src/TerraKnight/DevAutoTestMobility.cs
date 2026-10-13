using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mobility", accarmor-hm sec-G1b): the ninja dash against the Shield of Cthulhu's
    /// (top speed, ticks, distance, afterimages, the later slot winning); the Black Belt's dodge
    /// rate, alone and with the Mystic Arts Sash; the Sail jump's rise and its place in the order;
    /// the Portable Stool and the jump off it; Magiluminescence's run; how the new pieces look.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MobilityTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            // The jumps, the stool and the looks where the scene starts (lit, with head room); the dashes and
            // the runs on the long open stretch nearest to it.
            Vector3 start = Clear(hero, (Vector2)hero.transform.position);
            float dir = 1f;
            Vector3 home = start;
            if (OpenRun(hero, 22f, out var runAt, out float runDir, start))
            {
                home = Clear(hero, runAt);
                dir = runDir;
            }
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 机动：开阔处 {home}，朝 {dir}；起点 {start}，头顶 {HeadRoom(col) / Pxu:F0}px");

            // --- The dashes, no input after the double tap.
            foreach (var (wear, name, expect) in new[]
            {
                (new[] { ItemType.Tabi }, "Tabi", "16.9，地面 35 tick / 363px，残影 3"),
                (new[] { ItemType.MasterNinjaGear, ItemType.HermesBoots }, "MasterNinja+Hermes", "16.9，地面 27 tick / 332px，残影 3"),
                (new[] { ItemType.ShieldOfCthulhu }, "Shield", "14.5，15 tick / 167px（有赫尔墨斯时），残影随 eocDash"),
                (new[] { ItemType.ShieldOfCthulhu, ItemType.Tabi }, "Shield,Tabi", "后一格的分趾厚底袜胜出：16.9"),
                (new[] { ItemType.Tabi, ItemType.ShieldOfCthulhu }, "Tabi,Shield", "后一格的克苏鲁护盾胜出：14.5"),
            })
            {
                Wear(wear);
                yield return Settle(hero, home);
                int d0 = TerraMovement.DevDashes, kind = 0, shadows = 0;
                float x0 = col.bounds.center.x, xEnd = x0, top = 0f, ticks = 0f;
                bool seen = false;
                TerraMovement.RequestDash((int)dir);
                for (float t = 0f; t < 1.6f; t += Time.deltaTime)
                {
                    top = Mathf.Max(top, Mathf.Abs(rb.linearVelocity.x) / TerraMovement.PxT);
                    if (TerraMovement.IsDashing)
                    {
                        seen = true;
                        ticks += Time.deltaTime * 60f;
                        kind = Mathf.Max(kind, TerraMovement.DevDashKind);
                        shadows = Mathf.Max(shadows, TerraMovement.ShadowCount);
                        xEnd = col.bounds.center.x;
                    }
                    else if (seen) break;
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 机动 冲刺 {name}：开始 {TerraMovement.DevDashes - d0} 次，种类 {kind}，最快 {top:F2}，冲刺 {ticks:F0} tick 共 {Mathf.Abs(xEnd - x0) / Pxu:F0}px，残影 {shadows}（原版 {expect}）");
                yield return Land(hero);
            }
            // A dash to look at: the smoke and the three afterimages.
            Wear(ItemType.Tabi);
            yield return Settle(hero, home);
            TerraMovement.RequestDash((int)dir);
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) yield return null;
            yield return CloseUp(scene, "m_dash_tabi", hero, 160, 2);
            yield return Land(hero);

            // --- The rest where the scene starts.
            var runHome = home;
            home = start;
            // --- The Black Belt: one hit in ten; with the Mystic Arts Sash 19%.
            PlayerData.instance.isInvincible = false;
            var hitter = new GameObject("Dev Hitter");
            IEnumerator Hit()
            {
                hero.cState.invulnerable = false;
                mod.Stats.Life = mod.Stats.LifeMax;
                hitter.transform.position = hero.transform.position + Vector3.left;
                PlayerHooks.HurtPlayer(hero, 20, 1, hitter);
                yield return null;
            }
            yield return Settle(hero, home);
            foreach (var (wear, label, expect) in new[] { (new[] { ItemType.BlackBelt }, "黑腰带", "约 1/10"), (new[] { ItemType.BlackBelt, ItemType.MysticArtsSash }, "黑腰带+秘术腰带", "约 19%") })
            {
                Wear(wear);
                yield return null;
                int d0 = TerraHurt.DevDodges, b0 = TerraHurt.DevBeltDodges;
                const int tries = 100;
                bool pictured = false;
                for (int k = 0; k < tries; k++)
                {
                    int before = TerraHurt.DevBeltDodges;
                    yield return Hit();
                    if (!pictured && TerraHurt.DevBeltDodges > before)
                    {
                        pictured = true;
                        for (int f = 0; f < 4; f++) yield return null;
                        yield return CloseUp(scene, "m_belt_dodge", hero, 120, 2);
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] 机动 {label}：{tries} 次受击闪避 {TerraHurt.DevDodges - d0} 次（{expect}），其中黑腰带 {TerraHurt.DevBeltDodges - b0}");
                yield return new WaitForSeconds(0.4f);
            }
            Object.Destroy(hitter);
            PlayerData.instance.isInvincible = true;

            // --- The Sail jump: 18 ticks of lift (25 with a balloon), 112 / 202 px.
            foreach (var (wear, name, expect) in new[]
            {
                (new[] { ItemType.TsunamiInABottle }, "Tsunami", "112px"),
                (new[] { ItemType.SharkronBalloon }, "Sharkron", "202px"),
                (new[] { ItemType.CloudinaBottle }, "Cloud", "79px"),
            })
            {
                Wear(wear);
                yield return Settle(hero, home);
                hero.transform.position = home + Vector3.up * 1.5f;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
                rb.linearVelocity = Vector2.zero;
                TerraMovement.DevJump = true;
                int j0 = TerraMovement.DevExtraJumps;
                doDoubleJump.Invoke(hero, null);
                string jump = TerraMovement.DevJumpName;
                float rise = 0f;
                bool pictured = false;
                yield return Rise(col, 1.4f, r => rise = r, t =>
                {
                    if (!pictured && t > 0.1f && name == "Tsunami")
                    {
                        pictured = true;
                        Shot(scene, "m_sail");
                    }
                });
                TerraMovement.DevJump = false;
                Plugin.Log.LogInfo($"[AutoTest] 机动 额外跳 {name}：{jump}（{TerraMovement.DevExtraJumps - j0} 次），升高 {rise:F0}px（原版 {expect}）");
                yield return Land(hero);
            }
            // The order: Sandstorm, Blizzard, Fart, Sail, Cloud.
            Wear(ItemType.HorseshoeBundle, ItemType.TsunamiInABottle, ItemType.FartInABalloon);
            yield return Settle(hero, home);
            hero.transform.position = home + Vector3.up * 1.5f;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            var order = new System.Text.StringBuilder();
            for (int k = 0; k < 6; k++)
            {
                TerraMovement.DevJump = true;
                doDoubleJump.Invoke(hero, null);
                order.Append(TerraMovement.DevJumpName).Append(' ');
                for (float t = 0f; t < 0.5f && TerraMovement.DevJumpName != "None"; t += Time.deltaTime) yield return null;
                TerraMovement.DevJump = false;
                yield return new WaitForFixedUpdate();
            }
            Plugin.Log.LogInfo($"[AutoTest] 机动 跳跃顺序：{order}（原版 Sandstorm Blizzard Fart Sail Cloud None）");
            yield return Land(hero);

            // --- The Portable Stool: UP held standing still; the jump off it.
            float plain = 0f, stool = 0f;
            Wear();
            yield return Settle(hero, home);
            TerraMovement.DevJump = true;
            TerraMovement.DevGroundJump(hero);
            yield return Rise(col, 1.2f, r => plain = r);
            TerraMovement.DevJump = false;
            yield return Land(hero);
            Wear(ItemType.PortableStool);
            yield return Settle(hero, home);
            TerraMovement.DevUp = true;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null;
            bool onStool = TerraMovement.StoolActive;
            yield return CloseUp(scene, "m_stool", hero, 90, 3);
            // From the ground: the jump starts 26 px up, on the stool's top.
            float ground = col.bounds.min.y, peak = ground;
            TerraMovement.DevJump = true;
            TerraMovement.DevGroundJump(hero);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, col.bounds.min.y);
                yield return null;
            }
            stool = (peak - ground) / Pxu;
            TerraMovement.DevJump = false;
            TerraMovement.DevUp = false;
            Plugin.Log.LogInfo($"[AutoTest] 机动 梯凳：站上={onStool}，普通跳 {plain:F0}px，梯凳跳 {stool:F0}px（原版 98 / 147）");
            yield return Land(hero);

            // --- Magiluminescence: the run on the ground ×1.15.
            foreach (var (wear, name, expect) in new[]
            {
                (new ItemType[0], "plain", "3"), (new[] { ItemType.Magiluminescence }, "Magiluminescence", "3.45"),
                (new[] { ItemType.HermesBoots }, "Hermes", "6"), (new[] { ItemType.HermesBoots, ItemType.Magiluminescence }, "Hermes+Magiluminescence", "6.9"),
            })
            {
                Wear(wear);
                yield return Settle(hero, runHome);
                // Hermes' last stretch is slow (0.2 of the acceleration): start them at 5.5 px/tick, short of the run's end.
                bool boots = System.Array.IndexOf(wear, ItemType.HermesBoots) >= 0;
                if (boots) rb.linearVelocity = new Vector2(dir * 5.5f * TerraMovement.PxT, 0f);
                float top = 0f;
                TerraMovement.DevMove = (int)dir;
                for (float t = 0f; t < 1.6f; t += Time.deltaTime)
                {
                    if (hero.cState.onGround) top = Mathf.Max(top, Mathf.Abs(rb.linearVelocity.x) / TerraMovement.PxT);
                    yield return null;
                }
                if (name == "Hermes+Magiluminescence") yield return CloseUp(scene, "m_magilum_run", hero, 120, 2);
                TerraMovement.DevMove = 0;
                Plugin.Log.LogInfo($"[AutoTest] 机动 跑速 {name}：最快 {top:F2}（原版 {expect}）");
            }

            // --- How they look.
            foreach (var (name, wear) in new[]
            {
                ("ninja", new[] { ItemType.MasterNinjaGear }), ("tiger", new[] { ItemType.TigerClimbingGear }), ("shimmer", new[] { ItemType.ShimmerCloak }),
                ("magilum", new[] { ItemType.Magiluminescence }), ("tube", new[] { ItemType.FloatingTube }), ("bundle", new[] { ItemType.HorseshoeBundle, ItemType.TsunamiInABottle }),
                ("honey", new[] { ItemType.BalloonHorseshoeHoney, ItemType.BlackBelt }),
            })
            {
                Wear(wear);
                yield return Settle(hero, home);
                yield return CloseUp(scene, "m_look_" + name, hero, 90, 3);
            }

            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            mod.DevNoFallDamage = false;
        }
    }
}
