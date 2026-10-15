using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Old One's Army sentries (summon-hm sec-F; Terraria px, y down, one update a tick): the
    /// Flameburst towers (AI_130, their shots in TerraProjectilesSentryShots.cs), the Ballistas
    /// (AI_134), the Explosive Traps and their blasts (AI_138 / AI_139) and the Lightning Auras
    /// (AI_137). They stand on the ground under the cursor and fall with no cap but 16 px.
    /// </summary>
    internal sealed partial class Minions
    {
        static bool IsFlameburst(Kind k) => k >= Kind.Flameburst1 && k <= Kind.Flameburst3;
        static bool IsBallista(Kind k) => k >= Kind.Ballista1 && k <= Kind.Ballista3;
        static bool IsTrap(Kind k) => k >= Kind.Trap1 && k <= Kind.Trap3;
        static bool IsTrapBlast(Kind k) => k >= Kind.TrapBlast1 && k <= Kind.TrapBlast3;
        static bool IsAura(Kind k) => k >= Kind.Aura1 && k <= Kind.Aura3;

        /// <summary>
        /// Pl:49567-49604 with FindSentryRestingSpot's offsets (F.0.3): the sentry's centre so its
        /// bottom stands on the ground (the Flameburst T1, Ballista T1/T2 and traps a pixel toward
        /// the player's facing), with its spawn state.
        /// </summary>
        int lastMaxTurrets = 1;

        /// <summary>
        /// Main.TryDespawningProjectile (M:38531-38605): the cursor on an own sentry (its box at
        /// least 20 px square) whose nearest point is within the player's reach (5 tiles across,
        /// 3 up and down past the player's box): it goes, no item back.
        /// </summary>
        public bool TryDespawnSentry(Vector2 mouseWorld)
        {
            RefreshOwner();
            if (!hasOwner) return false;
            var mouse = TerraParticles.ToPx(mouseWorld);
            var reach = new Rect(pBox.x - 80f, pBox.y - 48f, pBox.width + 160f, pBox.height + 96f);
            foreach (var m in live)
            {
                if (m.Dead || !m.S.Sentry) continue;
                var box = m.Box;
                var near = new Vector2(Mathf.Clamp(pCenter.x, box.xMin, box.xMax), Mathf.Clamp(pCenter.y, box.yMin, box.yMax));
                if (!reach.Contains(near)) continue;
                float w = Mathf.Max(box.width, 20f), h = Mathf.Max(box.height, 20f);
                if (!new Rect(box.center.x - w / 2f, box.center.y - h / 2f, w, h).Contains(mouse)) continue;
                Kill(m);
                return true;
            }
            return false;
        }

        /// <summary>The death effects of the sentries and their children (P:77095, 76912, 75739, 73323-73347).</summary>
        void SentryDeath(M m)
        {
            switch (m.Kind)
            {
                case Kind.SpiderEgg:
                    HatchEgg(m);
                    break;
                case Kind.SpiderHiver:
                case Kind.BabySpider:
                    for (int k = 0; k < 5; k++)
                    {
                        int i = DustBox(m, 171, Vector2.zero, 100, Random.Range(1, 10) * 0.1f);
                        if (i < 0) continue;
                        TerraParticles.Instance[i].NoGravity = true;
                        TerraParticles.Instance[i].FadeIn = 1.5f;
                        TerraParticles.Instance[i].Velocity *= 0.75f;
                    }
                    break;
                case Kind.FrostHydra:
                    Sound("Item_27");
                    for (int k = 0; k < 80; k++)
                    {
                        int i = DustAt(new Vector2(m.Pos.x, m.Pos.y + 16f), m.W, m.H - 16, 185, Vector2.zero, 0, 1f);
                        if (i < 0) continue;
                        TerraParticles.Instance[i].Velocity *= 2f;
                        TerraParticles.Instance[i].NoGravity = true;
                        TerraParticles.Instance[i].Scale *= 1.15f;
                    }
                    break;
                case Kind.CrystalBlast:
                    CrystalExplode(m);
                    break;
                case Kind.LunarPortal:
                case Kind.RainbowCrystal:
                    // Its lasers or blasts go with it (the blasts go off as they do).
                    foreach (var c in live)
                        if (!c.Dead && c.Leader == m && (c.Kind == Kind.PortalLaser || c.Kind == Kind.CrystalBlast)) Kill(c);
                    break;
            }
        }

        void SpawnSentry(ItemDef staff, Vector2 ground)
        {
            float kb = Equipment.Knockback(staff);
            int dmg = staff.Damage;
            Kind kind;
            float up = 27f, dx = 0f;
            switch (staff.Minion)
            {
                case MinionKind.Flameburst1: kind = Kind.Flameburst1; dx = pDir; break;
                case MinionKind.Flameburst2: kind = Kind.Flameburst2; up = 29f; break;
                case MinionKind.Flameburst3: kind = Kind.Flameburst3; up = 30f; break;
                case MinionKind.Ballista1: kind = Kind.Ballista1; dx = pDir; break;
                case MinionKind.Ballista2: kind = Kind.Ballista2; dx = pDir; break;
                case MinionKind.Ballista3: kind = Kind.Ballista3; break;
                case MinionKind.Trap1: kind = Kind.Trap1; up = 8f; dx = pDir; break;
                case MinionKind.Trap2: kind = Kind.Trap2; up = 8f; dx = pDir; break;
                case MinionKind.Trap3: kind = Kind.Trap3; up = 8f; dx = pDir; break;
                case MinionKind.Aura1: kind = Kind.Aura1; break;
                case MinionKind.Aura2: kind = Kind.Aura2; break;
                case MinionKind.Aura3: kind = Kind.Aura3; break;
                default: return;
            }
            var m = Spawn(kind, staff, ground + new Vector2(dx, -up), dmg, kb);
            m.Vel = Vector2.zero;
            if (IsFlameburst(kind))
            {
                // ai0 = 1 skips the tower's "face the owner" start (F.6 #1): it faces the player's way.
                m.Ai0 = 1f;
                m.Ai1 = kind == Kind.Flameburst1 ? 80f : kind == Kind.Flameburst2 ? 70f : 60f;
                m.Dir = m.SpriteDir = pDir;
            }
            else if (IsBallista(kind)) m.Ai1 = BallistaDelay();
            else if (IsTrap(kind)) m.LocalAi0 = TrapCooldown();
        }

        static OoaSet Ooa => Equipment.Current.Ooa;

        /// <summary>Dev: the Ballistas' reload now.</summary>
        public static float DevBallistaDelay => BallistaDelay();

        /// <summary>
        /// GetBallistraShotDelay (P:69724-69740): 160 ticks; 100 with the Squire's tier 3 set; 60 in Ballista Panic
        /// (the Squire's sets, for 5 s after a hit); 30 both.
        /// </summary>
        static float BallistaDelay()
        {
            bool panic = Player.PlayerBuffs.Has(Player.PotionBuffs.BallistaPanic), t3 = (Ooa & OoaSet.SquireT3) != 0;
            return panic && t3 ? 30f : panic ? 60f : t3 ? 100f : 160f;
        }

        /// <summary>GetExplosiveTrapCooldown (P:70453-70465): 90 ticks, 60 with the Huntress's tier 2 set, 30 with tier 3.</summary>
        static float TrapCooldown() => (Ooa & OoaSet.HuntressT3) != 0 ? 30f : (Ooa & OoaSet.HuntressT2) != 0 ? 60f : 90f;

        int slingCooldown;

        /// <summary>The sentries' own attacks (sentry shots): the trap's blast, the portal's laser, the crystal's blasts, the hive's eggs and spiders.</summary>
        static bool IsSentryChild(Kind k) => IsTrapBlast(k) || k == Kind.PortalLaser || k == Kind.CrystalBlast || k == Kind.SpiderEgg || k == Kind.BabySpider;

        /// <summary>
        /// The sentry slings (accSentryBackpack; P:80533-80558, 70097-70375): a sentry the player
        /// touches while wearing one is picked up for good, one at a time (the carried one keeps
        /// the grab cooldown up); it rides on the player, still shooting, and dies when the sling
        /// comes off. Where it sits by its kind; an idle tower faces the player's way, the
        /// barnacle hangs ready.
        /// </summary>
        void Sling(M m)
        {
            bool sling = Equipment.Current.SentryBackpack;
            if (m.Carried && !sling)
            {
                Kill(m);
                return;
            }
            if (!m.Carried)
            {
                if (!sling || slingCooldown > 0 || !m.Box.Overlaps(pBox)) return;
                m.Carried = true;
                DevSlung++;
            }
            slingCooldown = 2;
            var c = pCenter;
            // The headgear bob of the player's body frame (Main.OffsetsPlayerHeadgear + (0, -2)).
            float bob = Player.TerraPlayerSprite.BobPx;
            if (m.Kind == Kind.LunarPortal || m.Kind == Kind.RainbowCrystal) m.Center = c + new Vector2(0f, -(21f + 20f + m.H / 2f));
            else if (IsAura(m.Kind)) m.Center = c;
            else if (m.Kind == Kind.Houndius) m.Center = c + new Vector2(0f, -21f);
            else
            {
                var v = Vector2.zero;
                if (m.Kind == Kind.FrostHydra)
                {
                    if (m.SpriteDir == 1) v.x = -14f;
                    v += new Vector2(10f * m.SpriteDir, -20f);
                }
                else if (m.Kind == Kind.SpiderHiver) v = new Vector2(m.SpriteDir, -20f);
                else if (m.Kind == Kind.Barnacle)
                {
                    v = new Vector2(pDir * -25f, 4f);
                    if (pDir == -1) v.x += pDir * 4f;
                    m.Rot = pDir == 1 ? Mathf.PI / 2f : -Mathf.PI / 2f;
                }
                else if (IsBallista(m.Kind)) v = new Vector2(0f, -14f);
                else if (IsFlameburst(m.Kind))
                {
                    v = new Vector2(pDir * 10f, -11f);
                    if (m.Dir != pDir) v.x += m.Dir * -2f;
                }
                else if (IsTrap(m.Kind))
                {
                    v = new Vector2(pDir * -9f, pDir == -1 ? 2f : 0f);
                    m.Rot = pDir == 1 ? 4.712389f : Mathf.PI / 2f;
                }
                m.Center = c + v + new Vector2(0f, bob);
            }
            m.Vel = Vector2.zero;
            m.TileCollide = false;
            m.GfxOffY = 0f;
            // AI_053 idle (P:37995-38012) and AI_130 reloaded (P:69349-69360) face the player's way; AI_197 acts attached (P:41067-41072).
            if ((m.Kind == Kind.FrostHydra || m.Kind == Kind.SpiderHiver || m.Kind == Kind.Houndius) && m.Ai0 <= 0f) m.Dir = m.SpriteDir = pDir;
            if (IsFlameburst(m.Kind) && m.Ai0 == 1f) m.Dir = m.SpriteDir = pDir;
            if (m.Kind == Kind.Barnacle)
            {
                m.Ai0 = 1f;
                if (m.Frame < 0 || m.Frame > 2) m.Frame = Random.Range(0, 3);
            }
        }

        /// <summary>Dev: sentries picked up by a sling.</summary>
        public static int DevSlung;

        /// <summary>Every sentry's AI ends so (P:69415): it falls, on blocks and platforms.</summary>
        static void SentryFall(M m)
        {
            m.TileCollide = true;
            m.FallThrough = false;
            m.Vel.x = 0f;
            m.Vel.y = Mathf.Min(m.Vel.y + 0.2f, 16f);
        }

        /// <summary>
        /// HandleMovement for a sentry (P:20052-20061): blocks meet a 16 px wide box of its full
        /// height (the aura: a 16 px box whose bottom is its centre, the ground point).
        /// </summary>
        void SentryCollision(M m)
        {
            var pos = m.Pos;
            int w = m.W, h = m.H;
            var c = m.Center;
            m.W = m.Kind == Kind.FrostHydra ? 26 : 16;
            if (IsAura(m.Kind))
            {
                m.H = 16;
                m.Pos = new Vector2(c.x - 8f, c.y - 16f);
            }
            else m.Pos = new Vector2(c.x - 8f, pos.y);
            var before = m.Pos;
            TileCollision(m, false);
            var moved = m.Pos - before;
            m.W = w;
            m.H = h;
            m.Pos = pos + moved;
        }

        /// <summary>
        /// The towers' target search (P:69419-69511): the owner's target if it passes, else the
        /// enemy nearest the shooting spot, within 900 px of it, passing the angle rule and in a
        /// clear line from the spot. No screen rule.
        /// </summary>
        Npc SentryTarget(Vector2 spot, System.Func<Npc, Vector2, bool> angleOk, float range = 900f)
        {
            bool Ok(Npc n)
            {
                var d = n.Center - spot;
                if (d.magnitude > range) return false;
                var u = d.sqrMagnitude > 0f ? d.normalized : Vector2.up;
                return angleOk(n, u) && ClearLine(spot, n.Center);
            }
            var o = OwnerTarget;
            if (o != null && Ok(o)) return o;
            Npc best = null;
            float bestD = float.MaxValue;
            foreach (var n in npcs)
            {
                float d = Vector2.Distance(spot, n.Center);
                if (d >= bestD || !Ok(n)) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        /// <summary>Utils.AngleLerp: the short way round, kept in (−π, π].</summary>
        static float AngleLerp(float from, float to, float t)
        {
            float r = from + Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad * t;
            while (r > Mathf.PI) r -= Mathf.PI * 2f;
            while (r <= -Mathf.PI) r += Mathf.PI * 2f;
            return r;
        }

        // ------------------------------------------------------------------ 663/665/667: Flameburst (AI_130)

        /// <summary>
        /// AI_130: reloading (80/70/60 ticks), then it looks for an enemy within ±45° of level and
        /// turns to it; the volley (6 or 8 frames of 4 ticks) lets its fireball go on tick 12, aimed
        /// at an enemy still in front, else straight ahead.
        /// </summary>
        void AI130(M m)
        {
            int tier = m.Kind - Kind.Flameburst1;
            // The Apprentice's sets (P:69332-69337): half as far again, nearly any angle (angleRatioMax 0.1), 1.4 times as fast.
            bool t2 = (Ooa & OoaSet.ApprenticeT2) != 0;
            float range = t2 ? 1350f : 900f, ratio = t2 ? 0.1f : 1f;
            var bottom = new Vector2(m.Center.x, m.Bottom);
            var spot = bottom + new Vector2(m.Dir * 6f, tier == 0 ? -40f : tier == 1 ? -44f : -46f);
            if (++m.LocalAi0 >= 300f) m.LocalAi0 = 0f;
            FlameburstDust(m, tier);
            if (m.Ai0 == 1f)
            {
                m.Frame = 0;
                bool ready = false;
                if (m.Ai1 > 0f) m.Ai1--;
                else ready = true;
                if (ready)
                {
                    var t = SentryTarget(spot, (n, u) => Mathf.Abs(u.x) >= Mathf.Abs(u.y) * ratio, range);
                    if (t != null)
                    {
                        float dx = t.Center.x - m.Center.x;
                        if (dx != 0f) m.Dir = dx < 0f ? -1 : 1;
                        m.Ai0 = 2f;
                        m.Ai1 = 0f;
                    }
                }
            }
            else if (m.Ai0 == 2f)
            {
                m.Frame = 1 + (int)(m.Ai1 / 4f);
                if (m.Ai1 == 12f)
                {
                    var aim = new Vector2(m.Dir, 0f);
                    var t = SentryTarget(spot, (n, u) => Mathf.Abs(u.x) >= Mathf.Abs(u.y) * ratio && !(m.Dir * u.x < 0f), range);
                    if (t != null && (t.Center - spot).sqrMagnitude > 0f) aim = (t.Center - spot).normalized;
                    float speed = (tier == 0 ? 12f : tier == 1 ? 15f : 18f) * (t2 ? 1.4f : 1f);
                    var v = aim * speed;
                    TerraProjectiles.Instance?.ShootMinion((ProjKind)((int)ProjKind.FlameburstShot1 + tier), TerraParticles.ToWorld(spot), new Vector2(v.x, -v.y), m.Damage, m.Knockback);
                }
                if (++m.Ai1 >= (tier == 0 ? 6 : 8) * 4)
                {
                    m.Ai0 = 1f;
                    m.Ai1 = tier == 0 ? 80f : tier == 1 ? 70f : 60f;
                }
            }
            m.SpriteDir = m.Dir;
            SentryFall(m);
        }

        /// <summary>P:69188-69224: embers (270) drifting off its foot, and the flame (6) in its head (one, two or three rolls).</summary>
        static void FlameburstDust(M m, int tier)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            int q = m.W / 4;
            if (Random.Range(0, 2) == 0)
            {
                float rx = m.Pos.x + q, rw = tier == 0 ? q * 3 : q * 2;
                if (m.Dir == 1) rx -= q;
                int i = p.NewDust(new Vector2(rx - 2f, m.Pos.y + m.H - 16f - 2f), (int)rw + 4, 10, 270, -m.Dir * 2f, -2f, 200);
                if (i >= 0)
                {
                    p[i].Color = new Color(1f, 1f, 1f, 0f);
                    p[i].FadeIn = 0.6f + Random.value * 0.6f;
                    p[i].Scale = 0.4f;
                    p[i].NoGravity = true;
                    p[i].Velocity = new Vector2(-m.Dir * Random.value * p[i].FadeIn, 0f);
                }
            }
            float hx = m.Center.x + (m.Dir == -1 ? -q : 0) + 4f * m.Dir, hy = m.Bottom - (m.H - 10);
            for (int k = 0; k <= tier; k++)
            {
                if (Random.Range(0, 5) != 0) continue;
                int i = p.NewDust(new Vector2(hx, hy), q, 10, 6);
                if (i < 0) continue;
                p[i].FadeIn = tier == 2 ? 1.1f : 1f;
                p[i].Scale = 1f;
                p[i].NoGravity = true;
                p[i].Velocity *= tier == 2 ? 2.4f : 2f;
            }
        }

        /// <summary>M:34125-34133, 34474-34492: column 0 of the sheet on its bottom (4 px into the ground), and four pulsing glow copies.</summary>
        static void DrawFlameburst(FxBatch fx, M m)
        {
            int tier = m.Kind - Kind.Flameburst1;
            int fw = tier == 0 ? 74 : tier == 1 ? 96 : 92, fh = tier == 2 ? 68 : 66;
            string tex = "Projectile_" + (663 + tier * 2);
            var pos = new Vector2(m.Center.x, m.Bottom + m.GfxOffY);
            bool flip = m.SpriteDir == -1;
            // Carried on a sling: the sheet's second column (M:34123-34133).
            int col = m.Carried ? fw : 0;
            fx.Draw(FxBatch.Frame(tex, col, fh * m.Frame, fw, fh, fw / 2f, fh - 4f), pos, Lit, 0f, 1f, flipX: flip);
            float k = Mathf.Cos(m.LocalAi0 / 100f * Mathf.PI * 2f) + 1f;
            var glow = new Color(140f / 255f, 100f / 255f, 40f / 255f, 0f) * (k / 4f + 0.5f);
            var g = FxBatch.Frame("Glow_" + (221 + tier), col, fh * m.Frame, fw, fh, fw / 2f, fh - 4f);
            for (int i = 0; i < 4; i++)
                fx.Draw(g, pos + new Vector2(Mathf.Cos(i * Mathf.PI / 2f), Mathf.Sin(i * Mathf.PI / 2f)) * k, glow, 0f, 1f, flipX: flip);
            float l = tier == 0 ? 1f : tier == 1 ? 1.2f : 1.5f;
            fx.Light(m.Center, new Color(0.4f, 0.2f, 0.1f) * l);
            fx.Light(new Vector2(m.Center.x, m.Bottom - 10f), new Color(0.4f, 0.2f, 0.1f) * l);
        }

        // ------------------------------------------------------------------ 677-679: Ballista (AI_134)

        /// <summary>
        /// AI_134: the crossbow turns toward the enemy nearest its centre (0.08 a tick; blind in a
        /// ±36.87° cone straight down) and relaxes to level without one; reloaded (160 ticks) and
        /// with a target, a 25-tick volley looses a bolt on its fifth tick, snapped onto the target.
        /// </summary>
        void AI134(M m)
        {
            int tier = m.Kind - Kind.Ballista1;
            var spot = m.Center;
            if (tier == 0) spot.y -= 4f;
            if (m.Ai0 == 0f)
            {
                m.Dir = m.SpriteDir = pDir;
                m.Ai0 = 1f;
                m.Ai1 = BallistaDelay();
                if (m.Dir == -1) m.Rot = Mathf.PI;
            }
            bool AngleOk(Npc n, Vector2 u) => !(Mathf.Abs(u.x) < Mathf.Abs(u.y) * 0.75f && u.y > 0f);
            if (m.Ai0 == 1f)
            {
                m.Frame = 0;
                bool ready = false;
                // Ballista Panic cuts the wait left (P:69632-69639).
                m.Ai1 = Mathf.Min(m.Ai1, BallistaDelay());
                if (m.Ai1 > 0f) m.Ai1--;
                else ready = true;
                var t = SentryTarget(spot, AngleOk);
                if (t != null)
                {
                    var v = (t.Center - spot).sqrMagnitude > 0f ? (t.Center - spot).normalized : Vector2.up;
                    m.Rot = AngleLerp(m.Rot, Mathf.Atan2(v.y, v.x), 0.08f);
                    m.Dir = m.Rot > Mathf.PI / 2f || m.Rot < -Mathf.PI / 2f ? -1 : 1;
                    if (ready)
                    {
                        if (v.x != 0f) m.Dir = v.x < 0f ? -1 : 1;
                        m.Ai0 = 2f;
                        m.Ai1 = 0f;
                    }
                }
                else m.Rot = AngleLerp(m.Rot, m.Dir == -1 ? Mathf.PI : 0f, 0.05f);
            }
            else if (m.Ai0 == 2f)
            {
                m.Frame = 1 + (int)(m.Ai1 / 5f);
                if (m.Ai1 == 5f)
                {
                    Sound("Custom/dd2_ballista_tower_shot_" + Random.Range(0, 3));
                    var aim = new Vector2(m.Dir, 0f);
                    var t = SentryTarget(spot, AngleOk);
                    if (t != null && (t.Center - spot).sqrMagnitude > 0f) aim = (t.Center - spot).normalized;
                    m.Rot = Mathf.Atan2(aim.y, aim.x);
                    m.Dir = m.Rot > Mathf.PI / 2f || m.Rot < -Mathf.PI / 2f ? -1 : 1;
                    // The Squire's sets (P:69589-69592, 11083-11086): the bolt at 21 instead of 16, through 7.
                    bool squire = (Ooa & OoaSet.SquireT2) != 0;
                    var v = aim * (squire ? 21f : 16f);
                    TerraProjectiles.Instance?.ShootMinion(ProjKind.BallistaBolt, TerraParticles.ToWorld(spot), new Vector2(v.x, -v.y), m.Damage, m.Knockback, squire ? 7 : 0);
                }
                if (++m.Ai1 >= 25f)
                {
                    m.Ai0 = 1f;
                    m.Ai1 = BallistaDelay();
                }
            }
            m.SpriteDir = m.Dir;
            SentryFall(m);
        }

        /// <summary>
        /// M:34145-34187: the base (never mirrored) on its bottom, then the crossbow turned about its
        /// pivot, flipped upside down while it faces left (the pivot texel stays put).
        /// </summary>
        static void DrawBallista(FxBatch fx, M m)
        {
            int tier = m.Kind - Kind.Ballista1;
            int bw = tier == 0 ? 30 : tier == 1 ? 38 : 32;
            fx.Draw(FxBatch.Frame("Extra_" + (83 + tier), 0, 0, bw, 30, bw / 2f, 28f), new Vector2(m.Center.x, m.Bottom + m.GfxOffY), Lit, 0f, 1f);
            int hw = tier == 0 ? 124 : tier == 1 ? 140 : 142;
            var pivot = tier == 0 ? new Vector2(61f, 28f) : tier == 1 ? new Vector2(69f, 28f) : new Vector2(66f, 32f);
            float lift = tier == 0 ? 3f : tier == 1 ? -1f : 1f;
            var head = FxBatch.Frame("Projectile_" + (677 + tier), 0, 56 * m.Frame, hw, 56, pivot.x, pivot.y);
            fx.Draw(head, m.Center + new Vector2(0f, lift + m.GfxOffY), Lit, m.Rot, 1f, flipY: m.SpriteDir == -1);
        }

        // ------------------------------------------------------------------ 691-693: Explosive Trap (AI_138), 694-696 its blast (AI_139)

        /// <summary>
        /// AI_138: armed 90 ticks after placing, then every 3 ticks it looks for any enemy in the
        /// 144 px box 48 px over it (through blocks); one there: the blast, and 90 ticks to re-arm.
        /// </summary>
        void AI138(M m)
        {
            int tier = m.Kind - Kind.Trap1;
            m.Dir = m.SpriteDir = 1;
            if (++m.FrameCounter >= 12)
            {
                m.FrameCounter = 0;
                m.Frame = (m.Frame + 1) % 4;
            }
            if (m.LocalAi0 > 0f) m.LocalAi0--;
            if (m.LocalAi0 <= 0f)
            {
                m.LocalAi0 = 3f;
                var c = m.Center + new Vector2(0f, -48f);
                var box = new Rect((int)(c.x - 72f), (int)(c.y - 72f), 144f, 144f);
                foreach (var n in npcs)
                {
                    if (!n.Box.Overlaps(box)) continue;
                    Sound("Custom/dd2_explosive_trap_explode_" + Random.Range(0, 3));
                    m.LocalAi0 = TrapCooldown();
                    var b = Spawn((Kind)((int)Kind.TrapBlast1 + tier), null, c, m.Damage, m.Knockback);
                    b.Damage = m.Damage;
                    b.TileCollide = false;
                    b.Alpha = 1f;
                    b.Dir = b.SpriteDir = 1;
                    b.TimeLeft = 60;
                    break;
                }
            }
            SentryFall(m);
        }

        /// <summary>AI_139: fades in (25 a tick), frames of 3 ticks, gone after its last; hits each enemy in its 144 px box once.</summary>
        void AI139(M m)
        {
            m.TileCollide = false;
            m.Vel = Vector2.zero;
            m.Alpha = Mathf.Max(0f, m.Alpha - 25f / 255f);
            if (++m.FrameCounter >= 3)
            {
                m.FrameCounter = 0;
                if (++m.Frame >= m.S.Frames) Kill(m);
            }
        }

        /// <summary>M:34137-34143, 34555-34568: the trap on its bottom (4 px into the ground), lit 40% toward white, and its glow at half strength.</summary>
        static void DrawTrap(FxBatch fx, M m)
        {
            int tier = m.Kind - Kind.Trap1;
            int w = tier == 0 ? 26 : tier == 1 ? 34 : 46, h = tier == 2 ? 18 : 14;
            var pos = new Vector2(m.Center.x, m.Bottom + m.GfxOffY);
            // Carried on a sling: turned on its side about its middle (M:34135-34143).
            float oy = m.Carried ? h / 2f : h - 4f;
            if (m.Carried) pos = m.Center;
            fx.Draw(FxBatch.Frame("Projectile_" + (691 + tier), 0, h * m.Frame, w, h, w / 2f, oy), pos, Color.Lerp(Lit, Color.white, 0.4f), m.Carried ? m.Rot : 0f, 1f);
            fx.Draw(FxBatch.Frame("Glow_" + (235 + tier), 0, h * m.Frame, w, h, w / 2f, oy), pos, new Color(1f, 1f, 1f, 0.5f), m.Carried ? m.Rot : 0f, 1f);
            fx.Light(m.Center, new Color(0.6f, 0.5f, 0.3f));
        }

        /// <summary>M:32564-32580: the blast on its bottom (8 px lower), with a soft copy 10% bigger at 30%; an orange column of light.</summary>
        static void DrawTrapBlast(FxBatch fx, M m)
        {
            int tier = m.Kind - Kind.TrapBlast1;
            int w = tier == 0 ? 80 : tier == 1 ? 112 : 176, h = tier == 0 ? 80 : tier == 1 ? 112 : 144;
            var pos = new Vector2(m.Center.x, m.Bottom + m.GfxOffY);
            var spr = FxBatch.Frame("Projectile_" + (694 + tier), 0, h * Mathf.Min(m.Frame, m.S.Frames - 1), w, h, w / 2f + (tier < 2 ? 3f : 0f), h - 8f);
            var c = new Color(1f, 1f, 1f, 0.5f) * (1f - m.Alpha);
            fx.Draw(spr, pos, c, 0f, 1f);
            fx.Draw(spr, pos, c * 0.3f, 0f, 1.1f);
            for (int k = 0; k < 3; k++) fx.Light(new Vector2(m.Center.x, m.Pos.y + 24f + 48f * k), new Color(1.3f, 0.9f, 0.2f), 2.5f);
        }

        // ------------------------------------------------------------------ 688-690: Lightning Aura (AI_137)

        /// <summary>
        /// AI_137: on its first tick it settles on the ground under it and takes its size, a square
        /// of side 2h round the ground point (h the room up to a ceiling, 4-10 tiles); every 30th
        /// tick it shocks every enemy within h − 20 of the ground point that its crystal can see;
        /// sparks (226) stream in from the rim to the crystal, a few rise from the base.
        /// </summary>
        void AI137(M m)
        {
            // The Monk's sets (P:69928-69941): a shock every 25 ticks instead of 30; with tier 3 the room up is 8-14 tiles instead of 4-10.
            bool t3 = (Ooa & OoaSet.MonkT3) != 0;
            if (++m.Ai0 >= ((Ooa & OoaSet.MonkT2) != 0 ? 25f : 30f)) m.Ai0 = 0f;
            if (m.Ai0 == 0f)
                foreach (var n in npcs)
                    if (AuraReaches(m, n))
                    {
                        Sound("Custom/dd2_lightning_aura_zap_" + Random.Range(0, 4));
                        break;
                    }
            if (m.LocalAi0 == 0f)
            {
                m.LocalAi0 = 1f;
                m.Vel = Vector2.zero;
                int ox = TileOf(m.Center.x), oy = TileOf(m.Center.y), gy = int.MinValue, cy = int.MinValue;
                for (int i = 0; i < 200 && gy == int.MinValue; i++)
                    if (GroundTile(ox, oy + i)) gy = oy + i;
                if (gy == int.MinValue)
                {
                    m.Pos.y += 16f;
                    return;
                }
                int up = t3 ? 14 : 10, least = t3 ? 8 : 4;
                for (int i = 0; i < up && cy == int.MinValue; i++)
                    if (GroundTile(ox, gy - 1 - i)) cy = gy - 1 - i;
                if (cy == int.MinValue) cy = oy - up - 1;
                float groundY = gy * 16f, h = Mathf.Clamp(groundY - cy * 16f, least * 16f, up * 16f);
                m.W = m.H = (int)(2f * h);
                m.Center = new Vector2(ox * 16f + 8f, groundY);
                m.OldPos = m.Pos;
            }
            if (++m.FrameCounter >= 8)
            {
                m.FrameCounter = 0;
                m.Frame = (m.Frame + 1) % m.S.Frames;
            }
            var p = TerraParticles.Instance;
            var crystal = new Vector2(m.Center.x, m.Pos.y + 40f);
            if (p != null)
            {
                for (int k = 0; k < 4; k++)
                {
                    if (Random.Range(0, 6) != 0) continue;
                    var u = Random.insideUnitCircle.normalized;
                    if (Mathf.Abs(u.x) < 0.12f) continue;
                    var at = m.Center + u * (m.H - 40f) / 2f;
                    if (SolidTile(TileOf(at.x), TileOf(at.y)) || !AuraSees(m, at)) continue;
                    int i = p.NewDust(at, 0, 0, 226, 0f, 0f, 100);
                    if (i < 0) continue;
                    p[i].Position = at;
                    p[i].Velocity = (crystal - at).normalized;
                    p[i].Scale = 0.7f;
                    p[i].FadeIn = 1f;
                    p[i].NoGravity = true;
                }
                for (int k = 0; k < 4; k++)
                {
                    if (Random.Range(0, 10) != 0) continue;
                    int i = p.NewDust(crystal - new Vector2(8f, 0f), 16, Mathf.Max(1, (int)(m.H / 2f - 40f)), 226, 0f, 0f, 100);
                    if (i < 0) continue;
                    p[i].Velocity = p[i].Velocity * 0.6f + new Vector2(0f, -2f);
                    p[i].Scale = 0.7f;
                    p[i].NoGravity = true;
                }
            }
            SentryFall(m);
        }

        /// <summary>Projectile.CanHit for the aura (P:71575-71605): a clear line from inside the crystal, or one bent a fifth of the way round either side.</summary>
        bool AuraSees(M m, Vector2 to)
        {
            if (SolidTile(TileOf(to.x), TileOf(to.y))) return false;
            var a = new Vector2(m.Center.x, m.Pos.y + 20f);
            if (ClearLine(a, to)) return true;
            var mid = (a + to) / 2f;
            var n = (to - a).sqrMagnitude > 0f ? (to - a).normalized : Vector2.up;
            float len = (to - a).magnitude;
            var b1 = mid + new Vector2(-n.y, n.x) * 0.2f * len;
            if (ClearLine(a, b1) && ClearLine(b1, to)) return true;
            var b2 = mid + new Vector2(n.y, -n.x) * 0.2f * len;
            return ClearLine(a, b2) && ClearLine(b2, to);
        }

        /// <summary>Colliding for the aura (P:15887-15901): in its square, the nearest point of the enemy within h − 20 of the ground point, and seen.</summary>
        bool AuraReaches(M m, Npc n)
        {
            if (m.LocalAi0 == 0f || !n.Box.Overlaps(m.Box)) return false;
            var near = new Vector2(Mathf.Clamp(m.Center.x, n.Box.xMin, n.Box.xMax), Mathf.Clamp(m.Center.y, n.Box.yMin, n.Box.yMax));
            if (Vector2.Distance(near, m.Center) >= m.H / 2f - 20f) return false;
            return AuraSees(m, n.Center) || AuraSees(m, new Vector2(n.Center.x, n.Box.yMin));
        }

        /// <summary>
        /// The aura's shock (every 30 ticks): each enemy it reaches, its full damage (armour ignored:
        /// armorPenetrationPercent 1), no knockback, the whip tags.
        /// </summary>
        void AuraDamage(M m)
        {
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return;
            foreach (var n in npcs)
            {
                if (n.Hm == null || n.Hm.isDead || m.Immune.ContainsKey(n.Id) || !AuraReaches(m, n)) continue;
                int dmg = SummonTags.Modify(n.Hm, m.Damage, out bool crit, 1f, true, m.Center);
                // The Monk's sets (P:12930-12943): a crit one time in four (tier 3), one in six (tier 2).
                if ((Ooa & OoaSet.MonkT3) != 0 ? Random.Range(0, 4) == 0 : (Ooa & OoaSet.MonkT2) != 0 && Random.Range(0, 6) == 0) crit = true;
                if (crit) DevAuraCrits++;
                melee.HitWith(n.Col, dmg, 0f, crit ? 100f : 0f, 1f, TerraParticles.ToWorld(m.Center), 9999);
                DevHits++;
                DevHitDamage += dmg;
                SummonTags.OnHit(n.Hm, m.Damage, m.Center);
                m.Immune[n.Id] = 3;
            }
        }

        /// <summary>Dev: the aura's crits from the Monk's sets.</summary>
        public static int DevAuraCrits;

        /// <summary>M:32523-32563: the crystal hanging from the square's top edge (and its glow), the base on the ground; a blue glow round the ground point.</summary>
        static void DrawAura(FxBatch fx, M m)
        {
            int tier = m.Kind - Kind.Aura1;
            int w = tier == 0 ? 22 : tier == 1 ? 28 : 34, h = tier == 0 ? 30 : tier == 1 ? 32 : 38;
            var c73 = Color.Lerp(Lit, Color.white, 0.5f);
            var top = new Vector2(m.Center.x, m.Pos.y + m.GfxOffY);
            fx.Draw(FxBatch.Frame("Projectile_" + (688 + tier), 0, h * m.Frame, w, h, w / 2f, 0f), top, c73, 0f, 1f);
            fx.Draw(FxBatch.Frame("Glow_" + (228 + tier), 0, h * m.Frame, w, h, w / 2f, 0f), top, new Color(c73.r, c73.g, c73.b, 0.5f), 0f, 1f);
            int bw = tier == 2 ? 38 : 28, bh = tier == 0 ? 10 : tier == 1 ? 12 : 14;
            fx.Draw(FxBatch.Frame("Extra_" + (86 + tier), 0, 0, bw, bh, bw / 2f, bh - 2f), m.Center + new Vector2(0f, m.GfxOffY), c73, 0f, 1f);
            fx.Light(m.Center, new Color(0.2f, 0.7f, 1f), 2.5f);
        }
    }
}
