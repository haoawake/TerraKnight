using System.Collections.Generic;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The 1.4.5 whip tags (TagEffectStack / TagEffectState, spec-summoner C.6). Only the last
    /// whips used have live tags (maxTagEffects: one, more with the tag bracelets): striking with
    /// a whip past that wipes the oldest one's tags off every enemy and takes away its player
    /// buff. A tag lasts 240 ticks (×3 with the Silver Bracer); while it does,
    /// every minion or minion shot hitting that enemy adds the whip's tag damage (and Morning
    /// Star's 10% crit). Firecracker and Durendal also arm a one-shot proc on each enemy struck:
    /// the next minion hit deals ×2.75 and blows up on the others, or gets 30 more damage.
    /// </summary>
    internal static class SummonTags
    {
        struct Effect
        {
            public int TagDamage, CritChance, PlayerBuff, PlayerBuffTime;
            /// <summary>WhipTagEffect.TagDuration: how long a tag (and a proc) lasts; 0 = 240.</summary>
            public int Duration;
            public int TagDuration => Duration > 0 ? Duration : 240;
        }

        static readonly Dictionary<WhipKind, Effect> effects = new Dictionary<WhipKind, Effect>
        {
            [WhipKind.Leather] = new Effect { TagDamage = 4 },
            [WhipKind.Snapthorn] = new Effect { TagDamage = 6, PlayerBuff = PlayerBuffs.ThornWhip, PlayerBuffTime = 180 },
            [WhipKind.SpinalTap] = new Effect { TagDamage = 7 },
            [WhipKind.Firecracker] = new Effect { TagDamage = 0 },
            // The Cool Whip's buff is added by its own strike, not the tag (PlayerBuffAppliedManually).
            [WhipKind.CoolWhip] = new Effect { TagDamage = 6 },
            [WhipKind.Durendal] = new Effect { TagDamage = 9, PlayerBuff = PlayerBuffs.SwordWhip, PlayerBuffTime = 180 },
            [WhipKind.MorningStar] = new Effect { TagDamage = 8, CritChance = 10 },
            [WhipKind.DarkHarvest] = new Effect { TagDamage = 15, PlayerBuff = PlayerBuffs.ScytheWhip, PlayerBuffTime = 180 },
            [WhipKind.Kaleidoscope] = new Effect { TagDamage = 20, CritChance = 10 },
            [WhipKind.SlimeWhip] = new Effect { TagDamage = 3 },
            // The Cob Whip's buff (its spider) is added by its own strike (PlayerBuffAppliedManually).
            [WhipKind.CobWhip] = new Effect { TagDamage = 3 },
            [WhipKind.CorruptWhip] = new Effect { TagDamage = 5 },
            [WhipKind.CrimsonWhip] = new Effect { TagDamage = 5 },
            [WhipKind.MeteorWhip] = new Effect { TagDamage = 2 },
            [WhipKind.FlowerWhip] = new Effect { TagDamage = 9 },
            [WhipKind.EelWhip] = new Effect { TagDamage = 12, CritChance = 5 },
            [WhipKind.ConstellationWhip] = new Effect { TagDamage = 15, CritChance = 15, Duration = 480 },
            [WhipKind.MoonLordWhip] = new Effect { TagDamage = 25, CritChance = 10 },
        };

        /// <summary>TagEffectState: one whip's tags and procs, per enemy (ticks left).</summary>
        sealed class State
        {
            public WhipKind Whip;
            public readonly Dictionary<int, int> Tag = new Dictionary<int, int>(), Proc = new Dictionary<int, int>();
        }

        /// <summary>TagEffectStack: the whips with live tags, the most recent first; maxTagEffects of them at most (5 in all).</summary>
        static readonly List<State> stack = new List<State>();
        static readonly Dictionary<int, HealthManager> enemies = new Dictionary<int, HealthManager>();
        static readonly List<int> keys = new List<int>();
        const int MaxEffects = 5;

        /// <summary>maxTagEffects: one, and one more for each tag bracelet (Silver Bracer, Mobius Strip, Wicked Armlet).</summary>
        static int MaxTags => Mathf.Min(MaxEffects, 1 + Equipment.Current.MaxTagEffects);

        /// <summary>tagEffectDuration: the tags' time times 1, three with the Silver Bracer.</summary>
        static float DurationScale => 1f + Equipment.Current.TagDuration;

        /// <summary>The whip used last.</summary>
        public static WhipKind Active => stack.Count > 0 ? stack[0].Whip : WhipKind.None;

        static State Find(WhipKind whip)
        {
            foreach (var s in stack) if (s.Whip == whip) return s;
            return null;
        }

        /// <summary>LimitActiveEffects: the oldest whips past the limit lose their tags and their player buff (OnRemovedFromPlayer).</summary>
        static void Limit(int limit)
        {
            limit = Mathf.Min(limit, MaxEffects);
            while (stack.Count > limit)
            {
                var old = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (effects[old.Whip].PlayerBuff > 0) PlayerBuffs.Remove(effects[old.Whip].PlayerBuff);
            }
        }

        /// <summary>TrySetActiveEffect: a whip not live takes a place (the oldest going when full); it becomes the most recent.</summary>
        static State SetActive(WhipKind whip)
        {
            var s = Find(whip);
            if (s == null)
            {
                Limit(MaxTags - 1);
                s = new State { Whip = whip };
                stack.Add(s);
            }
            stack.Remove(s);
            stack.Insert(0, s);
            return s;
        }

        static int Duration(WhipKind whip) => (int)(effects[whip].TagDuration * DurationScale);

        static bool Live(Dictionary<int, int> timers, int id) => timers.TryGetValue(id, out int t) && t > 0;

        /// <summary>TryApplyTagToNPC: a whip (or its snowflake) struck an enemy; its player buff lasts as much longer as its tags.</summary>
        public static void Apply(WhipKind whip, HealthManager hm)
        {
            if (whip == WhipKind.None || hm == null) return;
            var s = SetActive(whip);
            int id = hm.GetInstanceID();
            enemies[id] = hm;
            s.Tag[id] = Duration(whip);
            var e = effects[whip];
            if (e.PlayerBuff > 0) PlayerBuffs.Add(e.PlayerBuff, (int)(e.PlayerBuffTime * DurationScale));
        }

        /// <summary>TryEnableProcOnNPC (Firecracker's, Starfall's and the Flower whip's first enemy each crack; every Durendal, Eel and Moon Lord whip hit).</summary>
        public static void EnableProc(WhipKind whip, HealthManager hm)
        {
            if (hm == null) return;
            var s = Find(whip) ?? SetActive(whip);
            int id = hm.GetInstanceID();
            enemies[id] = hm;
            s.Proc[id] = Duration(whip);
        }

        /// <summary>CanProcOnNPC: the whip's proc is armed on this enemy (the Constellation's "strike again").</summary>
        public static bool CanProc(WhipKind whip, HealthManager hm)
        {
            var s = Find(whip);
            return hm != null && s != null && Live(s.Proc, hm.GetInstanceID());
        }

        /// <summary>RemoveAllProcs: every enemy's proc of the whip is spent.</summary>
        public static void RemoveAllProcs(WhipKind whip) => Find(whip)?.Proc.Clear();

        /// <summary>
        /// Damage_PVE_Inner's tag step for a minion (or minion shot) hit (TagEffectStack.ModifyHit,
        /// P:12945-12967): num7 plus the tag damage of every whip that tagged the enemy, plus
        /// Durendal's +30 and the eel's zap, times Firecracker's 2.75; a crit only by a tag's roll,
        /// or for a sentry the Templar's Sling's one in ten. The Armlet of Ruin's strike on a
        /// tagged hit (P:13447-13450), from the minion (else the enemy).
        /// </summary>
        public static int Modify(HealthManager hm, int num7, out bool crit, float tagMult = 1f, bool sentry = false, Vector2 fromPx = default)
        {
            crit = false;
            if (hm == null) return num7;
            if (sentry && Equipment.Current.GolemSentryBackpack && Random.Range(0, 10) == 0) crit = true;
            int id = hm.GetInstanceID();
            int tag = 0, flat = 0;
            float mult = 1f;
            bool tagged = false;
            foreach (var s in stack)
            {
                if (!Live(s.Tag, id)) continue;
                tagged = true;
                var e = effects[s.Whip];
                tag += (int)(e.TagDamage * tagMult);
                if (Random.Range(0, 100) < e.CritChance) crit = true;
                if (!Live(s.Proc, id)) continue;
                if (s.Whip == WhipKind.Firecracker) mult *= 2.75f;
                if (s.Whip == WhipKind.Durendal)
                {
                    flat += 30;
                    SwordFx.Instance?.ProcFlash(hm);
                }
                if (s.Whip == WhipKind.EelWhip) flat += EelZap(hm);
            }
            if (!tagged) return num7;
            int dmg = num7 + tag + flat;
            if (Equipment.Current.ArmletOfRuin)
            {
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                var from = fromPx != default ? fromPx : Effects.TerraParticles.ToPx(col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position);
                dmg += AccessoryEffects.SnapStrike(from, hm, 2);
            }
            return (int)(dmg * mult);
        }

        /// <summary>
        /// TagEffectStack.OnHit after a minion hit (the minion's centre in Terraria px), for each
        /// whip that tagged the enemy: its OnTaggedHit (Dark Harvest's lightning leaps from it,
        /// Kaleidoscope's stars burst at the minion, the Constellation's energy grows on it); then
        /// its proc (Firecracker's blast, Starfall's meteor, the petals, the phantom slash) goes off
        /// and is spent.
        /// </summary>
        public static void OnHit(HealthManager hm, int num7, Vector2 minionPx, float minionKb = 0f)
        {
            if (hm == null || stack.Count == 0) return;
            int id = hm.GetInstanceID();
            foreach (var s in stack.ToArray())
            {
                if (!Live(s.Tag, id)) continue;
                var w = s.Whip;
                if (w == WhipKind.DarkHarvest) TerraProjectiles.Instance?.ShootScytheLightning(hm, effects[w].TagDamage);
                if (w == WhipKind.Kaleidoscope) Effects.FxParticles.RainbowRodHit(minionPx);
                // WhipTagEffect_Constellation.OnTaggedHit: the cosmic energy grows on it (480 ticks); minions never spend it.
                if (w == WhipKind.ConstellationWhip)
                {
                    enemies[id] = hm;
                    s.Proc[id] = Duration(w);
                    continue;
                }
                if (!Live(s.Proc, id)) continue;
                if (w == WhipKind.Firecracker) Minions.Instance?.FireBlast(hm, (int)(num7 * 2.75f));
                // WhipTagEffect_Starcrash: a meteor falls on it (1.33× the minion's hit); the flowers burst into petals (2.25× split);
                // the Moon Lord whip's phantom slashes it (20, up to three in a chain).
                if (w == WhipKind.MeteorWhip) TerraProjectiles.Instance?.ShootWhipMeteor(hm, (int)(num7 * 1.33f), minionKb);
                if (w == WhipKind.FlowerWhip) TerraProjectiles.Instance?.ShootFlowerPetals(hm, num7);
                if (w == WhipKind.MoonLordWhip) TerraProjectiles.Instance?.ShootPhantomSlash(hm, 20, 0, Vector2.zero);
                s.Proc.Remove(id);
            }
        }

        static readonly Collider2D[] zapHits = new Collider2D[64];

        /// <summary>
        /// WhipTagEffect_ElectricEel.ModifyProcHit: before the minion's hit lands, up to five other
        /// enemies within 450 px of the struck one are zapped for a flat 20 (a dotted bolt between
        /// them); NPC_Hit_34; the hit gains 50, and 50 more per enemy zapped.
        /// </summary>
        static int EelZap(HealthManager struck)
        {
            var melee = TerraMod.Instance?.Melee;
            var sc = struck.GetComponent<Collider2D>() ?? struck.GetComponentInChildren<Collider2D>();
            if (melee == null || sc == null) return 50;
            var centre = (Vector2)sc.bounds.center;
            var done = new HashSet<HealthManager> { struck };
            int n = Physics2D.OverlapCircleNonAlloc(centre, 500f * Effects.TerraParticles.Px, zapHits, MeleeHits.HitMask), zapped = 0;
            for (int i = 0; i < n && zapped < 5; i++)
            {
                var hm = zapHits[i].GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !done.Add(hm)) continue;
                var col = hm.GetComponent<Collider2D>() ?? zapHits[i];
                if (Vector2.Distance(centre, col.bounds.ClosestPoint(centre)) / Effects.TerraParticles.Px > 450f) continue;
                zapped++;
                var a = Effects.TerraParticles.ToPx(RandomIn(sc.bounds));
                var b = Effects.TerraParticles.ToPx(RandomIn(col.bounds));
                Effects.FxParticles.BlueLightningSmallLong(b, a - b);
                melee.HitWith(col, 20, 0f, 0f, col.bounds.center.x >= centre.x ? 1f : -1f, col.bounds.center);
            }
            TerraMod.Instance?.PlaySound("NPC_Hit_34");
            return 50 + 50 * zapped;
        }

        static Vector2 RandomIn(Bounds b) => new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));

        /// <summary>Whether an enemy carries any whip's tag (dev).</summary>
        public static bool Tagged(HealthManager hm)
        {
            if (hm == null) return false;
            foreach (var st in stack) if (Live(st.Tag, hm.GetInstanceID())) return true;
            return false;
        }

        /// <summary>Dev: how many whips have live tags.</summary>
        public static int LiveWhips => stack.Count;

        /// <summary>TagEffectStack.Update: taking gear off shrinks the stack at once; every timer down a tick; dead enemies drop out.</summary>
        public static void Tick()
        {
            if (stack.Count == 0) return;
            Limit(MaxTags);
            foreach (var st in stack)
            {
                Count(st.Tag);
                Count(st.Proc);
            }
        }

        static void Count(Dictionary<int, int> timers)
        {
            if (timers.Count == 0) return;
            keys.Clear();
            keys.AddRange(timers.Keys);
            foreach (var k in keys)
            {
                bool gone = !enemies.TryGetValue(k, out var hm) || hm == null || hm.isDead;
                if (gone || --timers[k] <= 0) timers.Remove(k);
            }
        }

        public static void Clear()
        {
            stack.Clear();
            enemies.Clear();
        }
    }
}
