using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// The potions' and flasks' buffs (Player.UpdateBuffs, Pl:9962-10450) and the permanent
    /// boosters (UpdatePermanentBoosters, Pl:12945-12960): what they add to the player (gathered
    /// into Equipment each frame), and what they do on their own each tick — the Inferno potion's
    /// ring of fire, the Shine and Night Owl potions' light, the Spelunker's sparkle on treasure,
    /// the Hunter's glow on enemies, the Dangersense's on hazards, the Featherfall potion's slow
    /// fall. Battle, Calming and Invisibility change how fast the dead enemies come back
    /// (NPC.SpawnNPC's spawn rate ×0.5 / ×1.3 / ×1.2); the Flipper potion swims in acid as the
    /// flippers do. The potions that only act on water, fish or Terraria's own biomes (Gills,
    /// Water Walking, Fishing, Sonar, Crate, Biome Sight, Warmth, Heartreach) give their buff and
    /// nothing here acts on it.
    /// </summary>
    [HarmonyPatch]
    internal static class PotionBuffs
    {
        public const int ObsidianSkin = 1, Regeneration = 2, Swiftness = 3, Gills = 4, Ironskin = 5, ManaRegeneration = 6, MagicPower = 7, Featherfall = 8,
            Spelunker = 9, Invisibility = 10, Shine = 11, NightOwl = 12, Battle = 13, Thorns = 14, WaterWalking = 15, Archery = 16, Hunter = 17, Gravitation = 18,
            Mining = 104, Heartreach = 105, Calm = 106, Builder = 107, Titan = 108, Flipper = 109, Summoning = 110, Dangersense = 111, AmmoReservation = 112,
            Lifeforce = 113, Endurance = 114, Rage = 115, Inferno = 116, Wrath = 117, Fishing = 121, Sonar = 122, Crate = 123, Warmth = 124, Lucky = 257, BiomeSight = 343;

        /// <summary>The flasks' buffs (WeaponImbue*: Venom 71, Cursed Flames 73, Fire 74, Gold 75, Ichor 76, Nanites 77, Confetti 78, Poison 79) and their meleeEnchant.</summary>
        static readonly Dictionary<int, int> imbues = new Dictionary<int, int> { [71] = 1, [73] = 2, [74] = 3, [75] = 4, [76] = 5, [77] = 6, [78] = 7, [79] = 8 };

        static readonly bool defined = Define();

        static bool Define()
        {
            void D(int id, string name, string desc) => PlayerBuffs.Define(id, name, desc);
            D(ObsidianSkin, "黑曜石皮", "对熔岩免疫"); D(Regeneration, "再生", "提供生命再生"); D(Swiftness, "敏捷", "移动速度提高25%");
            D(Gills, "鱼鳃", "允许你在液体中呼吸"); D(Ironskin, "铁皮", "防御力提高8点"); D(ManaRegeneration, "魔力再生", "提高魔力再生速度");
            D(MagicPower, "魔能", "魔法伤害提高20%"); D(Featherfall, "羽落", "按向上键或向下键来控制下降速度"); D(Spelunker, "洞穴探险", "显示宝藏和矿石的位置");
            D(Invisibility, "隐身", "隐身"); D(Shine, "光芒", "发光"); D(NightOwl, "夜猫子", "提升夜视能力"); D(Battle, "战斗", "提高敌人生成速度");
            D(Thorns, "荆棘", "攻击者也会受到伤害"); D(WaterWalking, "水上漂", "按向下键进入水中"); D(Archery, "箭术", "弓伤害提高10%，箭速度提高20%");
            D(Hunter, "猎人", "显示敌人位置"); D(Gravitation, "重力", "按向上键可逆转重力"); D(Mining, "挖矿", "挖矿速度提高25%"); D(Heartreach, "拾心", "扩大心的拾取范围");
            D(Calm, "冷静", "敌人生成速度降低"); D(Builder, "建筑工", "提高放置速度和范围"); D(Titan, "泰坦", "提高击退力"); D(Flipper, "脚蹼", "在水中移动如常");
            D(Summoning, "召唤", "仆从数量上限增加1"); D(Dangersense, "危险感", "你可以看到附近的危险"); D(AmmoReservation, "弹药储备", "20%几率省下弹药");
            D(Lifeforce, "生命力", "最大生命值提高20%"); D(Endurance, "耐力", "伤害降低10%"); D(Rage, "暴怒", "暴击率提高10%"); D(Inferno, "狱火", "附近的敌人被点燃");
            D(Wrath, "怒气", "伤害提高10%"); D(Fishing, "钓鱼", "提高渔力"); D(Sonar, "声呐", "你能看到是什么在咬你的鱼钩"); D(Crate, "宝匣", "钓上宝匣的几率更大");
            D(Warmth, "温暖", "减轻寒冷源造成的影响"); D(Lucky, "幸运", "你觉得自己很幸运"); D(BiomeSight, "生物群系视觉", "显示感染物块的位置");
            D(71, "武器灌注：酸性毒液", "近战攻击会对你的目标施放酸性毒液"); D(73, "武器灌注：诅咒焰", "近战攻击会对敌人施放诅咒焰");
            D(74, "武器灌注：火焰", "近战攻击会使敌人着火"); D(75, "武器灌注：金", "近战攻击会使敌人掉落更多金子"); D(76, "武器灌注：灵液", "近战攻击会降低敌人防御力");
            D(77, "武器灌注：纳米机器人", "近战攻击会使敌人困惑，额外造成5%的伤害"); D(78, "武器灌注：彩纸", "近战攻击会喷撒彩纸"); D(79, "武器灌注：毒药", "近战攻击会使敌人中毒");
            return true;
        }

        /// <summary>A drink's buff: Honey on its own timer; a flask in place of any other flask (Player.AddBuff for the imbues).</summary>
        public static void Give(int buff, int ticks)
        {
            if (!defined) return;
            var mod = TerraMod.Instance;
            if (buff == PlayerBuffs.Honey)
            {
                if (mod != null) mod.Stats.HoneyTicks = Mathf.Max(mod.Stats.HoneyTicks, ticks);
                return;
            }
            if (imbues.ContainsKey(buff))
                foreach (var other in imbues.Keys)
                    if (other != buff) PlayerBuffs.Remove(other);
            PlayerBuffs.Add(buff, ticks);
            DevGiven++;
        }

        public static int DevGiven, DevInfernoHits, DevTeleports;

        static bool Has(int buff) => PlayerBuffs.Has(buff);

        /// <summary>What the buffs and the boosters add this frame.</summary>
        public static Bonuses Bonuses()
        {
            var b = new Bonuses();
            if (!defined) return b;
            if (Has(ObsidianSkin))
            {
                b.LavaImmune = true;
                b.FireWalk = true;
            }
            if (Has(Regeneration)) b.LifeRegen += 4;
            if (Has(Swiftness)) b.MoveSpeed += 0.25f;
            if (Has(Ironskin)) b.Defense += 8;
            if (Has(ManaRegeneration)) b.ManaRegenBuff = true;
            if (Has(MagicPower)) b.MagicDamage += 0.2f;
            if (Has(Featherfall)) b.SlowFall = true;
            if (Has(Spelunker)) b.FindTreasure = true;
            if (Has(Invisibility)) b.Invisible = true;
            if (Has(Shine)) b.Shine = true;
            if (Has(NightOwl)) b.NightVision = true;
            if (Has(Battle)) b.Battle = true;
            if (Has(Thorns)) b.Thorns = 1f;
            if (Has(Archery))
            {
                b.Archery = true;
                b.ArrowMult = 0.1f;
            }
            if (Has(Hunter)) b.DetectCreature = true;
            if (Has(Gravitation)) b.GravityControl = true;
            if (Has(Mining)) b.PickSpeed -= 0.25f;
            if (Has(Heartreach)) b.LifeMagnet = true;
            if (Has(Calm)) b.Calm = true;
            if (Has(Builder))
            {
                b.TileSpeed += 0.25f;
                b.BlockRange += 1;
            }
            if (Has(Titan)) b.KbBuff = true;
            // The Flipper potion swims as the flippers do (accFlipper): here, in acid.
            if (Has(Flipper)) b.Swim = true;
            if (Has(Summoning)) b.MaxMinions += 1;
            if (Has(Dangersense)) b.DangerSense = true;
            if (Has(AmmoReservation)) b.AmmoPotion = true;
            if (Has(Lifeforce)) b.LifeForce = true;
            if (Has(Endurance)) b.Endurance += 0.1f;
            if (Has(Rage))
            {
                b.MeleeCrit += 0.1f;
                b.RangedCrit += 0.1f;
                b.MagicCrit += 0.1f;
            }
            if (Has(Inferno)) b.Inferno = true;
            if (Has(Wrath))
            {
                b.MeleeDamage += 0.1f;
                b.RangedDamage += 0.1f;
                b.MagicDamage += 0.1f;
                b.MinionDamage += 0.1f;
            }
            if (Has(Warmth)) b.ResistCold = true;
            // Lucky (Pl:10431-10450): luckPotion 3 / 2 / 1 by the time left, a tenth of luck each.
            int lucky = PlayerBuffs.TimeLeft(Lucky);
            if (lucky > 0) b.Luck += (lucky > 36000 ? 3 : lucky > 18000 ? 2 : 1) * 0.1f;
            foreach (var kv in imbues)
                if (Has(kv.Key)) b.MeleeEnchant = kv.Value;
            var boosts = TerraMod.Instance?.Stats.Boosts;
            if (boosts != null)
            {
                if (boosts.Contains(PermanentBoost.AegisFruit)) b.Defense += 4;
                if (boosts.Contains(PermanentBoost.Ambrosia))
                {
                    b.PickSpeed -= 0.05f;
                    b.TileSpeed += 0.05f;
                }
                // Player.GetLadyBugLuck ... usedGalaxyPearl (Pl:29368-29375): 0.03 luck.
                if (boosts.Contains(PermanentBoost.GalaxyPearl)) b.Luck += 0.03f;
            }
            return b;
        }

        /// <summary>Spawn rate factor for the dead enemies' return (Battle ×0.5, Calming ×1.3, Invisibility ×1.2).</summary>
        public static float RespawnFactor
        {
            get
            {
                var c = Equipment.Current;
                float f = 1f;
                if (c.Battle) f *= 0.5f;
                if (c.Calm) f *= 1.3f;
                if (c.Invisible) f *= 1.2f;
                return f;
            }
        }

        // ------------------------------------------------------------------ the boosters

        /// <summary>
        /// A permanent booster used (Pl:45577-45807): the Life Fruit 5 more maximum life (and 5
        /// life) while fewer than twenty are eaten and the crystals are all used; the rest once
        /// each. False (kept) when it can't be used.
        /// </summary>
        public static bool UseBooster(ItemDef def, HeroController hero)
        {
            var mod = TerraMod.Instance;
            if (mod == null || hero == null) return false;
            var st = mod.Stats;
            if (def.Boost == PermanentBoost.LifeFruit)
            {
                if (st.LifeCrystals < TerraStats.MaxLifeCrystals || st.LifeFruits >= TerraStats.MaxLifeFruits) return false;
                st.LifeFruits++;
                st.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
                st.Heal(5);
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, "5", UI.CombatText.Heal);
            }
            else if (!st.Boosts.Add(def.Boost)) return false;
            else if (def.Boost == PermanentBoost.DemonHeart) mod.Inventory.RefreshEffective();
            mod.PlaySound(def.UseSound ?? "Item_4");
            DevBoosters++;
            return true;
        }

        public static int DevBoosters;

        // ------------------------------------------------------------------ the Teleportation Potion

        /// <summary>
        /// Player.TeleportationPotion: somewhere at random. Here: a spot in this scene with ground
        /// under it and room for the player (up to 200 tries), the teleport's sparkle at both ends.
        /// </summary>
        public static bool TeleportRandom(HeroController hero)
        {
            var gm = GameManager.UnsafeInstance;
            if (hero == null || gm == null || gm.sceneWidth <= 0f) return false;
            var col = hero.GetComponent<BoxCollider2D>();
            var size = col.bounds.size;
            const int mask = 1 << ChunkColliders.TerrainLayer;
            for (int tries = 0; tries < 200; tries++)
            {
                var p = new Vector2(Random.Range(2f, gm.sceneWidth - 2f), Random.Range(2f, gm.sceneHeight - 2f));
                var down = Physics2D.Raycast(p, Vector2.down, 30f, mask);
                if (down.collider == null) continue;
                var stand = down.point + new Vector2(0f, size.y * 0.5f + 0.05f);
                if (Physics2D.OverlapBox(stand, size * 0.95f, 0f, mask) != null) continue;
                if (Vector2.Distance(stand, col.bounds.center) < 4f) continue;
                Sparkle(col.bounds.center);
                hero.transform.position += (Vector3)(stand - (Vector2)col.bounds.center);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Sparkle(stand);
                TerraMod.Instance?.PlaySound("Item_6");
                TerraMod.Instance?.ResetFall();
                DevTeleports++;
                return true;
            }
            return false;
        }

        public static void Sparkle(Vector2 at)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var px = TerraParticles.ToPx(at);
            for (int i = 0; i < 50; i++)
            {
                int d = p.NewDust(px - new Vector2(10f, 21f), 20, 42, 15, 0f, 0f, 150, 1.2f);
                if (d < 0) continue;
                p[d].Velocity *= 0.5f;
            }
        }

        // ------------------------------------------------------------------ each frame

        static float infernoTicks, sparkleTicks, scanTime;
        static readonly List<Collider2D> hazards = new List<Collider2D>();
        static readonly List<Transform> treasures = new List<Transform>();

        /// <summary>The buffs that act on their own: the Inferno ring, the lights, the senses.</summary>
        public static void Tick(HeroController hero)
        {
            if (hero == null || !defined) return;
            var c = Equipment.Current;
            var fx = FxBatch.Instance;
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            var px = TerraParticles.ToPx(centre);
            // Shine: a light (0.8, 0.95, 1) on the player. Night Owl lights nothing itself: it lets the dark rooms be seen as the lantern does (PlayerHooks).
            if (fx != null && c.Shine) fx.Light(px, new Color(0.8f, 0.95f, 1f), 4f);
            if (c.Inferno) InfernoRing(hero, centre, px, fx);
            else infernoTicks = 0f;
            if (c.FindTreasure || c.DangerSense) ScanScene();
            sparkleTicks += Time.deltaTime * 60f;
            bool sparkle = sparkleTicks >= 6f;
            if (sparkle) sparkleTicks = 0f;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            // Spelunker: the geo rocks, the items lying about and the chests glint gold.
            if (c.FindTreasure && fx != null)
            {
                foreach (var t in treasures)
                {
                    if (t == null || !t.gameObject.activeInHierarchy || !OnScreen(cam, t.position)) continue;
                    var tp = TerraParticles.ToPx(t.position);
                    fx.Light(tp, new Color(0.9f, 0.75f, 0.2f), 2.5f);
                    if (sparkle && Random.Range(0, 3) == 0) Dust(tp, 204);
                }
                foreach (var w in WorldItems.Positions())
                    if (OnScreen(cam, w)) fx.Light(TerraParticles.ToPx(w), new Color(0.9f, 0.75f, 0.2f), 1.5f);
                foreach (var ch in Chests.Positions())
                    if (OnScreen(cam, ch)) fx.Light(TerraParticles.ToPx(ch), new Color(0.9f, 0.75f, 0.2f), 2.5f);
            }
            // Hunter: every enemy on screen shows red.
            if (c.DetectCreature && fx != null)
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.isDead || !hm.gameObject.activeInHierarchy || !OnScreen(cam, hm.transform.position)) continue;
                    var col = hm.GetComponent<Collider2D>();
                    var at = col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
                    fx.Light(TerraParticles.ToPx(at), new Color(1f, 0.15f, 0.1f), 2.5f);
                }
            // Dangersense: the spikes, thorns and other harm in the scene glow amber.
            if (c.DangerSense && fx != null)
                foreach (var h in hazards)
                {
                    if (h == null || !h.gameObject.activeInHierarchy || !OnScreen(cam, h.bounds.center)) continue;
                    fx.Light(TerraParticles.ToPx(h.bounds.center), new Color(1f, 0.6f, 0.1f), Mathf.Clamp(h.bounds.size.magnitude, 1.5f, 3f));
                    if (sparkle && Random.Range(0, 4) == 0) Dust(TerraParticles.ToPx(h.bounds.center), 60);
                }
        }

        static bool OnScreen(Camera cam, Vector2 at)
        {
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(new Vector3(at.x, at.y, 0f));
            return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }

        static void Dust(Vector2 px, int type)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            int d = p.NewDust(px - new Vector2(8f, 8f), 16, 16, type, 0f, 0f, 150, 0.8f);
            if (d >= 0)
            {
                p[d].NoGravity = true;
                p[d].Velocity *= 0.3f;
            }
        }

        /// <summary>Every half second: the geo rocks and the hazards (DamageHero) of the scene.</summary>
        static void ScanScene()
        {
            if (Time.time < scanTime) return;
            scanTime = Time.time + 0.5f;
            treasures.Clear();
            foreach (var g in Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None)) treasures.Add(g.transform);
            hazards.Clear();
            foreach (var d in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
            {
                if (d.GetComponentInParent<HealthManager>() != null) continue;
                var col = d.GetComponent<Collider2D>();
                if (col != null) hazards.Add(col);
            }
        }

        /// <summary>
        /// The Inferno potion (Pl:10299-10325): enemies within 200 px catch hellfire (2 s) and,
        /// once a second, take 20; the player glows (0.65, 0.4, 0.1) inside three turning rings of
        /// fire (Main.DrawInfernoRings).
        /// </summary>
        static void InfernoRing(HeroController hero, Vector2 centre, Vector2 px, FxBatch fx)
        {
            fx?.Light(px, new Color(0.65f, 0.4f, 0.1f), 3f);
            infernoTicks += Time.deltaTime * 60f;
            bool strike = infernoTicks >= 60f;
            if (strike) infernoTicks -= 60f;
            var melee = TerraMod.Instance?.Melee;
            const float range = 200f * TerraParticles.Px;
            var hit = new HashSet<HealthManager>();
            foreach (var col in Physics2D.OverlapCircleAll(centre, range, MeleeHits.HitMask))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hit.Add(hm)) continue;
                if (!EnemyDebuffs.Has(hm, EnemyDebuffs.Hellfire)) EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 120);
                if (strike && melee != null)
                {
                    melee.HitWith(col, 20, 0f, 0f, col.bounds.center.x >= centre.x ? 1f : -1f, centre);
                    DevInfernoHits++;
                }
            }
            if (fx != null) DrawRings(fx, px);
        }

        static float flameRingScale = 0.8f, flameRingRot;

        /// <summary>
        /// Main.DrawInfernoRings (M:65296-65344): FlameRing's three 400 px rings, each a sixth of a
        /// turn on from the last, 0.1 apart in scale, growing 0.004 and turning 0.05 a frame (back
        /// to 0.8 at full size), brightest at 0.9, half added.
        /// </summary>
        static void DrawRings(FxBatch fx, Vector2 px)
        {
            float step = Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            flameRingScale += 0.004f * step;
            if (flameRingScale >= 1f) flameRingScale = 0.8f;
            flameRingRot += 0.05f * step;
            if (flameRingRot > Mathf.PI * 2f) flameRingRot -= Mathf.PI * 2f;
            for (int j = 0; j < 3; j++)
            {
                float s = flameRingScale + 0.1f * j;
                if (s > 1f) s -= 0.2f;
                float a = Mathf.Lerp(0.8f, 0f, Mathf.Abs(s - 0.9f) * 10f);
                if (a <= 0f) continue;
                fx.Draw(FxBatch.Frame("FlameRing", 0, 400 * j, 400, 400, 200f, 200f), px, new Color(a, a, a, a / 2f), flameRingRot + Mathf.PI / 3f * j, s);
            }
        }

        /// <summary>
        /// The Featherfall potion (Pl:27222-27231): falling, gravity a third (a tenth holding up)
        /// unless down is held; no fall damage meanwhile.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void SlowFall(HeroController __instance, Rigidbody2D ___rb2d)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !Equipment.Current.SlowFall || __instance.cState.onGround || __instance.cState.dead || GrapplingHook.IsLatched) return;
            if (TerraMovement.DownHeld || TerraMovement.Flying) return;
            var v = ___rb2d.linearVelocity;
            if (v.y >= 0f) return;
            float g = -Physics2D.gravity.y * ___rb2d.gravityScale * Time.fixedDeltaTime;
            bool up = GameHooks.Input != null && GameHooks.Input.inputActions.up.IsPressed;
            // The step's gravity is paid in advance: give back all but a third (a tenth).
            ___rb2d.linearVelocity = new Vector2(v.x, v.y + g * (up ? 0.9f : 2f / 3f));
            mod.ResetFall();
        }

        /// <summary>The flask's touch on a melee or whip hit (StatusToNPC, Pl:6496-6531; the Confetti flask's burst, Pl:44829).</summary>
        public static void Imbue(HealthManager hm, Vector2 centre)
        {
            int e = Equipment.Current.MeleeEnchant;
            if (e == 0 || hm == null) return;
            switch (e)
            {
                case 1:
                    EnemyDebuffs.Add(hm, EnemyDebuffs.Venom, 60 * Random.Range(5, 10));
                    EnemyDebuffs.Add(hm, EnemyDebuffs.PotentAcid, 60);
                    break;
                case 2: EnemyDebuffs.Add(hm, EnemyDebuffs.CursedInferno, 60 * Random.Range(3, 7)); break;
                case 3: EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 60 * Random.Range(3, 7)); break;
                case 4: EnemyDebuffs.Add(hm, EnemyDebuffs.Midas, 120); break;
                case 5: EnemyDebuffs.Add(hm, EnemyDebuffs.Ichor, 60 * Random.Range(10, 20)); break;
                case 6: EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, 60 * Random.Range(1, 4)); break;
                case 7:
                    var p = TerraParticles.Instance;
                    if (p != null)
                    {
                        var px = TerraParticles.ToPx(centre);
                        for (int i = 0; i < 20; i++)
                        {
                            int d = p.NewDust(px - new Vector2(10f, 10f), 20, 20, 139 + Random.Range(0, 4), 0f, -2f, 0, 1.2f);
                            if (d >= 0) p[d].Velocity *= 2f;
                        }
                    }
                    break;
                case 8: EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 60 * Random.Range(5, 10)); break;
            }
            DevImbues++;
        }

        public static int DevImbues;
    }
}
