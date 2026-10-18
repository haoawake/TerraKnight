using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "accessories"): the 23 newer accessories against Terraria's numbers: jump
    /// heights (Frog Leg, balloons), each extra jump's rise and the Bundle of Balloons' order,
    /// wings' climb, wing time and glide, the type 2 rocket boots, the boots' top speeds, the
    /// spiked boots' wall cling, the damage bonuses (armour penetration, the Power Glove, the
    /// Avenger Emblem, the Magma Stone, the Worm Scarf), what a hit sets off (Star Cloak, Honey
    /// Comb, Panic Necklace, Magic Cuffs), the Philosopher's Stone, and pictures of them worn.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly System.Reflection.MethodInfo doDoubleJump = HarmonyLib.AccessTools.Method(typeof(HeroController), "DoDoubleJump");
        const float Pxu = Effects.TerraParticles.Px;

        IEnumerator AccessoriesTest(string scene, HeroController hero, bool visualsOnly = false)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var saved = (ItemStack[])inv.Accessories.Clone();
            var home = TallSpot(hero);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            home = hero.transform.position;
            Plugin.Log.LogInfo($"[AutoTest] 饰品：起点 {home:F2}，头顶空间 {HeadRoom(col) / Pxu / 16f:F1} 格");

            // --- What they look like worn (in the air, clear of the scenery in front). ---
            Wear(ItemType.AngelWings, ItemType.StarCloak, ItemType.CloudinaBalloon, ItemType.SharkToothNecklace, ItemType.PowerGlove);
            yield return Settle(hero, home);
            yield return Hover(hero, home);
            Shot(scene, "x_worn_a");
            yield return null;
            yield return null;
            Wear(ItemType.DemonWings, ItemType.SpectreBoots, ItemType.ObsidianShield, ItemType.MagicCuffs, ItemType.PanicNecklace);
            yield return Settle(hero, home);
            yield return Hover(hero, home);
            Shot(scene, "x_worn_b");
            yield return null;
            yield return null;
            TerraMovement.DevJump = true;
            TerraMovement.DevGroundJump(hero);
            yield return new WaitForSeconds(0.7f);
            Shot(scene, "x_worn_b_flying");
            TerraMovement.DevJump = false;
            yield return Land(hero);
            Wear(ItemType.BundleofBalloons, ItemType.FrogLeg, ItemType.WormScarf, ItemType.LightningBoots, ItemType.FeralClaws);
            yield return Settle(hero, home);
            yield return Hover(hero, home);
            Shot(scene, "x_worn_c");
            yield return null;
            yield return null;
            Wear(ItemType.ShinyRedBalloon, ItemType.MagicQuiver, ItemType.CrossNecklace, ItemType.CobaltShield, ItemType.ManaFlower);
            yield return Settle(hero, home);
            yield return Hover(hero, home);
            Shot(scene, "x_worn_d");
            yield return null;
            yield return null;
            // The sandstorm's spin, mid-lift.
            Wear(ItemType.SandstorminaBottle);
            yield return Settle(hero, home);
            hero.transform.position = home + Vector3.up * 1.5f;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            TerraMovement.DevJump = true;
            doDoubleJump.Invoke(hero, null);
            yield return new WaitForSeconds(0.2f);
            Shot(scene, "x_sandstorm_spin_1");
            yield return new WaitForSeconds(0.07f);
            Shot(scene, "x_sandstorm_spin_2");
            TerraMovement.DevJump = false;
            yield return Land(hero);

            if (visualsOnly)
            {
                for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = saved[i];
                yield break;
            }
            // --- Ground jumps: 98 px plain, 144 the Frog Leg, 172 a balloon, 232 both (spec-acc2 0.2). ---
            foreach (var (label, items, expect) in new[]
            {
                ("无饰品", new ItemType[0], 98), ("蛙腿", new[] { ItemType.FrogLeg }, 144), ("云朵气球", new[] { ItemType.CloudinaBalloon }, 172),
                ("蛙腿+闪亮红气球", new[] { ItemType.FrogLeg, ItemType.ShinyRedBalloon }, 232),
            })
            {
                Wear(items);
                yield return Settle(hero, home);
                float rise = 0f;
                TerraMovement.DevJump = true;
                TerraMovement.DevGroundJump(hero);
                yield return Rise(col, 1.6f, r => rise = r);
                TerraMovement.DevJump = false;
                Plugin.Log.LogInfo($"[AutoTest] 地面跳 {label}：升高 {rise:F0}px（原版 {expect}）");
            }

            // --- The Frog Leg's auto-jump: holding jump for three seconds. ---
            Wear(ItemType.FrogLeg);
            yield return Settle(hero, home);
            int takeoffs = 0;
            bool wasGround = true;
            TerraMovement.DevJump = true;
            TerraMovement.DevGroundJump(hero);
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                bool g = hero.cState.onGround;
                if (wasGround && !g) takeoffs++;
                wasGround = g;
                yield return null;
            }
            TerraMovement.DevJump = false;
            Plugin.Log.LogInfo($"[AutoTest] 蛙腿自动跳：按住 3 秒起跳 {takeoffs} 次（应多于 1 次）");

            // --- Each extra jump from rest in the air: Cloud 79, Blizzard 130, Fart 167, Sandstorm 236; the cloud with a balloon 141. ---
            foreach (var (label, items, expect) in new[]
            {
                ("云朵瓶", new[] { ItemType.CloudinaBottle }, 79), ("暴雪瓶", new[] { ItemType.BlizzardinaBottle }, 130), ("罐中臭屁", new[] { ItemType.FartinaJar }, 167),
                ("沙暴瓶", new[] { ItemType.SandstorminaBottle }, 236), ("云朵气球", new[] { ItemType.CloudinaBalloon }, 141),
            })
            {
                Wear(items);
                yield return Settle(hero, home);
                hero.transform.position = home + Vector3.up * 1.5f;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
                rb.linearVelocity = Vector2.zero;
                TerraMovement.DevJump = true;
                doDoubleJump.Invoke(hero, null);
                string name = TerraMovement.DevJumpName;
                float rise = 0f;
                bool pictured = false;
                yield return Rise(col, 1.6f, r => rise = r, t =>
                {
                    if (!pictured && t > 0.25f && (items[0] == ItemType.SandstorminaBottle || items[0] == ItemType.BlizzardinaBottle || items[0] == ItemType.FartinaJar))
                    {
                        pictured = true;
                        Shot(scene, "x_jump_" + items[0]);
                    }
                });
                TerraMovement.DevJump = false;
                Plugin.Log.LogInfo($"[AutoTest] 额外跳 {label}（{name}）：从静止升高 {rise:F0}px（原版 {expect}）");
            }

            // --- The Bundle of Balloons: Sandstorm, then Blizzard, then Cloud, then nothing. ---
            Wear(ItemType.BundleofBalloons);
            yield return Settle(hero, home);
            hero.transform.position = home + Vector3.up * 1.5f;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            var order = new System.Text.StringBuilder();
            for (int k = 0; k < 4; k++)
            {
                TerraMovement.DevJump = true;
                doDoubleJump.Invoke(hero, null);
                order.Append(TerraMovement.DevJumpName).Append(' ');
                for (float t = 0f; t < 1.2f && TerraMovement.DevJumpName != "None"; t += Time.deltaTime) yield return null;
                TerraMovement.DevJump = false;
                yield return new WaitForSeconds(0.1f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 气球束四连跳顺序：{order}（原版 沙暴、暴雪、云朵，第四次没有）");
            yield return Land(hero);

            // --- Wings: holding jump from the ground; Fledgling 294 px, Angel 857 (or the ceiling); then the glide at 3.33 px/tick. ---
            foreach (var (label, wing, expect) in new[] { ("雏翼", ItemType.FledglingWings, 294), ("天使之翼", ItemType.AngelWings, 857) })
            {
                Wear(wing);
                yield return Settle(hero, home);
                float rise = 0f, glide = 0f;
                TerraMovement.DevJump = true;
                TerraMovement.DevGroundJump(hero);
                bool pictured = false;
                yield return Rise(col, 3.5f, r => rise = r, t =>
                {
                    if (!pictured && t > 0.6f && TerraMovement.Flying)
                    {
                        pictured = true;
                        Shot(scene, "x_fly_" + wing);
                    }
                });
                float left = TerraMovement.DevWingTime;
                // Still holding jump, falling: the glide.
                for (float t = 0f; t < 2f && !hero.cState.onGround; t += Time.deltaTime)
                {
                    glide = Mathf.Min(glide, rb.linearVelocity.y / TerraMovement.PxT);
                    yield return null;
                }
                TerraMovement.DevJump = false;
                Plugin.Log.LogInfo($"[AutoTest] {label}：升高 {rise:F0}px（原版 {expect}，或顶到天花板），剩余飞行时间 {left:F0}，滑翔最快下落 {-glide:F2} px/tick（原版 3.33）");
                yield return Land(hero);
            }
            // Only one pair of wings at a time.
            Wear(ItemType.AngelWings);
            Plugin.Log.LogInfo($"[AutoTest] 第二对翅膀能否装进别的栏位：{InventoryGui.DevFits(ItemStack.Of(ItemType.DemonWings), 1)}（原版 否），雏翼换掉天使之翼位置：{InventoryGui.DevFits(ItemStack.Of(ItemType.FledglingWings), 0)}（原版 是）");

            // --- Wings with rocket boots: the boots' 7 become 42 more ticks of flight. ---
            Wear(ItemType.FledglingWings, ItemType.SpectreBoots);
            yield return Settle(hero, home);
            TerraMovement.DevJump = true;
            TerraMovement.DevGroundJump(hero);
            yield return new WaitForSeconds(0.15f);
            float wingWithRockets = TerraMovement.DevWingTime;
            TerraMovement.DevJump = false;
            Plugin.Log.LogInfo($"[AutoTest] 雏翼+幽灵靴：起飞后飞行时间 {wingWithRockets:F0}（原版 25+42=67 减去已用）");
            yield return Land(hero);

            // --- Spectre Boots' flight (type 2): rising while jump is held in the air. ---
            Wear(ItemType.SpectreBoots);
            yield return Settle(hero, home);
            hero.transform.position = home + Vector3.up * 0.5f;
            rb.linearVelocity = new Vector2(0f, 5f * TerraMovement.PxT);
            yield return new WaitForSeconds(0.05f);
            float rocketRise = 0f;
            TerraMovement.DevJump = true;
            yield return Rise(col, 1.5f, r => rocketRise = r, t => { if (t > 0.5f && t - Time.deltaTime <= 0.5f) Shot(scene, "x_spectre_rocket"); });
            TerraMovement.DevJump = false;
            Plugin.Log.LogInfo($"[AutoTest] 幽灵靴火箭（类型 {TerraMovement.RocketKind}）：升高 {rocketRise:F0}px");
            yield return Land(hero);

            // --- Top speeds: Hermes 6, Spectre 6, Lightning 6.75 (not scaled by its 8% move speed), Hermes with an Aglet still 6. ---
            foreach (var (label, items, expect) in new[]
            {
                ("赫尔墨斯靴", new[] { ItemType.HermesBoots }, 6f), ("幽灵靴", new[] { ItemType.SpectreBoots }, 6f), ("闪电靴", new[] { ItemType.LightningBoots }, 6.75f),
                ("赫尔墨斯靴+鞋带束头", new[] { ItemType.HermesBoots, ItemType.Aglet }, 6f),
            })
            {
                Wear(items);
                yield return Settle(hero, home);
                float top = 0f;
                int dir = RoomRight(col) > RoomLeft(col) ? 1 : -1;
                TerraMovement.DevMove = dir;
                for (float t = 0f; t < 6f; t += Time.deltaTime)
                {
                    top = Mathf.Max(top, Mathf.Abs(rb.linearVelocity.x) / TerraMovement.PxT);
                    yield return null;
                }
                TerraMovement.DevMove = 0;
                Plugin.Log.LogInfo($"[AutoTest] 最高跑速 {label}：{top:F2} px/tick（原版 {expect}），accRunSpeed={TerraMovement.AccRunSpeed:F2} moveSpeed={TerraMovement.MoveSpeed:F2}");
                hero.transform.position = home;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
            }

            // --- Wall slide and cling: a wall built beside the player; one climbing item slides at 0.5, the claws and spikes stop dead. ---
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.05f));
            int wallX = feet.x + 2;
            var wall = new System.Collections.Generic.List<Vector2Int>();
            for (int y = feet.y; y < feet.y + 14; y++)
                if (TerraMod.CanPlace(new Vector2Int(wallX, y)))
                {
                    TerraWorld.Set(wallX, y, CellKind.Stone);
                    wall.Add(new Vector2Int(wallX, y));
                }
            foreach (var (label, items, expect) in new[] { ("攀爬爪", new[] { ItemType.ClimbingClaws }, 0.5f), ("鞋钉", new[] { ItemType.ShoeSpikes }, 0.5f), ("攀爬爪+鞋钉", new[] { ItemType.ClimbingClaws, ItemType.ShoeSpikes }, 0f) })
            {
                Wear(items);
                yield return Settle(hero, home);
                float half = col.bounds.extents.x;
                hero.transform.position = new Vector3(wallX * CellGrid.CellSize - half - 0.02f + (hero.transform.position.x - col.bounds.center.x), home.y + 4.5f, home.z);
                rb.linearVelocity = new Vector2(0f, -3f);
                hero.FaceRight();
                yield return new WaitForFixedUpdate();
                // The game starts a slide from the real controls; put it in one, holding towards the wall.
                wallTouchingR(hero) = true;
                wallSlidingR(hero) = true;
                hero.cState.wallSliding = true;
                hero.cState.touchingWall = true;
                TerraMovement.DevMove = 1;
                float sum = 0f;
                int n = 0, sliding = 0;
                for (float t = 0f; t < 1.6f; t += Time.deltaTime)
                {
                    if (t > 0.5f && hero.cState.wallSliding)
                    {
                        sum += rb.linearVelocity.y / TerraMovement.PxT;
                        n++;
                    }
                    if (hero.cState.wallSliding) sliding++;
                    if (t > 1f && t - Time.deltaTime <= 1f && items.Length == 2) Shot(scene, "x_wall_cling");
                    yield return null;
                }
                TerraMovement.DevMove = 0;
                Plugin.Log.LogInfo($"[AutoTest] 贴墙 {label}：贴墙帧 {sliding}，下滑速度 {(n > 0 ? -sum / n : float.NaN):F2} px/tick（原版 {expect}），spikedBoots={TerraMovement.SpikedBoots}");
                yield return Land(hero);
            }
            foreach (var c in wall) TerraWorld.Set(c.x, c.y, CellKind.Empty);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);

            // --- Damage bonuses. ---
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            var sword = ItemDb.Get(ItemType.CopperBroadsword);
            Wear();
            yield return null;
            float kb0 = Equipment.Knockback(sword), scale0 = Equipment.ItemScale(sword);
            int blade0 = Equipment.Damage(ItemDb.Get(ItemType.TerraBlade));
            Wear(ItemType.PowerGlove, ItemType.AvengerEmblem, ItemType.SharkToothNecklace, ItemType.WormScarf, ItemType.MagmaStone);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 强力手套：铜阔剑击退 {kb0} -> {Equipment.Knockback(sword)}（原版 ×2），大小 {scale0} -> {Equipment.ItemScale(sword):F2}（×1.1），近战速度 {Equipment.Current.MeleeSpeed}");
            Plugin.Log.LogInfo($"[AutoTest] 复仇者徽章：泰拉刃伤害 {blade0} -> {Equipment.Damage(ItemDb.Get(ItemType.TerraBlade))}（原版 ×1.12）");
            if (target != null)
            {
                var info = EnemyStats.Of(target);
                int def = info.Defense;
                Plugin.Log.LogInfo($"[AutoTest] 鲨牙项链：敌人防御 {def}，20 点的一击 {EnemyStats.Strike(info, 20, false, 0)} -> {EnemyStats.Strike(info, 20, false, Equipment.Current.ArmorPen)}（多 min(5,防御)/2 = {Mathf.Min(5, def) / 2}）");
                // The Magma Stone: a sword hit sets the enemy on Hellfire.
                var trb = target.GetComponent<Rigidbody2D>();
                target.hp = Mathf.Max(target.hp, 300);
                EnemyDebuffs.Clear();
                Vector2 near = (Vector2)col.bounds.center + Vector2.right * 1.2f;
                bool hellfire = false;
                for (int k = 0; k < 4 && !hellfire && target != null && !target.isDead; k++)
                {
                    Pin(target, trb, near);
                    mod.DevUse(ItemType.CopperBroadsword, near);
                    for (float t = 0f; t < 0.4f && target != null; t += Time.deltaTime)
                    {
                        Pin(target, trb, near);
                        hellfire |= EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire);
                        if (t > 0.15f && t - Time.deltaTime <= 0.15f && k == 0) Shot(scene, "x_magma_swing");
                        yield return null;
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] 岩浆石：挥剑命中后地狱火={hellfire}");
            }
            int savedDef = mod.Stats.Defense, life0 = mod.Stats.Life;
            mod.Stats.Defense = 0;
            mod.Stats.Life = mod.Stats.LifeMax;
            int took = mod.Stats.TakeDirect(100);
            mod.Stats.Defense = savedDef;
            mod.Stats.Life = life0;
            Plugin.Log.LogInfo($"[AutoTest] 蠕虫围巾：100 点伤害（防御 0）实际受到 {took}（原版 83）");

            // --- What a hit sets off. ---
            Wear(ItemType.StarCloak, ItemType.HoneyComb, ItemType.PanicNecklace, ItemType.MagicCuffs);
            yield return Settle(hero, home);
            PlayerData.instance.isInvincible = false;
            hero.cState.invulnerable = false;
            mod.Stats.Life = mod.Stats.LifeMax;
            mod.Stats.Mana = 0;
            mod.PanicTicks = 0f;
            mod.Stats.HoneyTicks = 0f;
            int stars0 = TerraProjectiles.DevMade(ProjKind.StarCloakStar), bees0 = TerraProjectiles.DevMade(ProjKind.Bee);
            var hitter = new GameObject("Dev Hitter");
            hitter.transform.position = hero.transform.position + Vector3.left;
            PlayerHooks.HurtPlayer(hero, 40, 1, hitter);
            yield return null;
            int mana = mod.Stats.Mana;
            Plugin.Log.LogInfo($"[AutoTest] 受伤触发：星星 {TerraProjectiles.DevMade(ProjKind.StarCloakStar) - stars0}（原版 3），蜜蜂 {TerraProjectiles.DevMade(ProjKind.Bee) - bees0}（1-3），" +
                               $"恐慌 {mod.PanicTicks:F0} tick（480），蜂蜜 {mod.Stats.HoneyTicks:F0} tick（300），魔力 0 -> {mana}（原版加上原始伤害 40），移动速度 {TerraMovement.MoveSpeed:F2}");
            yield return new WaitForSeconds(0.45f);
            Shot(scene, "x_star_cloak");
            yield return new WaitForSeconds(1.5f);
            Object.Destroy(hitter);
            PlayerData.instance.isInvincible = true;

            // --- Philosopher's Stone: 45 s of potion sickness; taking it off makes the rest 60's worth. ---
            Wear(ItemType.PhilosophersStone);
            yield return null;
            mod.Stats.PotionSickness = 0f;
            mod.Stats.Life = Mathf.Max(1, mod.Stats.LifeMax - 60);
            mod.DevUse(ItemType.HealingPotion, (Vector2)col.bounds.center);
            float sick = mod.Stats.PotionSickness;
            Wear();
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 点金石：喝药后药水病 {sick:F1} 秒（原版 45），摘下后 {mod.Stats.PotionSickness:F1}（按 ÷0.75 换算）");
            mod.Stats.PotionSickness = 0f;

            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = saved[i];
            mod.DevNoFallDamage = false;
        }

        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, bool> wallTouchingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("touchingWallR");
        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, bool> wallSlidingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("wallSlidingR");

        void Wear(params ItemType[] items)
        {
            var inv = mod.Inventory;
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = i < items.Length ? ItemStack.Of(items[i]) : default;
        }

        /// <summary>Back on the ground at the start, still, long enough for the accessories' state to settle.</summary>
        IEnumerator Settle(HeroController hero, Vector3 home)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            TerraMovement.DevJump = false;
            TerraMovement.DevMove = 0;
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.35f);
            for (float t = 0f; t < 2f && !hero.cState.onGround; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.1f);
        }

        /// <summary>Held still 3.5 units up for a moment (the picture's frame), standing pose.</summary>
        IEnumerator Hover(HeroController hero, Vector3 home)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            for (int i = 0; i < 30; i++)
            {
                hero.transform.position = home + Vector3.up * 3.5f;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
            }
            yield return null;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            Plugin.Log.LogInfo($"[AutoTest] 悬停：目标 y={home.y + 3.5f:F2} 实际 y={hero.transform.position.y:F2} 脚底 {b.min.y:F2} 着地={hero.cState.onGround} 状态 {hero.hero_state} 跳跃加成={TerraMovement.JumpBoost} 贴图 {mod.DevSprite}");
        }

        IEnumerator Land(HeroController hero)
        {
            TerraMovement.DevJump = false;
            for (float t = 0f; t < 4f && !hero.cState.onGround; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>How far the feet rise above where they started over some seconds (Terraria px).</summary>
        static IEnumerator Rise(BoxCollider2D col, float seconds, System.Action<float> done, System.Action<float> each = null)
        {
            float start = col.bounds.min.y, top = start;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                top = Mathf.Max(top, col.bounds.min.y);
                each?.Invoke(t);
                yield return null;
            }
            done((top - start) / Pxu);
        }

        static float HeadRoom(BoxCollider2D col)
        {
            var hit = Physics2D.Raycast((Vector2)col.bounds.center + Vector2.up * col.bounds.extents.y, Vector2.up, 60f, 1 << ChunkColliders.TerrainLayer);
            return hit.collider != null ? hit.distance : 60f;
        }

        static float RoomRight(BoxCollider2D col) => Room(col, Vector2.right);
        static float RoomLeft(BoxCollider2D col) => Room(col, Vector2.left);

        static float Room(BoxCollider2D col, Vector2 dir)
        {
            var hit = Physics2D.Raycast(col.bounds.center, dir, 80f, 1 << ChunkColliders.TerrainLayer);
            return hit.collider != null ? hit.distance : 80f;
        }

        /// <summary>A spot on the ground near the start with the most room overhead (the wing and sandstorm heights need it).</summary>
        static Vector3 TallSpot(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 start = hero.transform.position;
            float offsetToFeet = col.bounds.min.y - start.y;
            Vector3 best = Clear(hero, start);
            float bestRoom = -1f;
            int terrain = 1 << ChunkColliders.TerrainLayer;
            for (float dx = -30f; dx <= 30f; dx += 1.5f)
            {
                var from = start + new Vector2(dx, 1.5f);
                if (Physics2D.OverlapPoint(from, terrain) != null) continue;
                var down = Physics2D.Raycast(from, Vector2.down, 10f, terrain);
                if (down.collider == null) continue;
                var up = Physics2D.Raycast(down.point + Vector2.up * 0.2f, Vector2.up, 60f, terrain);
                float room = up.collider != null ? up.distance : 60f;
                if (room <= bestRoom) continue;
                bestRoom = room;
                best = new Vector3(from.x, down.point.y - offsetToFeet + 0.05f, Constants.HERO_Z);
            }
            return best;
        }
    }
}
