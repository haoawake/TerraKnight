using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>
    /// Something a remade magic weapon leaves in the world (法师武器创意重做设计.md, the user's 10-28 "我希望你自由发挥搞得更有
    /// 创意"): a flower that shoots, a patch of embers, a crystal that splits bolts, a storm cloud, a link of light. It rides a
    /// projectile of its own kind (ProjKind.MageThing), so it strikes as the weapon's shots do (its class, its immunity per
    /// enemy, Hurts and HitEvery); everything else is its own. World units, ticks of 60 a second.
    /// </summary>
    internal abstract class MageThing
    {
        public ItemDef Def;
        public Vector2 Pos, Vel;
        /// <summary>Its striking box (world), when it Hurts.</summary>
        public Vector2 Size = new Vector2(0.6f, 0.6f);
        public int Damage;
        public float Knockback, Crit;
        public bool Hurts;
        /// <summary>Ticks between two strikes on the same enemy.</summary>
        public int HitEvery = 20;
        public int Age, Life = 600;
        public bool Done;
        /// <summary>A kind of thing of a weapon (its count is kept under a cap: MageMech.Count/Oldest).</summary>
        public string Key;

        /// <summary>Once a tick, before it moves by Vel.</summary>
        public virtual void Tick() { }
        /// <summary>Each frame, at its place in Terraria px (y down).</summary>
        public virtual void Draw(FxBatch fx, Vector2 px) { }
        public virtual void Hit(HealthManager hm) { }
        public virtual void End() { }
    }

    /// <summary>
    /// The shared mechanisms of the remade magic weapons (法师武器创意重做设计.md): marks stacked on enemies (each enemy its own,
    /// shown over it), things left in the world (MageThing), a strike on one enemy, the enemies near a point. Every effect "on
    /// the enemies" goes to each of them (the user's 10-28 "有很多说对敌人额外召唤的都没有针对所有敌人").
    /// </summary>
    internal static class MageMech
    {
        public const float Block = 16f * TerraParticles.Px;

        // ------------------------------------------------------------------ marks

        internal sealed class Mark
        {
            public int Count, Max;
            public float Until;
            public Color Colour;
            public int Dust;
        }

        static readonly Dictionary<HealthManager, Dictionary<string, Mark>> marks = new Dictionary<HealthManager, Dictionary<string, Mark>>();
        public static int DevMarks, DevThings;

        /// <summary>Adds to an enemy's mark of this key (kept for `seconds` from now, at most `max`); its count now.</summary>
        public static int AddMark(HealthManager hm, string key, int add, int max, float seconds, Color colour, int dust)
        {
            if (hm == null || hm.isDead) return 0;
            if (!marks.TryGetValue(hm, out var set)) marks[hm] = set = new Dictionary<string, Mark>();
            if (!set.TryGetValue(key, out var m)) set[key] = m = new Mark();
            m.Count = Mathf.Min(max, m.Count + add);
            m.Max = max;
            m.Until = Time.time + seconds;
            m.Colour = colour;
            m.Dust = dust;
            DevMarks++;
            return m.Count;
        }

        public static int MarkOf(HealthManager hm, string key) =>
            hm != null && marks.TryGetValue(hm, out var set) && set.TryGetValue(key, out var m) && m.Until > Time.time ? m.Count : 0;

        public static void ClearMark(HealthManager hm, string key)
        {
            if (hm != null && marks.TryGetValue(hm, out var set)) set.Remove(key);
        }

        /// <summary>Every live enemy with this mark, and its count (a copy: safe to clear marks while going through it).</summary>
        public static List<(HealthManager hm, int count)> Marked(string key)
        {
            var list = new List<(HealthManager, int)>();
            foreach (var kv in marks)
            {
                var hm = kv.Key;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                if (kv.Value.TryGetValue(key, out var m) && m.Until > Time.time && m.Count > 0) list.Add((hm, m.Count));
            }
            return list;
        }

        static readonly List<HealthManager> gone = new List<HealthManager>();
        static readonly List<string> expired = new List<string>();
        static int markTick;

        /// <summary>Once a frame (MageFire.Tick): marks run out; each marked enemy shows its marks as pips of their dust over it.</summary>
        // ------------------------------------------------------------------ pulls

        /// <summary>An enemy drawn toward a point at `speed` world units a second this frame, not into rock (a Target Dummy stays on its furniture).</summary>
        public static void Pull(HealthManager hm, Vector2 to, float speed)
        {
            if (hm == null || hm.isDead || EnemyStats.Unstoppable(EnemyStats.Of(hm)) || World.TargetDummy.Is(hm)) return;
            var from = (Vector2)hm.transform.position;
            var way = to - from;
            float step = Mathf.Min(way.magnitude, speed * Time.deltaTime);
            if (step < 0.001f) return;
            var dir = way.normalized;
            if (Physics2D.Raycast(TerraProjectiles.EnemyCentre(hm), dir, step + 0.4f, 1 << World.ChunkColliders.TerrainLayer).collider != null) return;
            hm.transform.position = from + dir * step;
        }

        public static void Tick()
        {
            TickSlowed();
            gone.Clear();
            foreach (var kv in marks)
            {
                if (kv.Key == null || kv.Key.isDead || !kv.Key.gameObject.activeInHierarchy) { gone.Add(kv.Key); continue; }
                expired.Clear();
                foreach (var m in kv.Value) if (m.Value.Until <= Time.time || m.Value.Count <= 0) expired.Add(m.Key);
                foreach (var k in expired) kv.Value.Remove(k);
                if (kv.Value.Count == 0) gone.Add(kv.Key);
            }
            foreach (var hm in gone) marks.Remove(hm);
            if (++markTick % 6 != 0) return;
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            foreach (var kv in marks)
            {
                var top = TerraProjectiles.EnemyTop(kv.Key);
                int row = 0;
                foreach (var m in kv.Value.Values)
                {
                    // A row of pips over the enemy, one a stack, the full row flaring.
                    for (int i = 0; i < m.Count; i++)
                    {
                        float x = (i - (m.Count - 1) * 0.5f) * 0.28f;
                        var at = TerraParticles.ToPx(top + new Vector2(x, 0.45f + row * 0.3f));
                        int d = pp.NewDust(at - new Vector2(2f, 2f), 4, 4, m.Dust, 0f, 0f, 60, m.Count >= m.Max ? 1.25f : 0.85f);
                        if (d < 0) continue;
                        ref var dd = ref pp[d];
                        dd.Position = at;
                        dd.Velocity = Vector2.zero;
                        dd.NoGravity = true;
                    }
                    row++;
                }
            }
        }

        // ------------------------------------------------------------------ slowed and frozen

        sealed class Slowed { public float Until, Factor; public tk2dSprite Sprite; public Color Was; public bool Tinted; public Hold Held; }
        static readonly Dictionary<HealthManager, Slowed> slowed = new Dictionary<HealthManager, Slowed>();

        /// <summary>
        /// An enemy held still (frozen, turned to stone, sealed, rooted) as the network's puppets are (Net.Enemies.MakePuppet):
        /// its FSMs off and deaf to events (and the remade AI's paused), its other scripts off, its body kinematic, its
        /// animation paused, kept where it was. Its body's speed cut alone held only the enemies a rigidbody moves: the rest
        /// (the user's 10-28 "蛇发女妖头的石化根本没有用") walked and attacked on.
        /// </summary>
        sealed class Hold
        {
            public PlayMakerFSM[] Fsms;
            public bool[] Paused;
            public MonoBehaviour[] Parts;
            public bool[] On;
            public Rigidbody2D Body;
            public RigidbodyType2D Type;
            /// <summary>
            /// A crawler's or a climber's (HK's Crawler, Climber): its body's velocity when held, or the last its coroutine set while
            /// held (they set it only when they turn), given back. Any other is let go standing: its own scripts set its speed again,
            /// and a speed given back was a knockback's as often as not (it was hit just before), which then slid it on for good
            /// (the user's 10-28 "石化结束后敌人疯狂往右边平移").
            /// </summary>
            public bool Turner;
            public Vector2 Vel;
            public tk2dSpriteAnimator Anim;
            public Vector3 At;
        }

        /// <summary>A slow to this or less holds the enemy still.</summary>
        const float HoldBelow = 0.1f;

        /// <summary>
        /// An enemy slowed for a while: its body's speed held to `factor` of what its script gives it each frame; to a tenth
        /// or less, held still (Hold). Tinted when nearly stopped: ice-blue, or `look` (stone grey). A longer or stronger one
        /// replaces a weaker.
        /// </summary>
        public static void Slow(HealthManager hm, float factor, float seconds, Color? look = null)
        {
            if (hm == null || hm.isDead) return;
            // Bosses and elites aren't held still: slowed a fifth at most.
            if (EnemyStats.Unstoppable(EnemyStats.Of(hm))) factor = Mathf.Max(factor, 0.8f);
            if (!slowed.TryGetValue(hm, out var s)) slowed[hm] = s = new Slowed { Factor = 1f };
            s.Factor = Mathf.Min(s.Factor, factor);
            s.Until = Mathf.Max(s.Until, Time.time + seconds);
            if (s.Factor <= 0.3f && !s.Tinted)
            {
                s.Sprite = hm.GetComponent<tk2dSprite>();
                var c = look ?? new Color(0.6f, 0.85f, 1f);
                if (s.Sprite != null) { s.Was = s.Sprite.color; s.Sprite.color = new Color(c.r, c.g, c.b, s.Was.a); s.Tinted = true; }
            }
            // A Target Dummy stays as it is; another player's enemy (a puppet) is moved by its owner.
            if (s.Factor <= HoldBelow && s.Held == null && !World.TargetDummy.Is(hm) && !EnemyStats.Of(hm).Net) s.Held = HoldStill(hm);
        }

        public static bool IsSlowed(HealthManager hm) => hm != null && slowed.TryGetValue(hm, out var s) && s.Until > Time.time;

        /// <summary>Dev: whether an enemy is held still now.</summary>
        public static bool IsHeld(HealthManager hm) => hm != null && slowed.TryGetValue(hm, out var s) && s.Held != null;

        static Hold HoldStill(HealthManager hm)
        {
            var h = new Hold { Fsms = hm.GetComponentsInChildren<PlayMakerFSM>(true), Anim = hm.GetComponent<tk2dSpriteAnimator>(), Body = hm.GetComponent<Rigidbody2D>(), At = hm.transform.position,
                Turner = hm.GetComponent<Climber>() != null || hm.GetComponent<Crawler>() != null };
            h.Paused = new bool[h.Fsms.Length];
            for (int i = 0; i < h.Fsms.Length; i++)
            {
                var f = h.Fsms[i];
                Net.Enemies.Freeze(f);
                // Only what this hold paused is let go with it (the remade AI pauses FSMs of its own).
                if (f.Fsm != null && !EnemyAi.EnemyAi.Paused.Contains(f.Fsm)) { EnemyAi.EnemyAi.Paused.Add(f.Fsm); h.Paused[i] = true; }
            }
            var parts = new List<MonoBehaviour>();
            foreach (var b in hm.GetComponents<MonoBehaviour>())
                if (b != null && !(b is PlayMakerFSM) && !Net.Enemies.Keep(b)) parts.Add(b);
            h.Parts = parts.ToArray();
            h.On = new bool[h.Parts.Length];
            for (int i = 0; i < h.Parts.Length; i++)
            {
                h.On[i] = h.Parts[i].enabled;
                h.Parts[i].enabled = false;
            }
            if (h.Body != null)
            {
                h.Type = h.Body.bodyType;
                h.Vel = h.Body.linearVelocity;
                h.Body.linearVelocity = Vector2.zero;
                h.Body.bodyType = RigidbodyType2D.Kinematic;
            }
            if (h.Anim != null) h.Anim.Pause();
            DevHolds++;
            return h;
        }

        /// <summary>Everything a hold switched off, back as it was (an FSM gone with its scene just leaves the records).</summary>
        static void LetGo(Hold h)
        {
            for (int i = 0; i < h.Fsms.Length; i++)
            {
                var f = h.Fsms[i];
                if (h.Paused[i] && !ReferenceEquals(f, null) && f != null && f.Fsm != null) EnemyAi.EnemyAi.Paused.Remove(f.Fsm);
                Net.Enemies.Thaw(f);
            }
            for (int i = 0; i < h.Parts.Length; i++)
                if (h.Parts[i] != null) h.Parts[i].enabled = h.On[i];
            if (h.Body != null)
            {
                h.Body.bodyType = h.Type;
                h.Body.linearVelocity = h.Turner ? h.Vel : Vector2.zero;
            }
            if (h.Anim != null) h.Anim.Resume();
        }

        public static int DevHolds;

        /// <summary>An enemy dying held still is let go first, so its scripts take its death as the game sends it.</summary>
        internal static void Release(HealthManager hm)
        {
            if (hm == null || !slowed.TryGetValue(hm, out var s)) return;
            if (s.Held != null) LetGo(s.Held);
            s.Held = null;
            if (s.Tinted && s.Sprite != null) s.Sprite.color = s.Was;
            slowed.Remove(hm);
        }

        static readonly List<HealthManager> thawed = new List<HealthManager>();

        static void TickSlowed()
        {
            thawed.Clear();
            foreach (var kv in slowed)
            {
                var hm = kv.Key;
                var s = kv.Value;
                if (hm == null || hm.isDead || s.Until <= Time.time)
                {
                    if (s.Held != null) LetGo(s.Held);
                    if (s.Tinted && s.Sprite != null) s.Sprite.color = s.Was;
                    thawed.Add(hm);
                    continue;
                }
                var rb = hm.GetComponent<Rigidbody2D>();
                if (s.Held != null)
                {
                    // Where it was held (a hit's recoil or a pull doesn't carry it off). Its coroutines run on through a
                    // hold: a speed one of them sets meanwhile (a climber's at the end of a turn) is the one given back.
                    hm.transform.position = s.Held.At;
                    if (rb != null && s.Held.Turner && rb.linearVelocity.sqrMagnitude > 1e-6f) s.Held.Vel = rb.linearVelocity;
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                    continue;
                }
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity *= Mathf.Pow(s.Factor, Time.deltaTime * 60f);
                else if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic) rb.linearVelocity *= s.Factor;
            }
            foreach (var hm in thawed) slowed.Remove(hm);
        }

        [HarmonyLib.HarmonyPatch]
        static class HoldPatches
        {
            [HarmonyLib.HarmonyPrefix]
            [HarmonyLib.HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
            static void LetGoBeforeDeath(HealthManager __instance) => Release(__instance);
        }

        // ------------------------------------------------------------------ enemies

        /// <summary>The live enemies whose body is within `radius` (world) of a point, nearest first; in sight of it when asked.</summary>
        public static List<HealthManager> EnemiesNear(Vector2 at, float radius, bool sight = true)
        {
            var list = new List<HealthManager>();
            foreach (var col in Physics2D.OverlapCircleAll(at, radius, 1 << 11))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || list.Contains(hm)) continue;
                if (sight && Physics2D.Linecast(at, TerraProjectiles.EnemyCentre(hm), 1 << World.ChunkColliders.TerrainLayer)) continue;
                list.Add(hm);
            }
            list.Sort((a, b) => (TerraProjectiles.EnemyCentre(a) - at).sqrMagnitude.CompareTo((TerraProjectiles.EnemyCentre(b) - at).sqrMagnitude));
            return list;
        }

        /// <summary>
        /// A blow of the weapon's on one enemy where it stands (an unseen hitbox there for a tick), carrying r's effects. No
        /// knockback: the remade weapons' extra blows came so often they kept enemies flying (the user's 10-28 "魔法武器频率都很高，导致击退也特别高").
        /// </summary>
        public static void Strike(ItemDef def, HealthManager hm, int damage, RangedShot r = null)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null || hm == null || hm.isDead || def == null) return;
            r = r ?? new RangedShot();
            r.Item = def;
            r.Local = true;
            TerraProjectiles.SpawnClass = def.Class;
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            BladeSkills.DevMageArtShots++;
            try { tp.Shoot(ProjKind.SoulDrainBox, TerraProjectiles.EnemyCentre(hm), Vector2.zero, Mathf.Max(1, damage), 0f, Equipment.Crit(def)); }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
            }
        }

        /// <summary>The weapon's damage now times k.</summary>
        public static int DamageOf(ItemDef def, float k) => Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(def, null) * k));

        // ------------------------------------------------------------------ things

        /// <summary>A thing set going (its cap kept: the oldest of its key goes first when there are `cap` already).</summary>
        public static void Add(MageThing thing, int cap = 0)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null || thing == null) return;
            if (cap > 0 && thing.Key != null)
            {
                var all = tp.DevThings(thing.Key);
                for (int i = 0; i + cap <= all.Count; i++) { all[i].Done = true; }
            }
            tp.SpawnThing(thing);
            DevThings++;
            BladeSkills.DevMageArtShots++;
        }

        public static int Count(string key) => TerraProjectiles.Instance != null ? TerraProjectiles.Instance.DevThings(key).Count : 0;

        public static List<MageThing> Things(string key) => TerraProjectiles.Instance != null ? TerraProjectiles.Instance.DevThings(key) : new List<MageThing>();

        // ------------------------------------------------------------------ dust

        /// <summary>A still dust at a world point (weightless), its index or -1.</summary>
        public static int Dust(Vector2 at, int type, Vector2 velocity, float scale = 1f, int alpha = 0)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return -1;
            var px = TerraParticles.ToPx(at);
            int d = pp.NewDust(px - new Vector2(2f, 2f), 4, 4, type, 0f, 0f, alpha, scale);
            if (d < 0) return -1;
            ref var dd = ref pp[d];
            dd.Position = px;
            dd.Velocity = new Vector2(velocity.x, -velocity.y);
            dd.NoGravity = true;
            return d;
        }

        /// <summary>A ring of dust round a world point (radius world), moving outward at `speed` px a tick.</summary>
        public static void Ring(Vector2 at, float radius, int type, int n, float speed = 0f, float scale = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Dust(at + way * radius, type, way * speed, scale);
            }
        }
    }
}
