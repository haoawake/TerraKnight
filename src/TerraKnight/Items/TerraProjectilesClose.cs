using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The jousting lances remade (the user's 10-26 "继续升级剩下的这些…特效，动画，粒子效果，新颖，多样"), with the remade swings on:
    /// held out 28% further (the lance drawn as much longer, so it still reaches the hand), still as hard as the player is
    /// fast; standing it shakes forward in short jabs (抖枪); running, wind lines stream off it and past seven tiles a second
    /// a cone of them breaks at its point (破风), its metal's motes at the head (the Shadow lance's shadowflame, the Hallowed
    /// one's light), sparks where it strikes. The right-click charge (BladeSkillsClose): the lance couched, then a charge
    /// along the way he faces, the lance out and striking as hard as the charge is fast (冲锋); the Shadow lance's leaves
    /// its afterimages and sets what it strikes alight with shadowflame, the Hallowed one's ends in a burst of light.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>How much further the remade lance reaches (and how much longer it is drawn).</summary>
        const float LanceLength = 1.28f;

        /// <summary>A charge under way: the lance it carries, from and to (world), how fast (Terraria px a tick), its start and length.</summary>
        sealed class LanceDash
        {
            public ItemDef Item;
            public Proj Lance;
            public Vector2 From, To, Offset;
            public float Speed, Start, Seconds, Facing, Charge;
            public bool Ended;
            public readonly List<Vector2> Ghosts = new List<Vector2>();
        }

        static LanceDash lanceDash;

        /// <summary>The wind lines and light streaks about the lances (Terraria px), each a moment.</summary>
        readonly List<(Vector2 A, Vector2 B, float Age, float Life, Color Tint, float Thick)> lanceLines = new List<(Vector2, Vector2, float, float, Color, float)>();

        /// <summary>Dev: charges begun, their enemy hits, the last one's distance (world); stationary jabs; wind lines drawn.</summary>
        public static int DevLanceDashes, DevLanceDashHits, DevLanceJabs, DevLanceWinds;
        public static float DevLanceDashFar;

        static bool LanceRemade => BladeArts.Enabled;

        /// <summary>A right-click charge carrying the player now (the arm along the lance).</summary>
        internal static bool LanceCharging => lanceDash != null && !lanceDash.Ended;

        /// <summary>The lance's reach this update: the remade one's length, its stationary jab on top.</summary>
        float LanceOut(Proj p)
        {
            if (!LanceRemade) return 1f;
            p.Scale = LanceLength;
            if (lanceDash != null && lanceDash.Lance == p) return LanceLength * 1.06f;
            // Standing (under 1.5 px a tick): a jab forward every half second, out in 4 ticks and back in 6.
            if (LanceSpeed() < 1.5f)
            {
                int k = p.Updates % 30;
                if (k == 0) DevLanceJabs++;
                float jab = k < 4 ? k / 4f : k < 10 ? (10 - k) / 6f : 0f;
                return LanceLength * (1f + 0.14f * jab);
            }
            return LanceLength;
        }

        /// <summary>The lance's damage this hit: as hard as the player goes, or as the charge it carries goes.</summary>
        static float LanceScale(Proj p) => lanceDash != null && lanceDash.Lance == p && !lanceDash.Ended
            ? LanceMultiplier(new Vector2(lanceDash.Speed * Player.TerraMovement.PxT, 0f))
            : LanceMultiplier(HeroVelocity());

        static float LanceKnockScale(Proj p) => lanceDash != null && lanceDash.Lance == p ? lanceDash.Speed / 7f : LanceSpeed() / 7f;

        /// <summary>Each update of a held lance (after its own step): the remade one's wind, motes and jab streak.</summary>
        void LanceLook(Proj p, ItemType item)
        {
            if (!LanceRemade || p.Dead) return;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var def = ItemDb.Get(item);
            var art = SwordFx.CloseArtOf(def);
            var dir = TV(p).normalized;
            var head = CenterPx(p);
            bool dashing = lanceDash != null && lanceDash.Lance == p;
            var run = dashing ? new Vector2(lanceDash.Facing, 0f) : (Vector2)hero.GetComponent<Rigidbody2D>().linearVelocity;
            float speed = dashing ? lanceDash.Speed : LanceSpeed();
            var runPx = run.sqrMagnitude > 1e-6f ? Flip(run.normalized) : new Vector2(p.Dir, 0f);
            float along = Vector2.Dot(dir, runPx);
            var across = new Vector2(-dir.y, dir.x);
            if (speed > 1.8f && along > 0.4f)
            {
                // Wind lines streaming off the lance and past the player.
                float rate = Mathf.Clamp01((speed - 1.8f) / 5f);
                for (int w = 0; w < 2; w++)
                {
                    if (Random.value > 0.55f + 0.45f * rate) continue;
                    var a = head - dir * Random.Range(0f, 90f * LanceLength) + across * Random.Range(-20f, 20f);
                    var b = a - runPx * (32f + speed * 8f);
                    lanceLines.Add((a, b, 0f, 0.24f, Color.Lerp(new Color(0.88f, 0.93f, 1f), art.Light, 0.3f), 5f + rate * 4f));
                    DevLanceWinds++;
                }
                // Wisps of air peeled off the head.
                if (Random.value < 0.3f + 0.4f * rate) BoxDust(head - new Vector2(6f, 6f), 12, 12, 16, -runPx.x * (2f + speed * 0.4f), -runPx.y * 2f, 100, 1.1f);
                // Past six: the air breaks at its point.
                if (speed > 6f && Random.value < 0.7f)
                    foreach (float s in new[] { -1f, 1f })
                        foreach (float deg in new[] { 18f, 32f })
                        {
                            var side = Rotate(-dir, s * deg);
                            lanceLines.Add((head + dir * 6f, head + side * (40f + speed * 3f), 0f, 0.16f, Color.Lerp(Color.white, art.Light, 0.25f), 7f - deg * 0.08f));
                        }
                if (Random.value < 0.6f) SwordFx.SpearMote(art, head - dir * Random.value * 16f, -runPx * 1.5f, p.Dir);
                if (art.Theme == BladeArts.Theme.Steel && Random.value < 0.2f) BoxDust(head - new Vector2(4f, 4f), 8, 8, 16, -runPx.x * 2f, -runPx.y * 2f, 120, 1f);
            }
            else if (Random.value < 0.12f) SwordFx.SpearMote(art, head - dir * Random.value * 10f, dir * 0.4f, p.Dir);
            // The jab's line along the lance as it shakes out.
            if (LanceSpeed() < 1.5f && !dashing && p.Updates % 30 == 1)
                lanceLines.Add((head - dir * 40f, head + dir * 6f, 0f, 0.12f, art.Light, 5f));
        }

        /// <summary>Where the remade lance strikes: sparks off its point and its theme's burst; the Shadow lance's charge sets it alight.</summary>
        void LanceHit(Proj p, HealthManager hm, ItemType item)
        {
            if (!LanceRemade || hm == null) return;
            var def = ItemDb.Get(item);
            var at = CenterPx(p);
            SwordFx.SpearHit(SwordFx.CloseArtOf(def), at, p.Dir);
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(at - new Vector2(3f, 3f), 6, 6, 278, p.Dir * Random.Range(1f, 4f), Random.Range(-4f, -1f), 0, 0.55f);
                if (i >= 0) { D(i).Color = new Color(1f, 0.85f, 0.5f); D(i).FadeIn = 0.4f; }
            }
            if (lanceDash != null && lanceDash.Lance == p)
            {
                DevLanceDashHits++;
                if (item == ItemType.ShadowJoustingLance) EnemyDebuffs.Add(hm, EnemyDebuffs.Shadowflame, 180);
            }
        }

        // ------------------------------------------------------------------ the charge (the right-click art)

        /// <summary>The couched charge let go: along the way he faces, 7 to 17 tiles (a wall nearer), the lance out all the way.</summary>
        internal bool LanceCharge(ItemDef def, HeroController hero, float charge)
        {
            if (def == null || hero == null || def.Shoot == ProjKind.None || lanceDash != null) return false;
            foreach (var q in live) if (!q.Dead && IsLance(q.Kind)) Kill(q);
            float facing = hero.cState.facingRight ? 1f : -1f;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            float speed = 12f + 6f * charge;
            float seconds = 0.16f + 0.1f * charge;
            float far = speed * 60f * seconds * TerraParticles.Px;
            var way = new Vector2(facing, 0f);
            var hit = Physics2D.BoxCast(b.center, b.size * 0.9f, 0f, way, far, (1 << 8) | (1 << World.ChunkColliders.TerrainLayer));
            if (hit.collider != null) far = Mathf.Max(0f, hit.distance - 0.05f);
            seconds = Mathf.Max(0.06f, far / (speed * 60f * TerraParticles.Px));
            float scale = Equipment.UseScale(def);
            SpawnClass = DamageClass.Melee;
            var p = Spawn(def.Shoot, b.center, new Vector2(facing, 0f) * (def.ShootSpeed / scale), Equipment.Damage(def), Equipment.Knockback(def), Equipment.Crit(def), 0,
                Mathf.Max(3, Mathf.RoundToInt(def.UseAnimation * 60f)));
            SpawnClass = DamageClass.None;
            if (p == null) return false;
            lanceDash = new LanceDash
            {
                Item = def, Lance = p, From = b.center, To = (Vector2)b.center + way * far, Offset = (Vector2)hero.transform.position - (Vector2)b.center,
                Speed = speed, Start = Time.time, Seconds = seconds, Facing = facing, Charge = charge,
            };
            Player.TerraHurt.Invulnerable(hero, Mathf.RoundToInt(seconds * 60f) + 20);
            TerraMod.Instance?.PlaySound("Item_1", 0.8f);
            TerraMod.Instance?.PlaySound("Item_74", 0.4f);
            DevLanceDashes++;
            DevLanceDashFar = far;
            return true;
        }

        /// <summary>Each frame: the charging player carried along its way (untouchable), its afterimages laid; at its end the lance put up.</summary>
        void LanceDashFrame()
        {
            var d = lanceDash;
            if (d == null) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || d.Lance == null || d.Lance.Dead && !d.Ended) { lanceDash = null; return; }
            var rb = hero.GetComponent<Rigidbody2D>();
            float t = (Time.time - d.Start) / d.Seconds;
            if (!d.Ended)
            {
                var at = Vector2.Lerp(d.From, d.To, Mathf.Clamp01(t)) + d.Offset;
                if (rb != null)
                {
                    rb.position = at;
                    rb.linearVelocity = new Vector2(d.Facing * d.Speed * Player.TerraMovement.PxT, 0f);
                }
                hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                if (d.Ghosts.Count == 0 || Vector2.Distance(d.Ghosts[d.Ghosts.Count - 1], at) > 0.5f) d.Ghosts.Add(at);
                if (t >= 1f)
                {
                    d.Ended = true;
                    if (rb != null) rb.linearVelocity = new Vector2(d.Facing * 4f * Player.TerraMovement.PxT, 0f);
                    LanceDashEnd(d, hero);
                }
            }
            // The lance put up a moment after the charge.
            if (d.Ended && Time.time - d.Start > d.Seconds + 0.18f)
            {
                if (d.Lance != null && !d.Lance.Dead) Kill(d.Lance);
                lanceDash = null;
            }
        }

        /// <summary>The charge's end: the Hallowed lance's burst of light round where it stopped.</summary>
        void LanceDashEnd(LanceDash d, HeroController hero)
        {
            if (d.Item.Type != ItemType.HallowJoustingLance) return;
            var centre = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            var mod = TerraMod.Instance;
            var px = TerraParticles.ToPx(centre);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f + Random.value * 0.3f;
                var w = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                lanceLines.Add((px + w * 10f, px + w * (40f + Random.value * 22f), 0f, 0.28f, new Color(1f, 0.92f, 0.6f), 6f));
                int i = BoxDust(px - new Vector2(4f, 4f), 8, 8, 57, w.x * 4f, w.y * 4f, 100, 1.3f);
                if (i >= 0) D(i).NoGravity = true;
            }
            if (mod == null) return;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(d.Item) * (1f + d.Charge)));
            var done = new HashSet<HealthManager>();
            foreach (var col in Physics2D.OverlapCircleAll(centre, 3f * 16f * TerraParticles.Px, MeleeHits.HitMask))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !done.Add(hm)) continue;
                if (mod.Melee.HitWith(col, damage, 6f, Equipment.Crit(d.Item), col.bounds.center.x < centre.x ? -1f : 1f, centre)) DevLanceDashHits++;
            }
            mod.PlaySound("Item_29", 0.5f);
        }

        /// <summary>The remade lance's extras over and under it: the charge's afterimages, the wind lines and streaks.</summary>
        void DrawLanceArt(FxBatch fx, Proj p, ItemType item)
        {
            if (!LanceRemade) return;
            var d = lanceDash;
            if (d != null && d.Lance == p && d.Ghosts.Count > 1)
            {
                // The charge's afterimages: the lance where the player was, fading back along the way.
                var art = SwordFx.CloseArtOf(d.Item);
                var tint = item == ItemType.ShadowJoustingLance ? new Color(0.55f, 0.25f, 0.85f) : BladeArts.Tint(art, art.Light, 0f);
                var whole = Sprite(p.Spec.Texture).rect;
                var spear = FxBatch.Frame(p.Spec.Texture, 0, 0, (int)whole.width, (int)whole.height, 0f, 0f);
                var v = Flip(p.Vel);
                bool right = p.Dir > 0f;
                float rot = Mathf.Atan2(v.y, v.x) + 2.355f;
                if (right) rot -= Mathf.PI / 2f;
                var now = d.Ghosts[d.Ghosts.Count - 1];
                var headOff = CenterPx(p) - TerraParticles.ToPx(now);
                int n = d.Ghosts.Count;
                for (int k = Mathf.Max(0, n - 6); k < n - 1; k++)
                {
                    float g = (k - (n - 6f)) / 6f;
                    var c = new Color(tint.r, tint.g, tint.b, 0f) * (0.45f * g);
                    fx.Draw(spear, TerraParticles.ToPx(d.Ghosts[k]) + headOff, c, rot, p.Scale, flipX: right, overPlayer: true);
                }
            }
        }

        /// <summary>The wind lines and streaks, aging (each frame, drawn with the shots).</summary>
        void DrawLanceLines(FxBatch fx)
        {
            if (lanceLines.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = lanceLines.Count - 1; i >= 0; i--)
            {
                var l = lanceLines[i];
                l.Age += dt;
                if (l.Age >= l.Life) { lanceLines.RemoveAt(i); continue; }
                lanceLines[i] = l;
                float f = 1f - l.Age / l.Life;
                // Drawn out from its start, thinning as it goes.
                var b = Vector2.Lerp(l.A, l.B, Mathf.Min(1f, l.Age / (l.Life * 0.35f)));
                StreakPx(fx, l.A, b, l.Tint, l.Thick * (0.4f + 0.6f * f), 0.75f * f);
            }
        }

    }
}
