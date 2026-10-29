using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade magic weapons' own mechanisms (法师武器创意重做设计.md; the user's 10-28 "你这些武器做的都很没新意，左键几乎都是
    /// 套皮加额外弹幕，右键几乎全是多倍弹幕…我希望你自由发挥搞得更有创意"): each weapon's left changes how it fights (things left in
    /// the world, marks stacked and set off, shots that grow, bounce, split or come back) and its right is a move of its own that
    /// works with what the left leaves (MageMech). Every effect "on the enemies" goes to each of them. Left: MageLefts.Custom
    /// (Lefts); right: BladeSkills arts (Arts). This file: stages 0-1.
    /// </summary>
    internal static partial class MageKits
    {
        static float B => MageMech.Block;
        static int TerrainMask => 1 << World.ChunkColliders.TerrainLayer;

        // ------------------------------------------------------------------ helpers

        /// <summary>The game's shot of the use, carrying r (its hooks), as the weapon makes it.</summary>
        static void Cast(ShootPatterns.Shot s, RangedShot r) => RangedFire.With(r, s);

        /// <summary>
        /// A shot that flies out `blocks` from where it was cast along dir at `speed` px an update (a scythe's slow start and
        /// speeding up, a fireball's fall steered past), turns at the first thing it cuts when turnOnHit, and comes back to
        /// the player's hand, gone there. It turns by the way it has really flown, not by its age: an age outran a slow
        /// start (the user's 10-28 "恶魔之镰初始速度太慢了但依然被判定走了很多格…很多飞回手里的法术都是这样").
        /// </summary>
        static void OutAndBack(RangedShot r, Vector2 from, Vector2 dir, float blocks, float speed, bool turnOnHit)
        {
            bool back = false;
            int ticks = 0, backFor = 0;
            var far = from + (dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right) * (blocks + 6f) * B;
            // Long enough to fly out three times over: a shot held up on the way still comes back.
            int most = Mathf.CeilToInt(blocks * 16f / speed) * 3 + 30;
            if (turnOnHit)
            {
                var hit = r.OnStrike;
                r.OnStrike = (hm, at) => { hit?.Invoke(hm, at); back = true; };
            }
            r.Steer = (pos, vel) =>
            {
                if (!back && (Vector2.Distance(pos, from) >= blocks * B || ++ticks > most)) back = true;
                if (!back) return far;
                var hero = HeroController.SilentInstance;
                return hero != null ? HeroCentre(hero) : (Vector2?)null;
            };
            r.SteerTurn = 14f;
            r.SteerSpeed = speed;
            r.SteerMax = speed * 1.3f;
            var fly = r.OnFly;
            r.OnFly = (pos, vel) =>
            {
                if (fly != null && !fly(pos, vel)) return false;
                if (back) backFor++;
                var hero = HeroController.SilentInstance;
                return backFor < 8 || hero == null || Vector2.Distance(pos, HeroCentre(hero)) > 1.2f * B;
            };
        }

        /// <summary>One of a kind from a point (world) at a velocity (px an update, y up), the weapon's damage × k, carrying r; no knockback (an extra, as MageMech.Strike).</summary>
        internal static void Bolt(ItemDef def, ProjKind kind, Vector2 from, Vector2 vel, float k, RangedShot r = null)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null || def == null || kind == ProjKind.None) return;
            r = r ?? new RangedShot();
            r.Item = def;
            r.Local = true;
            TerraProjectiles.SpawnClass = def.Class;
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try { tp.Shoot(kind, from, vel, MageMech.DamageOf(def, k), 0f, Equipment.Crit(def)); }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
            }
            BladeSkills.DevMageArtShots++;
        }

        /// <summary>A shot at an enemy (or a point) from `from`: its velocity at `speed` px an update.</summary>
        static Vector2 At(Vector2 from, Vector2 to, float speed) => ((to - from).sqrMagnitude > 1e-6f ? (to - from).normalized : Vector2.right) * speed;

        /// <summary>The ground under a point (world), within `blocks`; the point that far down when there is none.</summary>
        static Vector2 Ground(Vector2 at, float blocks)
        {
            var hit = Physics2D.Raycast(at, Vector2.down, blocks * B, TerrainMask);
            return hit.collider != null ? hit.point : at + Vector2.down * blocks * B;
        }

        static List<HealthManager> Near(Vector2 at, float blocks, bool sight = true) => MageMech.EnemiesNear(at, blocks * B, sight);

        static HealthManager Nearest(Vector2 at, float blocks, HealthManager not = null)
        {
            foreach (var hm in Near(at, blocks)) if (hm != not) return hm;
            return null;
        }

        static Vector2 Centre(HealthManager hm) => TerraProjectiles.EnemyCentre(hm);

        static Vector2 HeroCentre(HeroController hero) => hero.GetComponent<BoxCollider2D>().bounds.center;

        static void Arc(Vector2 from, Vector2 to, ItemDef def) => SwordFx.Instance?.RangedArc(TerraParticles.ToPx(from), TerraParticles.ToPx(to), def);

        static void Sound(string name, float volume = 0.6f, float pitch = 1f) => TerraMod.Instance?.PlaySound(name, volume, pitch);

        static void Sprite(FxBatch fx, string texture, Vector2 px, Color c, float rotation, float scale) => fx.Draw(FxBatch.Whole(texture), px, c, rotation, scale);

        /// <summary>A burst of dust at a world point, flying out at up to `speed` px a tick.</summary>
        static void Burst(Vector2 at, int dust, int n, float speed, float scale = 1.2f)
        {
            for (int i = 0; i < n; i++) MageMech.Dust(at, dust, Random.insideUnitCircle * speed, scale * Random.Range(0.8f, 1.2f));
        }

        static KindArtBuilder Art(ItemType t) => new KindArtBuilder(t);

        /// <summary>An art registered for a weapon (over the one before), paid in its mana.</summary>
        internal sealed class KindArtBuilder
        {
            readonly ItemType type;
            public KindArtBuilder(ItemType t) { type = t; }

            public void Press(string name, float cooldown, float mana, string zh, string en, System.Func<HeroController, ItemDef, bool> go) =>
                BladeSkills.RegisterArt(type, new BladeSkills.KindArt
                {
                    Name = name, Cooldown = cooldown, Tip = "右键：" + zh + $"（消耗 {mana:0.#} 倍魔力）", TipEn = "Right click: " + en + $" (costs {mana:0.#}× the mana)",
                    Press = (h, d) => BladeSkills.MagePay(d, mana) && go(h, d),
                });

            public void Charge(string name, float full, float mana, string zh, string en, System.Action<HeroController, ItemDef, float> release) =>
                BladeSkills.RegisterArt(type, new BladeSkills.KindArt
                {
                    Name = name, Charged = true, Full = full, Aimed = true, ChargeSound = "tk_gun_charge",
                    Tip = "右键：" + zh + $"（消耗 {mana:0.#} 倍魔力）", TipEn = "Right click: " + en + $" (costs {mana:0.#}× the mana)",
                    Release = (h, d, c) => { if (BladeSkills.MagePay(d, mana)) release(h, d, c); },
                });
        }

        /// <summary>The remade weapons' lefts (MageLefts.Make calls this last).</summary>
        internal static void Lefts()
        {
            Lefts0();
            Lefts2();
            Lefts3();
            Lefts4();
            Lefts5();
        }

        /// <summary>The remade weapons' arts (BladeSkills.RegisterMagicArts calls this last).</summary>
        internal static void Arts()
        {
            Arts0();
            Arts2();
            Arts3();
            Arts4();
            Arts5();
        }

        // ================================================================== stage 0-1

        static readonly Color DayGold = new Color(1f, 0.85f, 0.35f), Fire = new Color(1f, 0.55f, 0.2f), Frost = new Color(0.6f, 0.85f, 1f);

        static void Lefts0()
        {
            // ---- Daybloom Staff: every third cast a seed falls and grows into a daybloom that puffs motes at enemies.
            MageLefts.Custom(ItemType.DaybloomStaff, "每第三次施法落下一颗种子，落地长成一株太阳花，开花后朝附近敌人喷花粉 4 秒（最多 3 株）",
                "every third cast a seed falls and grows into a daybloom that puffs motes at enemies near for 4 s (3 at most)",
                (s, r, run) =>
                {
                    var t = s;
                    t.Damage = Mathf.Max(1, Mathf.RoundToInt(s.Damage * 0.85f));
                    Cast(t, r);
                    if (run % 3 == 0)
                        MageMech.Add(new DaybloomSeed { Def = s.Def, Pos = s.From, Vel = new Vector2(Mathf.Sign(s.Vel.x) * 0.06f, 0.08f), Key = "daybloom_seed" });
                });

            // ---- Wand of Sparking: sparks leave embers where they land; one striking a burning enemy bursts.
            MageLefts.Custom(ItemType.WandofSparking, "火花落地留下余烬 2.5 秒，敌人踩上着火；打中着火的敌人时火星迸开（0.15 倍）",
                "sparks leave embers where they land for 2.5 s, setting enemies on fire; a spark striking a burning enemy bursts (0.15×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) => MageMech.Add(new Ember { Def = def, Pos = Ground(at, 1.5f) + Vector2.up * 0.15f, Key = "ember", Damage = MageMech.DamageOf(def, 0.08f) }, 6);
                    r.OnStrike = (hm, at) =>
                    {
                        if (!EnemyDebuffs.Has(hm, EnemyDebuffs.OnFire)) { EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 120); return; }
                        foreach (var e in Near(at, 2f)) MageMech.Strike(def, e, MageMech.DamageOf(def, 0.15f));
                        Burst(at, 6, 10, 4f);
                    };
                    Cast(s, r);
                });

            // ---- Wand of Frosting: frost marks; at five the enemy freezes a moment.
            MageLefts.Custom(ItemType.WandofFrosting, "霜屑在敌人身上叠霜印记，叠满 5 层时把它冻住 1.2 秒（0.5 倍）",
                "frost sparks stack frost on an enemy; at five it freezes for 1.2 s (0.5×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        if (MageMech.AddMark(hm, "frost", 1, 5, 5f, Frost, 135) < 5) return;
                        MageMech.ClearMark(hm, "frost");
                        MageMech.Slow(hm, 0.1f, 1.2f);
                        MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.5f));
                        Burst(Centre(hm), 135, 16, 3f, 1.4f);
                        Sound("Item_27", 0.5f, 1.2f);
                    };
                    Cast(s, r);
                });

            // ---- The gem staffs: a mechanism each.
            MageLefts.Custom(ItemType.AmethystStaff, "紫晶光在敌人身上叠印记，第 3 层时敌人身上长出紫晶簇炸开（0.6 倍，再迸出四片晶屑）",
                "amethyst bolts mark an enemy; at the third an amethyst cluster bursts out of it (0.6×, and four shards)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        if (MageMech.AddMark(hm, "amethyst", 1, 3, 4f, new Color(0.8f, 0.3f, 1f), 86) < 3) return;
                        MageMech.ClearMark(hm, "amethyst");
                        var c = Centre(hm);
                        MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.6f));
                        for (int i = 0; i < 4; i++)
                        {
                            float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                            Bolt(def, ProjKind.AmethystBolt, c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, 0.15f, new RangedShot());
                        }
                        Burst(c, 86, 18, 4f, 1.4f);
                        Sound("Item_27", 0.55f, 0.9f);
                    };
                    Cast(s, r);
                });
            MageLefts.Custom(ItemType.TopazStaff, "黄玉光穿过三个敌人，每多穿过一个，在这个敌人身上多打出一道金光（每层 0.3 倍）",
                "topaz bolts go through three enemies; each one past the first takes a golden flash more for every one before it (0.3× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.Pierce = 2;
                    // One bolt a cast: the cast's own count is the bolt's.
                    int struck = 0;
                    r.OnStrike = (hm, at) =>
                    {
                        if (struck > 0)
                        {
                            MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.3f * struck));
                            Burst(at, 87, 6 + struck * 4, 3f);
                        }
                        struck++;
                    };
                    Cast(s, r);
                });
            MageLefts.Custom(ItemType.SapphireStaff, "蓝玉光撞墙反弹，最多弹 3 次，每弹一次更快一些",
                "sapphire bolts bounce off walls up to three times, faster each time",
                (s, r, run) =>
                {
                    GemPrism.Hook(r, ProjKind.SapphireBolt);
                    Cast(s, SapphireBounce(s.Def, r, 0));
                });
            MageLefts.Custom(ItemType.EmeraldStaff, "翡翠光飞到鼠标处时分成三道（两侧的各 0.5 倍）",
                "emerald bolts split in three at the cursor (the side ones 0.5× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    var at = s.Mouse;
                    float far = Mathf.Max(2f * B, Vector2.Distance(s.From, at));
                    var from = s.From;
                    bool split = false;
                    r.OnFly = (pos, vel) =>
                    {
                        if (split || Vector2.Distance(from, pos) < far) return true;
                        split = true;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float a = Mathf.Atan2(vel.y, vel.x) + side * 0.4f;
                            Bolt(def, ProjKind.EmeraldBolt, pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * vel.magnitude, 0.5f);
                        }
                        Burst(pos, 89, 10, 3f);
                        return true;
                    };
                    Cast(s, r);
                });
            MageLefts.Custom(ItemType.RubyStaff, "红玉光命中处炸开，连续打同一个敌人时爆炸越来越大（最多 5 层）",
                "ruby bolts burst where they strike, bigger with each hit on the same enemy in a row (5 at most)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        int n = MageMech.AddMark(hm, "ruby", 1, 5, 2.5f, new Color(1f, 0.2f, 0.2f), 90);
                        float radius = 0.8f + 0.4f * n;
                        foreach (var e in Near(at, radius)) MageMech.Strike(def, e, MageMech.DamageOf(def, 0.035f * n));
                        MageMech.Ring(at, radius * B * 0.6f, 90, 8 + n * 3, 2f + n * 0.6f, 1.3f);
                        Burst(at, 6, 4 + n * 2, 2f + n);
                        if (n >= 3) Sound("Item_14", 0.35f + 0.05f * n, 1.2f);
                    };
                    Cast(s, r);
                });
            MageLefts.Custom(ItemType.AmberStaff, "琥珀光粘在敌人身上，2 秒后凝成琥珀把它减速，再碎成三片化石碎片（0.7 倍）",
                "amber bolts stick to an enemy; 2 s on the amber sets, slowing it, and shatters into three fossil shards (0.7×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) => AmberStick.On(def, hm, 2f, 0.7f, 0.5f, 1f);
                    Cast(s, r);
                });
            MageLefts.Custom(ItemType.DiamondStaff, "钻石光第一次命中时折射，分出两道射向附近另外两个敌人（各 0.4 倍）",
                "a diamond bolt refracts at its first hit into two more at the next two enemies near (0.4× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    bool done = false;
                    r.OnStrike = (hm, at) =>
                    {
                        if (done) return;
                        done = true;
                        int n = 0;
                        foreach (var e in Near(at, 14f))
                        {
                            if (e == hm) continue;
                            Bolt(def, ProjKind.DiamondBolt, at, At(at, Centre(e), 7f), 0.4f);
                            if (++n >= 2) break;
                        }
                        if (n > 0) Burst(at, 91, 12, 3f, 1.3f);
                    };
                    GemPrism.Hook(r, ProjKind.DiamondBolt);
                    Cast(s, r);
                });

            // ---- Thunder Staff: static on what it strikes; struck again, it arcs to every other charged enemy.
            MageLefts.Custom(ItemType.ThunderStaff, "霹雳给敌人叠静电（最多 3 层），再次打中带静电的敌人时，电弧跳向附近所有带静电的敌人（每层 0.25 倍）",
                "its bolt charges an enemy with static (3 at most); striking a charged one again, arcs leap to every charged enemy near (0.25× a charge)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        int had = MageMech.MarkOf(hm, "static");
                        MageMech.AddMark(hm, "static", 1, 3, 6f, new Color(0.5f, 0.8f, 1f), 226);
                        if (had == 0) return;
                        foreach (var (e, n) in MageMech.Marked("static"))
                        {
                            if (e == hm || Vector2.Distance(Centre(e), at) > 18f * B) continue;
                            MageMech.Strike(def, e, MageMech.DamageOf(def, 0.25f * n));
                            Arc(at, Centre(e), def);
                        }
                    };
                    Cast(s, r);
                });

            // ---- The Gray Zapinator: each shot goes wrong its own way.
            MageLefts.Custom(ItemType.ZapinatorGray, "每发随机失控成一种：巨大、分裂、粘在敌人身上 1 秒后爆炸、或飞到尽头折返",
                "every shot goes wrong one way at random: huge, splitting, sticking to an enemy and blowing up a second later, or turning back at the end",
                (s, r, run) => Cast(s, Zap(s, r, 1f)));
        }

        /// <summary>A Sapphire Staff bolt that bounces off walls (up to 3), a fifth faster each time.</summary>
        static RangedShot SapphireBounce(ItemDef def, RangedShot r, int bounces)
        {
            r.OnEnd = (at, vel) =>
            {
                if (bounces >= 3 || vel.sqrMagnitude < 1e-4f) return;
                var dir = vel.normalized;
                var hit = Physics2D.Raycast(at - dir * 0.2f, dir, 0.8f, TerrainMask);
                if (hit.collider == null) return;
                var away = Vector2.Reflect(dir, hit.normal);
                var next = SapphireBounce(def, new RangedShot(), bounces + 1);
                GemPrism.Hook(next, ProjKind.SapphireBolt);
                Bolt(def, ProjKind.SapphireBolt, hit.point + hit.normal * 0.15f, away * vel.magnitude * 1.2f, 1f, next);
                Burst(hit.point, 88, 8, 2.5f);
                Sound("Item_10", 0.35f, 1.3f + 0.1f * bounces);
            };
            return r;
        }

        /// <summary>A Zapinator's shot gone wrong one way at random (its share k of the weapon's damage).</summary>
        static RangedShot Zap(ShootPatterns.Shot s, RangedShot r, float k)
        {
            var def = s.Def;
            switch (Random.Range(0, 4))
            {
                case 0:
                    r.Size = 2f;
                    r.Pierce = 2;
                    r.Trail = 1f;
                    break;
                case 1:
                    r.Shards = 3;
                    r.ShardDamage = 0.35f;
                    break;
                case 2:
                    r.OnStrike = (hm, at) => ZapBomb.On(def, hm, 1f, 0.8f * k);
                    break;
                default:
                    r.OnEnd = (at, vel) =>
                    {
                        var hero = HeroController.SilentInstance;
                        if (hero == null) return;
                        Bolt(def, s.Kind, at, At(at, HeroCentre(hero), Mathf.Max(6f, vel.magnitude)), 0.7f * k, new RangedShot { Pierce = 3, Trail = 0.6f });
                    };
                    break;
            }
            return r;
        }

        static void Arts0()
        {
            // ---- Daybloom Staff: a great sunflower.
            Art(ItemType.DaybloomStaff).Press("向日葵", 6f, 3f, "鼠标处长出一株巨大的向日葵，4 秒内花盘转向一个又一个敌人洒下花粉；场上的太阳花一齐盛开",
                "a great sunflower grows on the cursor, turning to one enemy after another for 4 s as it showers motes; every daybloom out blooms at once",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 6f);
                    MageMech.Add(new Sunflower { Def = d, Pos = at + Vector2.up * 1.6f, Key = "sunflower" }, 1);
                    foreach (var f in MageMech.Things("daybloom")) ((DaybloomFlower)f).Bloom();
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_8", 0.6f, 0.8f);
                    return true;
                });

            // ---- Wand of Sparking: every burning enemy and every ember goes up.
            Art(ItemType.WandofSparking).Press("引燃", 5f, 4f, "附近每个着火的敌人原地炸开（7 倍），场上的余烬一齐爆燃（各 1 倍）；没有着火的敌人时，点燃鼠标附近的敌人（5 倍）",
                "every burning enemy near bursts where it stands (7×) and every ember flares up (1× each); with none burning, the enemies by the cursor are set on fire (5×)",
                (h, d) =>
                {
                    var c = HeroCentre(h);
                    int lit = 0;
                    foreach (var e in Near(c, 25f))
                    {
                        if (!EnemyDebuffs.Has(e, EnemyDebuffs.OnFire)) continue;
                        MageMech.Strike(d, e, MageMech.DamageOf(d, 7f));
                        Burst(Centre(e), 6, 24, 6f, 1.6f);
                        lit++;
                    }
                    foreach (var t in MageMech.Things("ember")) ((Ember)t).Flare();
                    if (lit == 0)
                        foreach (var e in Near(BladeSkills.MageAim(), 8f))
                        {
                            EnemyDebuffs.Add(e, EnemyDebuffs.OnFire, 240);
                            MageMech.Strike(d, e, MageMech.DamageOf(d, 5f));
                            Burst(Centre(e), 6, 14, 4f);
                        }
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_14", 0.6f, 1.1f);
                    return true;
                });

            // ---- Wand of Frosting: every frosted enemy shatters.
            Art(ItemType.WandofFrosting).Press("碎冰", 5f, 4f, "每个带霜印记的敌人碎裂：按层数受伤（每层 0.9 倍）、迸出冰屑并减速；没有时在鼠标处炸开一圈冰霜（2.4 倍）",
                "every frosted enemy shatters: hurt by its frost (0.9× a stack), throwing out frost sparks, slowed; with none, a ring of frost bursts on the cursor (2.4×)",
                (h, d) =>
                {
                    var marked = MageMech.Marked("frost");
                    foreach (var (e, n) in marked)
                    {
                        var c = Centre(e);
                        MageMech.ClearMark(e, "frost");
                        MageMech.Strike(d, e, MageMech.DamageOf(d, 0.9f * n + 0.8f));
                        MageMech.Slow(e, 0.4f, 2f);
                        for (int i = 0; i < n * 2; i++)
                        {
                            float a = i * Mathf.PI * 2f / (n * 2);
                            Bolt(d, ProjKind.FrostSpark, c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, 0.2f);
                        }
                        Burst(c, 135, 20, 4f, 1.5f);
                    }
                    if (marked.Count == 0)
                    {
                        var at = BladeSkills.MageAim();
                        foreach (var e in Near(at, 6f))
                        {
                            MageMech.Strike(d, e, MageMech.DamageOf(d, 2.4f));
                            MageMech.Slow(e, 0.3f, 2f);
                        }
                        MageMech.Ring(at, 0.5f, 135, 30, 6f, 1.6f);
                    }
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_27", 0.7f, 0.8f);
                    return true;
                });

            // ---- The gem staffs.
            Art(ItemType.AmethystStaff).Press("紫晶簇", 6f, 3f, "鼠标处立起一座紫晶簇，5 秒内轮流朝附近每个敌人射出紫晶光",
                "an amethyst cluster rises on the cursor and for 5 s shoots amethyst bolts at each enemy near in turn",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 4f) + Vector2.up * 0.9f;
                    MageMech.Add(new GemTurret { Def = d, Pos = at, Key = "amethyst_cluster", Kind = ProjKind.AmethystBolt, Texture = "Item_181", DustType = 86, K = 0.55f }, 2);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });
            Art(ItemType.TopazStaff).Charge("金枪", 1f, 3f, "按住蓄力一支黄玉长枪，越蓄越长，松开贯穿一条线上的所有敌人（最多 4 倍）",
                "hold to charge a topaz lance, longer the longer you hold; let go and it goes through everything in its line (up to 4×)",
                (h, d, c) =>
                {
                    BladeSkills.MageTurn(h);
                    var from = HeroCentre(h);
                    var aim = BladeSkills.MageAim();
                    Bolt(d, ProjKind.TopazBolt, from, At(from, aim, 9f + 5f * c), 1f + 3f * c,
                        new RangedShot { Size = 1.5f + 2f * c, Pierce = 30, Trail = 1f, Tracer = true, TrailDust = 87 });
                    BladeSkills.MageArtCast(d, null, 1f + c);
                });
            Art(ItemType.SapphireStaff).Press("镜廊", 6f, 3f, "鼠标处展开一面蓝玉镜，先射出两道蓝玉光，6 秒内打到镜面的蓝玉光反射向最近的两个敌人（各 2.2 倍）",
                "a sapphire mirror opens on the cursor, letting out two bolts of its own; for 6 s a sapphire bolt reaching it is reflected at the two enemies nearest (2.2× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new GemPrism { Def = d, Pos = at, Key = "gem_prism", Texture = "Item_177", DustType = 88, Takes = ProjKind.SapphireBolt,
                        Gives = new[] { ProjKind.SapphireBolt, ProjKind.SapphireBolt }, K = 2.2f }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });
            Art(ItemType.EmeraldStaff).Press("翡翠环", 7f, 3f, "六枚翡翠绕身旋转 8 秒，轮流冲向 10 格内的敌人再飞回，碰到的敌人受伤（各 0.5 倍）",
                "six emeralds circle you for 8 s, hurting what they touch and darting at the enemies within 10 blocks in turn (0.5× each)",
                (h, d) =>
                {
                    foreach (var t in MageMech.Things("emerald_ward")) t.Done = true;
                    for (int i = 0; i < 6; i++) MageMech.Add(new WardShard { Def = d, Key = "emerald_ward", Angle = i * 60f, Texture = "Projectile_124", DustType = 89, Damage = MageMech.DamageOf(d, 0.5f) });
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });
            Art(ItemType.RubyStaff).Press("红玉陨", 6f, 4f, "一颗巨大的红玉从天而降砸在鼠标处（3 倍），留下一片燃烧 3 秒的地面",
                "a great ruby falls from the sky onto the cursor (3×) and leaves the ground burning for 3 s",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 8f);
                    MageMech.Add(new RubyFall { Def = d, Pos = at + Vector2.up * 14f * B, Target = at, Key = "ruby_fall" });
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });
            Art(ItemType.AmberStaff).Press("封存", 6f, 3f, "鼠标附近的每个敌人被琥珀封住 1.5 秒（几乎不能动），封存结束时碎裂受伤（1.6 倍）",
                "every enemy by the cursor is sealed in amber for 1.5 s (all but still), and takes 1.6× as it shatters",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    foreach (var e in Near(at, 8f)) AmberStick.On(d, e, 1.5f, 1.6f, 0.08f, 1.5f, true);
                    MageMech.Ring(at, 0.4f, 262, 24, 5f, 1.4f);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });
            Art(ItemType.DiamondStaff).Press("钻石棱镜", 6f, 3f, "鼠标处悬起一颗钻石，先射出紫、蓝、红三色光，6 秒内穿过它的钻石光也化作三色射向三个敌人（各 1.8 倍）",
                "a diamond hangs on the cursor and lets out amethyst, sapphire and ruby bolts; for 6 s a diamond bolt passing through becomes the three too, at three enemies (1.8× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new GemPrism { Def = d, Pos = at, Key = "gem_prism", Texture = "Item_182", DustType = 91, Takes = ProjKind.DiamondBolt,
                        Gives = new[] { ProjKind.AmethystBolt, ProjKind.SapphireBolt, ProjKind.RubyBolt }, K = 1.8f }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Thunder Staff: a storm over the charged.
            Art(ItemType.ThunderStaff).Press("雷暴", 7f, 4f, "鼠标上方聚起雷云，4 秒内每半秒朝每个带静电的敌人和云下的敌人落雷（各 1.2 倍），并给它们叠静电",
                "a thundercloud gathers over the cursor; for 4 s, every half second, lightning strikes every charged enemy and those under it (1.2× each), charging them",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim() + Vector2.up * 6f * B;
                    MageMech.Add(new StormCloud { Def = d, Pos = at, Key = "storm" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- The Gray Zapinator: overload.
            Art(ItemType.ZapinatorGray).Press("超载", 6f, 3f, "2 秒内连射十发，越射越快，每发随机失控；最后枪身过热，喷出一圈冲击波（1 倍）",
                "ten shots over 2 s, faster and faster, each going wrong its own way; then it overheats and blasts out a shockwave (1×)",
                (h, d) =>
                {
                    Overload(h, d, 10, 1f);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });
        }

        /// <summary>The Zapinators' overload: n shots, quicker each, then the shockwave round the player.</summary>
        static void Overload(HeroController hero, ItemDef def, int n, float k)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            int at = 0;
            for (int i = 0; i < n; i++)
            {
                at += Mathf.RoundToInt(Mathf.Lerp(14f, 4f, i / (float)(n - 1)));
                tp.Later(at, (f, v) =>
                {
                    var h = HeroController.SilentInstance;
                    if (h == null) return;
                    var from = HeroCentre(h);
                    var aim = BladeSkills.MageAim();
                    var s = new ShootPatterns.Shot { Def = def, Kind = ProjKind.ZapinatorLaser, From = from, Mouse = aim, Vel = At(from, aim, Mathf.Max(6f, def.ShootSpeed)) };
                    Bolt(def, ProjKind.ZapinatorLaser, from, s.Vel, 0.3f * k, Zap(s, new RangedShot { Item = def }, 0.3f * k));
                    Sound("Item_12", 0.4f, 1f + 0.05f * i);
                });
            }
            tp.Later(at + 10, (f, v) =>
            {
                var h = HeroController.SilentInstance;
                if (h == null) return;
                var c = HeroCentre(h);
                foreach (var e in Near(c, 7f)) MageMech.Strike(def, e, MageMech.DamageOf(def, 1f * k));
                MageMech.Ring(c, 0.5f, 226, 36, 9f, 1.5f);
                MageMech.Ring(c, 0.3f, 6, 24, 6f, 1.3f);
                Sound("Item_14", 0.7f, 0.8f);
            });
        }

        // ================================================================== things (stage 0-1)

        /// <summary>A Daybloom seed falling to the ground, where it grows a daybloom (4 at most).</summary>
        sealed class DaybloomSeed : MageThing
        {
            public DaybloomSeed() { Life = 240; }
            public override void Tick()
            {
                Vel.y -= 0.012f;
                var hit = Physics2D.Raycast(Pos, Vector2.down, Mathf.Max(0.1f, -Vel.y) + 0.1f, TerrainMask);
                if (hit.collider == null) return;
                Done = true;
                MageMech.Add(new DaybloomFlower { Def = Def, Pos = hit.point + Vector2.up * 0.35f, Key = "daybloom" }, 3);
                Burst(hit.point, 3, 6, 1.5f, 0.9f);
            }
            public override void Draw(FxBatch fx, Vector2 px) => Sprite(fx, "Item_307", px, Color.white, Age * 0.1f, 0.8f);
        }

        /// <summary>A daybloom that grows, opens and puffs motes at the enemies near for 4 s, then wilts.</summary>
        sealed class DaybloomFlower : MageThing
        {
            int next = 30;
            public DaybloomFlower() { Life = 30 + 240; }
            public void Bloom()
            {
                for (int i = 0; i < 3; i++) Puff(Nearest(Pos, 14f));
            }
            void Puff(HealthManager hm)
            {
                if (hm == null) return;
                Bolt(Def, ProjKind.DaybloomMote, Pos + Vector2.up * 0.3f, At(Pos, Centre(hm), 6f), 0.3f, new RangedShot { Point = Centre(hm), Seek = 8f, PointSpeed = 3f });
            }
            public override void Tick()
            {
                if (Age < next) return;
                next = Age + 45;
                Puff(Nearest(Pos, 14f));
                Burst(Pos + Vector2.up * 0.3f, 64, 3, 1f, 0.8f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 30f), fade = Mathf.Clamp01((Life - Age) / 30f);
                Sprite(fx, "Item_313", px, new Color(1f, 1f, 1f, fade), Mathf.Sin(Age * 0.08f) * 0.1f, 1.1f * grow);
            }
        }

        /// <summary>The Daybloom's art: a great sunflower turning to one enemy after another, showering motes.</summary>
        sealed class Sunflower : MageThing
        {
            int target;
            public Sunflower() { Life = 240; }
            public override void Tick()
            {
                if (Age % 9 != 0) return;
                var near = Near(Pos, 16f);
                if (near.Count == 0) { Burst(Pos + Vector2.up, 64, 2, 2f); return; }
                var hm = near[target++ % near.Count];
                var from = Pos + Vector2.up * 0.9f;
                Bolt(Def, ProjKind.DaybloomMote, from, At(from, Centre(hm), 7f), 0.18f, new RangedShot { Point = Centre(hm), Seek = 10f, PointSpeed = 4f });
                if (Age % 27 == 0) Sound("Item_8", 0.25f, 1.4f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 20f), fade = Mathf.Clamp01((Life - Age) / 20f);
                Sprite(fx, "Item_63", px, new Color(1f, 1f, 1f, fade), Mathf.Sin(Age * 0.05f) * 0.08f, 3f * grow);
                FxBatch.Instance?.Light(Pos, DayGold, 6f);
                if (Age % 3 == 0) MageMech.Dust(Pos + Vector2.up * 1.1f + Random.insideUnitCircle * 0.8f, 64, Vector2.up * 0.5f, 0.9f);
            }
        }

        /// <summary>An ember left by a spark: it sets what stands in it on fire and burns it a little; flared, it bursts.</summary>
        sealed class Ember : MageThing
        {
            public Ember() { Life = 150; Hurts = true; HitEvery = 30; Size = new Vector2(1f, 0.6f) * MageMech.Block; }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 180);
            public void Flare()
            {
                foreach (var e in Near(Pos, 2.5f)) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1f));
                Burst(Pos, 6, 16, 5f, 1.5f);
                Done = true;
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age % 4 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-0.4f, 0.4f), 0f) * MageMech.Block, 6, Vector2.up * Random.Range(0.5f, 1.5f), Random.Range(0.8f, 1.3f));
                FxBatch.Instance?.Light(Pos, Fire * 0.5f, 1.5f);
            }
        }

        /// <summary>A gem turret: a big gem on the ground shooting its bolts at each enemy near in turn.</summary>
        sealed class GemTurret : MageThing
        {
            public ProjKind Kind;
            public string Texture;
            public int DustType;
            public float K = 0.8f;
            int target;
            public GemTurret() { Life = 300; }
            public override void Tick()
            {
                if (Age % 24 != 0) return;
                var near = Near(Pos, 14f);
                if (near.Count == 0) return;
                var hm = near[target++ % near.Count];
                Bolt(Def, Kind, Pos + Vector2.up * 0.4f, At(Pos, Centre(hm), 8f), K);
                Burst(Pos + Vector2.up * 0.4f, DustType, 5, 2f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 15f), fade = Mathf.Clamp01((Life - Age) / 20f);
                Sprite(fx, Texture, px, new Color(1f, 1f, 1f, fade), Mathf.Sin(Age * 0.05f) * 0.1f, 2.6f * grow);
                if (Age % 5 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 0.6f, DustType, Vector2.up * 0.5f, 1.1f);
            }
        }

        /// <summary>A gem hanging in the air that turns the bolts of one kind passing through it into others, each at an enemy.</summary>
        sealed class GemPrism : MageThing
        {
            public string Texture;
            public int DustType;
            public ProjKind Takes;
            public ProjKind[] Gives;
            public float K;
            public GemPrism() { Life = 360; }
            public override void Tick()
            {
                // As it opens, a volley of its own at the enemies near.
                if (Age == 8) Take(Pos, Vector2.right * 8f);
            }
            /// <summary>A bolt of its kind at `at` (world): taken in (true) and sent on as Gives.</summary>
            public bool Take(Vector2 at, Vector2 vel)
            {
                if (Done || Vector2.Distance(at, Pos) > 1.3f * MageMech.Block) return false;
                var near = Near(Pos, 16f);
                for (int i = 0; i < Gives.Length; i++)
                {
                    var to = near.Count > 0 ? Centre(near[i % near.Count]) : Pos + (Vector2)(Quaternion.Euler(0f, 0f, (i - (Gives.Length - 1) * 0.5f) * 20f) * vel.normalized) * 4f;
                    Bolt(Def, Gives[i], Pos, At(Pos, to, Mathf.Max(7f, vel.magnitude)), K, new RangedShot { Trail = 0.6f });
                }
                Burst(Pos, DustType, 12, 3f, 1.3f);
                Sound("Item_43", 0.4f, 1.4f);
                return true;
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float fade = Mathf.Clamp01((Life - Age) / 20f);
                Sprite(fx, Texture, px + new Vector2(0f, Mathf.Sin(Age * 0.06f) * 3f), new Color(1f, 1f, 1f, fade), Age * 0.03f, 2.2f);
                FxBatch.Instance?.Light(Pos, Color.white * 0.6f, 3f);
                if (Age % 4 == 0) MageMech.Ring(Pos, 0.9f * MageMech.Block, DustType, 1, 0f, 0.9f);
            }

            /// <summary>Hooks a staff's bolts onto the prisms out: a bolt of Takes passing through one is turned.</summary>
            public static void Hook(RangedShot r, ProjKind kind)
            {
                r.OnFly = (pos, vel) =>
                {
                    foreach (var t in MageMech.Things("gem_prism"))
                        if (t is GemPrism g && g.Takes == kind && g.Take(pos, vel)) return false;
                    return true;
                };
            }
        }

        /// <summary>A shard going round the player (a ward), hurting what it touches.</summary>
        sealed class WardShard : MageThing
        {
            public float Angle;
            public string Texture;
            public int DustType;
            HealthManager prey;
            int dashAt, next;
            public WardShard() { Life = 480; Hurts = true; HitEvery = 30; Size = new Vector2(0.7f, 0.7f) * 1f; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Angle += 4f;
                float a = Angle * Mathf.Deg2Rad;
                var home = HeroCentre(hero) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f * MageMech.Block;
                Vel = Vector2.zero;
                // Every second and a half (each shard in its turn) it darts at an enemy within 10 blocks and comes back.
                if (prey == null && Age >= next && Age >= (int)(Angle / 60f) % 6 * 15)
                {
                    prey = Nearest(home, 10f);
                    dashAt = Age;
                    next = Age + 90;
                }
                if (prey != null && (prey.isDead || Age - dashAt > 40)) prey = null;
                if (prey != null && Age - dashAt <= 20)
                {
                    var to = Centre(prey);
                    Pos = Vector2.MoveTowards(Pos, to, 0.9f * MageMech.Block);
                    if (Vector2.Distance(Pos, to) < 0.3f * MageMech.Block) dashAt -= 20;
                }
                else Pos = Vector2.MoveTowards(Pos, home, prey != null ? 0.9f * MageMech.Block : 10f);
                if (Age % 6 == 0) MageMech.Dust(Pos, DustType, Vector2.zero, 0.9f);
            }
            public override void Hit(HealthManager hm) => Burst(Pos, DustType, 6, 2f);
            public override void Draw(FxBatch fx, Vector2 px) => Sprite(fx, Texture, px, Color.white, -(Angle + 90f) * Mathf.Deg2Rad, 1.1f);
        }

        /// <summary>The Ruby Staff's art: a great ruby falling onto its spot; then the ground there burns.</summary>
        sealed class RubyFall : MageThing
        {
            public Vector2 Target;
            public RubyFall() { Life = 120; }
            public override void Tick()
            {
                Vel = Vector2.down * Mathf.Min(1.2f, 0.15f + Age * 0.03f);
                if (Age % 2 == 0) Burst(Pos + Vector2.up * 0.5f, 6, 3, 1.5f, 1.4f);
                if (Pos.y + Vel.y > Target.y) return;
                Done = true;
                foreach (var e in Near(Target, 5f)) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 3f));
                MageMech.Ring(Target, 0.5f, 90, 40, 9f, 1.6f);
                Burst(Target, 6, 30, 7f, 1.6f);
                Sound("Item_14", 0.8f, 0.7f);
                MageMech.Add(new Burning { Def = Def, Pos = Target + Vector2.up * 0.3f, Key = "ruby_burn", Damage = MageMech.DamageOf(Def, 0.35f) }, 2);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                Sprite(fx, "Item_178", px, Color.white, Age * 0.15f, 3.5f);
                FxBatch.Instance?.Light(Pos, new Color(1f, 0.3f, 0.3f), 4f);
            }
        }

        /// <summary>Burning ground: what stands in it burns.</summary>
        sealed class Burning : MageThing
        {
            public Burning() { Life = 180; Hurts = true; HitEvery = 30; Size = new Vector2(6f, 1.2f) * MageMech.Block; }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 180);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age % 2 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-3f, 3f), 0f) * MageMech.Block, 6, Vector2.up * Random.Range(1f, 2.5f), Random.Range(1f, 1.6f));
                FxBatch.Instance?.Light(Pos, Fire * 0.7f, 4f);
            }
        }

        /// <summary>Amber stuck on an enemy: after its time it sets (slowing the enemy) and shatters into fossil shards.</summary>
        sealed class AmberStick : MageThing
        {
            public HealthManager On_;
            public float K, Slow, SlowFor;
            public bool Sealed;
            bool silent;
            static readonly Dictionary<HealthManager, AmberStick> stuck = new Dictionary<HealthManager, AmberStick>();

            public static void On(ItemDef def, HealthManager hm, float seconds, float k, float slow, float slowFor, bool seal = false)
            {
                if (hm == null || hm.isDead) return;
                if (stuck.TryGetValue(hm, out var s) && s != null && !s.Done)
                {
                    if (!seal || s.Sealed) return;
                    // A seal over a smaller amber: that one goes quietly.
                    s.silent = true;
                    s.Done = true;
                }
                var a = new AmberStick { Def = def, On_ = hm, Life = Mathf.RoundToInt(seconds * 60f), K = k, Slow = slow, SlowFor = slowFor, Sealed = seal, Pos = Centre(hm), Key = "amber" };
                stuck[hm] = a;
                if (seal) MageMech.Slow(hm, slow, seconds, new Color(1f, 0.75f, 0.3f));
                MageMech.Add(a);
            }
            public override void Tick()
            {
                if (On_ == null || On_.isDead) { Done = true; return; }
                Pos = Centre(On_);
                Vel = Vector2.zero;
            }
            public override void End()
            {
                if (stuck.TryGetValue(On_, out var mine) && mine == this) stuck.Remove(On_);
                if (silent || On_ == null || On_.isDead) return;
                var c = Centre(On_);
                MageMech.Strike(Def, On_, MageMech.DamageOf(Def, K));
                if (!Sealed) MageMech.Slow(On_, Slow, SlowFor);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f + Random.value;
                    Bolt(Def, ProjKind.AmberBolt, c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, 0.2f);
                }
                Burst(c, 262, 16, 3.5f, 1.3f);
                Sound("Item_27", 0.5f, 0.7f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float k = Age / (float)Mathf.Max(1, Life);
                Sprite(fx, "Item_999", px, new Color(1f, 1f, 1f, 0.6f + 0.4f * k), 0f, Sealed ? 2.2f : 0.8f + 0.6f * k);
                if (Age % 6 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 0.4f, 262, Vector2.zero, 0.9f);
            }
        }

        /// <summary>A Zapinator's sticky charge on an enemy: it goes off a second on, round it.</summary>
        sealed class ZapBomb : MageThing
        {
            HealthManager on;
            float k;
            public static void On(ItemDef def, HealthManager hm, float seconds, float k)
            {
                if (hm == null || hm.isDead) return;
                MageMech.Add(new ZapBomb { Def = def, on = hm, k = k, Life = Mathf.RoundToInt(seconds * 60f), Pos = Centre(hm), Key = "zap_bomb" }, 6);
            }
            public override void Tick()
            {
                if (on != null && !on.isDead) Pos = Centre(on);
                Vel = Vector2.zero;
                if (Age % 8 == 0) MageMech.Dust(Pos, 226, Random.insideUnitCircle * 2f, 1.1f);
            }
            public override void End()
            {
                foreach (var e in Near(Pos, 3f)) MageMech.Strike(Def, e, MageMech.DamageOf(Def, k));
                MageMech.Ring(Pos, 0.3f, 226, 20, 6f, 1.3f);
                Sound("Item_14", 0.45f, 1.3f);
            }
        }

        /// <summary>The Thunder Staff's storm cloud: every half second, lightning on every charged enemy and the one under it.</summary>
        sealed class StormCloud : MageThing
        {
            public string MarkKey = "static";
            public float K = 1.2f;
            public StormCloud() { Life = 240; }
            public override void Tick()
            {
                if (Age % 30 != 15) return;
                var hit = new HashSet<HealthManager>();
                foreach (var (e, n) in MageMech.Marked(MarkKey))
                    if (Vector2.Distance(Centre(e), Pos) < 25f * MageMech.Block) hit.Add(e);
                foreach (var e in Along(Pos, Pos + Vector2.down * 16f * MageMech.Block, 8f)) hit.Add(e);
                foreach (var e in hit)
                {
                    var c = Centre(e);
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, K));
                    if (MarkKey == "static") MageMech.AddMark(e, "static", 1, 3, 6f, new Color(0.5f, 0.8f, 1f), 226);
                    Arc(Pos + new Vector2(Random.Range(-1.5f, 1.5f), -0.5f) * MageMech.Block, c, Def);
                    Burst(c, 226, 8, 3f);
                }
                if (hit.Count > 0) Sound("Item_122", 0.45f, 1.1f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 3; i++)
                    MageMech.Dust(Pos + new Vector2(Random.Range(-3.5f, 3.5f), Random.Range(-0.8f, 0.8f)) * MageMech.Block, 31, new Vector2(Random.Range(-0.3f, 0.3f), 0f), Random.Range(1.4f, 2.2f), 120);
                if (Age % 10 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-3f, 3f), -0.4f) * MageMech.Block, 226, Vector2.down * 2f, 1f);
                FxBatch.Instance?.Light(Pos, new Color(0.4f, 0.6f, 1f) * 0.5f, 5f);
            }
        }
    }
}
