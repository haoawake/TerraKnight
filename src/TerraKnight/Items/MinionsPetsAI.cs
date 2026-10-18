using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The pets not on AI_026: the Shadow Orb and the fairies (aiStyle 11, P:24651), the Crimson Heart, the Companion Cube,
    /// the Dirtiest Block and the boulders (AI_067, P:50015-52108), the Magic Lantern (aiStyle 90, P:32969), the pets held
    /// at the player's shoulder (aiStyle 124, P:35619; AI_144_DD2Pet, P:71133) and the worms (AI_174, P:46178); and
    /// Chester's Piggy Bank (Main.TryInteractingWithMoneyTrough, Main.cs:38452; Player.HandleBeingInChestRange, Pl:31244).
    /// </summary>
    internal sealed partial class Minions
    {
        /// <summary>The player holds up, down, left or right (Player.controlUp...).</summary>
        static bool Holding(int k)
        {
            var input = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
            if (input == null) return false;
            return k == 0 ? input.up.IsPressed : k == 1 ? input.down.IsPressed : k == 2 ? input.left.IsPressed : input.right.IsPressed;
        }

        /// <summary>Player.ownedProjectileCounts for a pet: whether one of that projectile is out.</summary>
        bool PetOut(int id)
        {
            for (int k = 0; k < 2; k++)
                if ((pets[k] != null && pets[k].Pet.Id == id) || (petTwins[k] != null && petTwins[k].Pet.Id == id)) return true;
            return false;
        }

        // ------------------------------------------------------------------ aiStyle 11

        /// <summary>
        /// aiStyle 11 (P:24651-24760): the Shadow Orb turning slowly, 3 px a tick toward the player until 70 px off (just
        /// over his head or under his feet while up or down is held); the fairies at 6 px until 50 off, never overshooting,
        /// tilting with their speed, four frames of 4 ticks and a fairy dust (56, 73, 74) one tick in six. Past 800 px it is
        /// at the player.
        /// </summary>
        void PetAI011(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            bool fairy = type == 72 || type == 86 || type == 87;
            if (fairy)
            {
                if (velocity.x > 0f) m.SpriteDir = -1;
                else if (velocity.x < 0f) m.SpriteDir = 1;
                m.Rot = velocity.x * 0.1f;
                if (++m.FrameCounter >= 4)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame >= 4) m.Frame = 0;
                if (Random.Range(0, 6) == 0)
                {
                    int dust = type == 86 ? 73 : type == 87 ? 74 : 56;
                    int d = DustAt(m.Pos, m.W, m.H, dust, Vector2.zero, 200, 0.8f);
                    if (d >= 0) TerraParticles.Instance[d].Velocity *= 0.3f;
                }
            }
            else m.Rot += 0.02f;
            float speed = fairy ? 6f : 3f;
            float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
            int stop = 70;
            if (type == 18)
            {
                if (Holding(0))
                {
                    dy = pBox.yMin - 40f - m.Center.y;
                    dx -= 6f;
                    stop = 4;
                }
                else if (Holding(1))
                {
                    dy = pBox.yMax + 40f - m.Center.y;
                    dx -= 6f;
                    stop = 4;
                }
            }
            if (fairy) stop = 50;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > 800f) m.Pos = new Vector2(pCenter.x - m.W / 2, pCenter.y - m.H / 2);
            else if (dist > stop)
            {
                float left = dist - stop;
                velocity = new Vector2(dx, dy) * (speed / dist);
                if (fairy && velocity.magnitude > left) velocity = velocity.normalized * left;
            }
            else velocity = Vector2.zero;
        }

        // ------------------------------------------------------------------ AI_067's pets

        /// <summary>
        /// AI_067 for the Crimson Heart (500), the Companion Cube (653), the Dirtiest Block (1018) and the boulders (1056,
        /// 1090): rolling (turning with their speed) after a spot by the player (the heart 15 + 10 + 40 px behind, the boulders
        /// 25 behind, the cube and the block at him) at 0.5 a tick to 4 (the player's speed, 0.7, when faster); the cube, the
        /// block and the boulders slow to a stop near it and match a slower player; the heart hops all the while it moves,
        /// the cube and block over 3 a tick, the boulders over 5; the heart beats (frames, a blood drop a tick), the block sheds
        /// dirt. Past 200 px (the cube 300) or 300 up or down it flies back (10 a tick, the rainbow boulder 18, bouncing round).
        /// Not here: the Companion Cube's stab in the dark (no light query) and its death in lava (no lava).
        /// </summary>
        void PetAI067(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            float leash = type == 653 ? 300f : 200f;
            const float leashY = 300f;
            // P:50345-50388: the idle spot (Terraria's player.width / 2 = 10).
            var goal = pCenter;
            if (type == 500)
            {
                goal.x -= (15 + 10) * pDir;
                goal.x -= 40 * pDir;
            }
            else if (type == 1056 || type == 1090) goal.x -= (15 + 10) * pDir;
            if (type == 500)
            {
                // P:50356-50379: the heart beats (6 ticks a frame, 12 on frames 0 and 2) and drips blood.
                int ticks = m.Frame == 0 || m.Frame == 2 ? 12 : 6;
                if (++m.FrameCounter >= ticks)
                {
                    m.FrameCounter = 0;
                    if (++m.Frame >= p.Frames) m.Frame = 0;
                }
                m.Rot += velocity.x / 20f;
                var up = Rotated(Rotated(UpT, m.Rot), m.Dir * 0.2f);
                var tp = TerraParticles.Instance;
                int d = tp != null ? tp.NewDust(m.Center + up * 10f - new Vector2(4f, 4f), 0, 0, 5, up.x, up.y) : -1;
                if (d >= 0)
                {
                    tp[d].Scale = 1f;
                    tp[d].Velocity = Rotated(up, Random.Range(-1f, 1f) * (Mathf.PI / 4f)) * 3.5f;
                    tp[d].NoGravity = true;
                }
            }
            if (type == 1018) m.Rot += velocity.x / 20f;
            if (type == 1056 || type == 1090) m.Rot += velocity.x / 16f;
            if (type == 653) m.Rot += velocity.x / 20f;
            // P:17588-17613: the heart, the cube and the block drop through platforms once the player is 4 px lower.
            m.FallThrough = type == 500 || type == 653 || type == 1018 ? pBox.yMax > m.Bottom + 4f : pBox.yMax - 12f > m.Bottom;

            if (m.Ai0 == 1f)
            {
                // P:50638-50858: flying back.
                m.TileCollide = false;
                float accel = 0.2f, speed = type == 1090 ? 18f : 10f;
                const int land = 200;
                float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
                if (speed < ps) speed = ps;
                var to = pCenter - m.Center;
                float dist = to.magnitude;
                if (dist > 2000f) m.Pos = pCenter - new Vector2(m.W, m.H) / 2f;
                if (dist < land && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
                {
                    m.Ai0 = 0f;
                    if (velocity.y < -6f) velocity.y = -6f;
                }
                float near = type == 1090 ? 350f : 60f;
                if (dist < near)
                {
                    m.LocalAi0 = 0f;
                    if (type == 1090 && velocity.magnitude < speed)
                    {
                        if (velocity == Vector2.zero) velocity = new Vector2(0f, -1f);
                        velocity = velocity.normalized * speed;
                    }
                }
                else
                {
                    to = to.normalized * speed;
                    if (type == 1090)
                    {
                        // The rainbow boulder bounces about on its way (30° either side).
                        velocity = Rotated(to, Random.Range(-1f, 1f) * 0.5235988f);
                        if (m.LocalAi0 == 0f) m.LocalAi0 = 1f;
                    }
                    else
                    {
                        velocity.x = Steer(velocity.x, to.x, accel);
                        velocity.y = Steer(velocity.y, to.y, accel);
                    }
                }
                if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                if (type == 1056 || type == 1090) m.Rot += 0.5f * m.SpriteDir;
                if (type == 1018 && miscCounter % 3 == 0)
                {
                    const int r = 2;
                    var tp = TerraParticles.Instance;
                    int d = tp != null ? tp.NewDust(m.Pos + new Vector2(-r, -r), 16 + r * 2, 16 + r * 2, 0, 0f, 0f, 0, 0.8f) : -1;
                    if (d >= 0) tp[d].Velocity = Rotated(-velocity * 0.25f, Random.Range(-1f, 1f) * 0.2617994f);
                }
            }
            // P:51166-51190: the leash.
            if (m.Ai0 == 0f)
            {
                var to = pCenter - m.Center;
                if (to.magnitude > 2000f) m.Pos = pCenter - new Vector2(m.W, m.H) / 2f;
                else if (to.magnitude > leash || Mathf.Abs(to.y) > leashY)
                {
                    if (velocity.y > 0f && to.y < 0f) velocity.y = 0f;
                    if (velocity.y < 0f && to.y > 0f) velocity.y = 0f;
                    m.Ai0 = 1f;
                }
            }
            if (m.Ai0 == 0f) Walk067Pet(m, p, PetIdleGoal(m, goal, leash, leashY));
            if (type == 1090)
            {
                // P:50069-50106: the rainbow's frame runs 0-99 two ticks a frame; localAI[2] eases to 1 while it flies.
                if (++m.FrameCounter >= 2)
                {
                    m.FrameCounter = 0;
                    if (++m.Frame >= 100) m.Frame = 0;
                }
                m.LocalAi2 = Mathf.Clamp01(m.LocalAi2 + (m.Ai0 == 1f ? 0.025f : -0.025f));
            }
        }

        /// <summary>
        /// P:51191-51245: the idle spot's place: the player himself when the spot is on his far side, dropped to the floor
        /// (20 tiles at most), pulled back to where a box could move from the player, kept where it is when close, and
        /// within the leash.
        /// </summary>
        Vector2 PetIdleGoal(M m, Vector2 goal, float leash, float leashY)
        {
            if (Vector2.Distance(m.Center, pCenter) > 60f && Vector2.Distance(m.Center, goal) > 60f && Mathf.Sign(goal.x - pCenter.x) != Mathf.Sign(m.Center.x - pCenter.x)) goal = pCenter;
            var r = new Rect(goal.x - m.W / 2f, goal.y - m.H / 2f, m.W, m.H);
            for (int k = 0; k < 20; k++)
            {
                if (SolidBox(r)) break;
                r.y += 16f;
                goal.y += 16f;
            }
            var start = pCenter - new Vector2(m.W, m.H) / 2f;
            goal = start + Sweep(start, goal - pCenter, m.W, m.H) + new Vector2(m.W, m.H) / 2f;
            if (Vector2.Distance(m.Center, goal) < 32f && Vector2.Distance(pCenter, m.Center) < Vector2.Distance(pCenter, goal)) goal = m.Center;
            var off = pCenter - goal;
            if (off.magnitude > leash || Mathf.Abs(off.y) > leashY)
            {
                var from = new Vector2(pCenter.x - m.W / 2f, pCenter.y - m.H / 2f);
                var d = goal - pCenter;
                var last = from;
                for (float t = 0f; t < 1f; t += 0.05f)
                {
                    var at = from + d * t;
                    if (SolidBox(at.x, at.y, r.width, r.height)) break;
                    last = at;
                }
                goal = last + new Vector2(m.W, m.H) / 2f;
            }
            return goal;
        }

        /// <summary>P:51246-51655, 52051: AI_067's walk for the pets, its hops and its fall (0.4 and a pull toward a lower spot).</summary>
        void Walk067Pet(M m, PetProj p, Vector2 goal)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            m.TileCollide = true;
            float accel = 0.5f, soft = 4f, max = 4f;
            const float creep = 0.1f;
            float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
            if (max < ps)
            {
                max = ps;
                accel = 0.7f;
            }
            if (type == 653 || type == 1018)
            {
                float pv = pVel.magnitude;
                if (pv < 0.1f) pv = 0f;
                if (pv != 0f && pv < max) max = pv;
            }
            int wish = 0;
            bool jump = false;
            float dx = goal.x - m.Center.x;
            var toGoal = goal - m.Center;
            if ((type == 1056 || type == 1090) && Mathf.Abs(dx) < 50f)
            {
                float pv = pVel.magnitude;
                if (pv < 0.1f) pv = 0f;
                if (pv != 0f && pv < max) max = pv;
                // At rest the boulder rolls back upright (AngleLerp 0.1).
                if (velocity.magnitude <= 0.3f) m.Rot = AngleLerp(WrapAngle(m.Rot), 0f, 0.1f);
                velocity.x *= 0.9f;
                if (Mathf.Abs(velocity.x) < 0.1f) velocity.x = 0f;
            }
            else if ((type == 1018 && Mathf.Abs(dx) < 50f) || (type == 653 && Mathf.Abs(dx) < 150f))
            {
                m.Rot = AngleTowards(m.Rot, 0f, 0.2f);
                velocity.x *= 0.9f;
                if (Mathf.Abs(velocity.x) < 0.1f) velocity.x = 0f;
            }
            else if (Mathf.Abs(dx) > 5f)
            {
                if (dx < 0f)
                {
                    wish = -1;
                    if (velocity.x > -soft) velocity.x -= accel;
                    else velocity.x -= creep;
                }
                else
                {
                    wish = 1;
                    if (velocity.x < soft) velocity.x += accel;
                    else velocity.x += creep;
                }
                bool hop = type != 653 && type != 1018 && !((type == 1056 || type == 1090) && velocity.magnitude <= 5f);
                if (hop) jump = true;
            }
            else
            {
                velocity.x *= 0.9f;
                if (Mathf.Abs(velocity.x) < accel * 2f) velocity.x = 0f;
            }
            bool bigGap = Mathf.Abs(toGoal.x) >= 64f || (toGoal.y <= -48f && Mathf.Abs(toGoal.x) >= 8f);
            if (wish != 0 && bigGap)
            {
                int i = TileOf(m.Pos.x + m.W / 2) + wish + (int)velocity.x, j0 = TileOf(m.Pos.y);
                for (int j = j0; j < j0 + m.H / 16 + 1; j++)
                    if (SolidTile(i, j)) jump = true;
            }
            if (type == 500 && velocity.x != 0f) jump = true;
            if ((type == 653 || type == 1018) && Mathf.Abs(velocity.x) > 3f) jump = true;
            if ((type == 1056 || type == 1090) && velocity.magnitude > 5f) jump = true;
            StepUp(m, m.FallThrough);
            float pull = Mathf.Clamp01(Mathf.InverseLerp(0f, 100f, toGoal.y)) * Mathf.Clamp01(Mathf.InverseLerp(-2f, -6f, velocity.y));
            if (velocity.y == 0f)
            {
                if (jump)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int fi = TileOf(k == 0 ? m.Pos.x : k == 2 ? m.Pos.x + m.W : m.Pos.x + m.W / 2), fj = TileOf(m.Pos.y + m.H);
                        if (!Floor026(fi, fj)) continue;
                        int i = TileOf(m.Pos.x + m.W / 2) + wish + (int)velocity.x, j = TileOf(m.Pos.y + m.H / 2);
                        velocity.y = WallJump(i, j);
                    }
                }
                if (goal.y - m.Center.y < -48f)
                {
                    float h = m.Center.y - goal.y;
                    velocity.y = h < 60f ? -6f : h < 80f ? -7f : h < 100f ? -8f : h < 120f ? -9f : h < 140f ? -10f : h < 160f ? -11f
                        : h < 190f ? -12f : h < 210f ? -13f : h < 270f ? -14f : h < 310f ? -15f : -16f;
                }
                // The block lands in a puff of dirt.
                if (type == 1018 && m.LocalAi2 == 0f)
                {
                    m.LocalAi2 = 1f;
                    var tp = TerraParticles.Instance;
                    for (int k = 0; k < 6 && tp != null; k++)
                    {
                        int d = tp.NewDust(m.Pos + velocity, 16, 16, 0, 0f, 0f, 0, 0.8f);
                        if (d < 0) continue;
                        var v = new Vector2(velocity.x * 0.25f, -2f + Mathf.Abs(velocity.y) * 0.25f);
                        tp[d].Velocity = Rotated(v, Random.Range(-1f, 1f) * 0.2617994f);
                    }
                }
            }
            else if (type == 1018) m.LocalAi2 = 0f;
            velocity.x = Mathf.Clamp(velocity.x, -max, max);
            if (velocity.x < 0f) m.Dir = -1;
            if (velocity.x > 0f) m.Dir = 1;
            if (velocity.x == 0f) m.Dir = pCenter.x > m.Center.x ? 1 : -1;
            if (velocity.x > accel && wish == 1) m.Dir = 1;
            if (velocity.x < -accel && wish == -1) m.Dir = -1;
            m.SpriteDir = m.Dir;
            velocity.y += 0.4f + pull;
            if (velocity.y > 10f) velocity.y = 10f;
        }

        /// <summary>Utils.AngleTowards: an angle turned toward another by at most a step.</summary>
        static float AngleTowards(float from, float to, float step)
        {
            float d = Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return from + Mathf.Clamp(d, -step, step);
        }

        // ------------------------------------------------------------------ aiStyle 90

        /// <summary>
        /// aiStyle 90 (P:32969-33089): the Magic Lantern 40 px over the player's shoulder on his facing side, closing a third of
        /// the way a tick at 4 px (more past 200); it keeps to blocks until it loses sight of him or falls 200 behind, then
        /// flies through them. Every 10 ticks the treasure within 17 tiles sparkles (dust 204). Fast, it swings along its way
        /// (frames 4-7), else hangs (0-3).
        /// </summary>
        void PetAI090(M m, PetProj p)
        {
            ref var velocity = ref m.Vel;
            if (m.TileCollide)
            {
                if (!ClearLine(m.Center, pCenter)) m.TileCollide = false;
                else if (!SolidBox(m.Box) && ClearLine(m.Center, pCenter)) m.TileCollide = true;
            }
            m.Dir = pDir;
            m.SpriteDir = m.Dir;
            if (++m.LocalAi0 >= 10f)
            {
                m.LocalAi0 = 0f;
                TreasureSparkle(m.Center, 4, null);
            }
            var to = pCenter - m.Center;
            to.x += 40 * m.Dir;
            to.y -= 40f;
            float dist = to.magnitude;
            if (dist > 1000f) m.Center = pCenter;
            const float ease = 3f;
            float speed = 4f;
            if (dist > 200f)
            {
                speed += (dist - 200f) * 0.1f;
                m.TileCollide = false;
            }
            if (dist < speed)
            {
                velocity *= 0.25f;
                speed = dist;
            }
            if (to != Vector2.zero) to = to.normalized * speed;
            velocity = (velocity * (ease - 1f) + to) / ease;
            if (velocity.magnitude > 6f)
            {
                float along = Mathf.Atan2(velocity.y, velocity.x) + 1.57f;
                if (Mathf.Abs(m.Rot - along) >= 3.14f) m.Rot += along < m.Rot ? -6.28f : 6.28f;
                m.Rot = (m.Rot * 4f + along) / 5f;
                if (++m.FrameCounter > 4)
                {
                    m.FrameCounter = 0;
                    if (++m.Frame > 7) m.Frame = 4;
                }
                if (m.Frame < 4) m.Frame = 7;
                return;
            }
            if (m.Rot > 3.14f) m.Rot -= 6.28f;
            if (m.Rot > -0.01f && m.Rot < 0.01f) m.Rot = 0f;
            else m.Rot *= 0.9f;
            if (++m.FrameCounter > 6)
            {
                m.FrameCounter = 0;
                if (++m.Frame > 3) m.Frame = 0;
            }
        }

        /// <summary>
        /// The spelunker sparkle round a pet (17 tiles; Main.IsTileSpelunkable: the meteorite on the grid and the chests set
        /// down): one tile in `chance` gets a dust 204 (alpha 150, 0.3, fading in to 0.75). With `nearest`, the nearest such
        /// tile is reported (the Suspicious Looking Eye looks at it).
        /// </summary>
        void TreasureSparkle(Vector2 center, int chance, System.Action<Vector2> nearest)
        {
            if ((center - pCenter).magnitude > 2560f) return;
            const int r = 17;
            int ci = TileOf(center.x), cj = TileOf(center.y);
            var tp = TerraParticles.Instance;
            float best = -1f;
            Vector2 bestAt = default;
            void Tile(int i, int j)
            {
                if (new Vector2(ci - i, cj - j).magnitude >= r) return;
                int roll = Random.Range(0, nearest != null ? 8 : chance);
                if (roll >= (nearest != null ? 4 : 1)) return;
                var at = new Vector2(i * 16 + 8, j * 16 + 8);
                if (nearest != null)
                {
                    float dd = Vector2.Distance(center, at);
                    if (dd < best || best == -1f)
                    {
                        best = dd;
                        bestAt = at;
                    }
                    if (roll >= 2) return;
                }
                if (tp == null) return;
                int d = tp.NewDust(new Vector2(i * 16, j * 16), 16, 16, 204, 0f, 0f, 150, 0.3f);
                if (d < 0) return;
                tp[d].FadeIn = 0.75f;
                tp[d].Velocity *= 0.1f;
            }
            for (int i = ci - r; i <= ci + r; i++)
                for (int j = cj - r; j <= cj + r; j++)
                    if (TerraWorld.Get(i, -j - 1) == CellKind.Meteorite) Tile(i, j);
            foreach (var box in Chests.Boxes())
            {
                var tl = TerraParticles.ToPx(new Vector2(box.xMin, box.yMax));
                int i0 = TileOf(tl.x + 1f), j0 = TileOf(tl.y + 1f);
                for (int i = i0; i < i0 + 2; i++)
                    for (int j = j0; j < j0 + 2; j++)
                        if (Mathf.Abs(i - ci) <= r && Mathf.Abs(j - cj) <= r) Tile(i, j);
            }
            if (nearest != null && best != -1f) nearest(bestAt);
        }

        // ------------------------------------------------------------------ aiStyle 124

        /// <summary>
        /// aiStyle 124 (P:35619-36340): the Suspicious Looking Eye, the Eye of Cthulhu, Twins, Fairy Princess, Jack 'O Lantern,
        /// Ice Queen and Glommer pets at a spot by the player's shoulder (30 px ahead and 20 up; the Twins 50 up, 70 with the
        /// eye out; the Fairy Princess 36 behind; the lantern 60 up and behind with the Twins; the Ice Queen 30 behind, ahead
        /// with the Fairy Princess; Glommer 40 behind), a tenth of the way a tick; facing the player's way (the princess, the
        /// queen and Glommer their own way once 50 px off). Fast, each its own turn and frames (the princess and queen shed
        /// dust); slow, its idle; Glommer, resting 4 tiles over the ground, bounces now and then. The eye watches what moves,
        /// the nearest enemy or a treasure it spotted (a check one tick in fifteen), its pupil drawn by DrawPets.
        /// </summary>
        void PetAI124(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            m.Dir = m.SpriteDir = pDir;
            float side = 30f, up = -20f;
            int dir = pDir;
            if (type == 882 && PetOut(650)) dir *= -1;
            if (type == 888)
            {
                side = 30f;
                up = -50f;
                if (PetOut(650)) up = -70f;
            }
            if (type == 895)
            {
                side = -36f;
                up = -50f;
            }
            if (type == 896)
            {
                side = 30f;
                up = -60f;
                if (PetOut(888)) side = -30f;
            }
            if (type == 898)
            {
                side = -30f;
                up = -50f;
                if (PetOut(895)) side = 30f;
            }
            if (type == 957)
            {
                side = -40f;
                up = -40f;
                if (PetOut(895)) side = 40f;
            }
            var offset = new Vector2(dir * side, up);
            var spot = pCenter + offset;
            float dist = Vector2.Distance(m.Center, spot);
            if (dist > 1000f) m.Center = pCenter + offset;
            var to = spot - m.Center;
            const float near = 4f;
            if (dist < near) velocity *= 0.25f;
            if (to != Vector2.zero) velocity = to.magnitude < near ? to : to * 0.1f;
            if (dist > 50f && (type == 895 || type == 898 || type == 957))
            {
                m.Dir = m.SpriteDir = 1;
                if (velocity.x < 0f) m.Dir = m.SpriteDir = -1;
            }
            var tp = TerraParticles.Instance;
            if (velocity.magnitude > 6f)
            {
                switch (type)
                {
                    case 650:
                        m.Rot = EaseAngle(m.Rot, Mathf.Atan2(velocity.y, velocity.x) + Mathf.PI / 2f, 12f);
                        if (++m.FrameCounter >= 4)
                        {
                            m.FrameCounter = 0;
                            if (++m.Frame >= p.Frames) m.Frame = 0;
                        }
                        break;
                    case 882:
                        m.Rot = velocity.x * 0.125f;
                        if (++m.FrameCounter >= 3)
                        {
                            m.FrameCounter = 0;
                            if (m.Frame < 6) m.Frame = 6;
                            else if (++m.Frame > 15) m.Frame = 10;
                        }
                        break;
                    case 888:
                        m.Rot = EaseAngle(m.Rot, Mathf.Atan2(velocity.y, velocity.x) + Mathf.PI / 2f, 12f);
                        if (++m.FrameCounter >= 3)
                        {
                            m.FrameCounter = 0;
                            if (m.Frame < 6) m.Frame = 6;
                            else if (++m.Frame > 17) m.Frame = 6;
                        }
                        break;
                    case 895:
                    case 898:
                        m.Rot = EaseAngle(m.Rot, velocity.x * 0.1f, 12f);
                        if (++m.FrameCounter >= 3)
                        {
                            m.FrameCounter = 0;
                            if (++m.Frame >= p.Frames) m.Frame = 0;
                        }
                        if (m.FrameCounter == 0 && tp != null)
                        {
                            int d = type == 895 ? tp.NewDust(m.Pos, m.W, m.H, 242, 0f, 0f, 50, 2f) : tp.NewDust(m.Pos, m.W, m.H, 80, 0f, 0f, 50);
                            if (d >= 0) tp[d].NoGravity = true;
                        }
                        break;
                    case 896:
                        m.Rot = EaseAngle(m.Rot, Mathf.Atan2(velocity.y, velocity.x) + Mathf.PI / 2f, 12f);
                        if (++m.FrameCounter >= 4)
                        {
                            m.FrameCounter = 0;
                            if (m.Frame < 8) m.Frame = 8;
                            else if (++m.Frame >= p.Frames) m.Frame = 8;
                        }
                        break;
                    case 957:
                        m.Rot = EaseAngle(m.Rot, velocity.x * 0.05f, 12f);
                        if (++m.FrameCounter >= 4)
                        {
                            m.FrameCounter = 0;
                            if (++m.Frame >= p.Frames * 2) m.Frame = 0;
                        }
                        break;
                }
            }
            else
            {
                if (type != 882)
                {
                    if (m.Rot > Mathf.PI) m.Rot -= Mathf.PI * 2f;
                    if (m.Rot > -0.005f && m.Rot < 0.005f) m.Rot = 0f;
                    else m.Rot *= 0.96f;
                }
                switch (type)
                {
                    case 650:
                        if (++m.FrameCounter >= 6)
                        {
                            m.FrameCounter = 0;
                            if (++m.Frame >= p.Frames) m.Frame = 0;
                        }
                        break;
                    case 882:
                        m.Rot = velocity.x * 0.125f;
                        if (++m.FrameCounter >= 5)
                        {
                            m.FrameCounter = 0;
                            m.Frame++;
                            if (m.Frame == 6 || m.Frame >= p.Frames) m.Frame = 0;
                        }
                        break;
                    case 888:
                        if (++m.FrameCounter >= 5)
                        {
                            m.FrameCounter = 0;
                            m.Frame++;
                            if (m.Frame == 6 || m.Frame >= 18) m.Frame = 0;
                        }
                        break;
                    case 895:
                    case 898:
                        if (++m.FrameCounter >= 5)
                        {
                            m.FrameCounter = 0;
                            if (++m.Frame >= p.Frames) m.Frame = 0;
                        }
                        break;
                    case 896:
                        // The lantern winks one time in fifteen (frames 8 on).
                        if (++m.FrameCounter >= 5)
                        {
                            m.FrameCounter = 0;
                            m.Frame++;
                            if (m.Frame == 8 || m.Frame >= p.Frames)
                            {
                                m.Frame = 0;
                                if (Random.Range(0, 15) == 0) m.Frame = 8;
                            }
                        }
                        break;
                    case 957:
                        GlommerIdle(m, p);
                        break;
                }
            }
            if (type != 650) return;
            // P:36216-36260: a treasure seen is looked at for a second.
            if (m.Ai0 > 0f && ++m.Ai0 >= 60f)
            {
                m.Ai0 = 0f;
                m.Ai1 = 0f;
            }
            if (Random.Range(0, 15) == 0)
                TreasureSparkle(m.Center, 8, at =>
                {
                    m.Ai0 = 1f;
                    m.Ai1 = Mathf.Atan2(at.y - m.Center.y, at.x - m.Center.x);
                });
            EyePupil(m);
        }

        /// <summary>Rotation eased toward an angle over `k` ticks, the short way round.</summary>
        static float EaseAngle(float rot, float target, float k)
        {
            if (Mathf.Abs(rot - target) >= Mathf.PI) rot += target < rot ? -Mathf.PI * 2f : Mathf.PI * 2f;
            return (rot * (k - 1f) + target) / k;
        }

        /// <summary>
        /// P:36112-36214: Glommer at rest. Hovering with the ground exactly 4 tiles under it, after its wait (200-349 ticks)
        /// it bounces (frames 24-35 of its third column, 3 ticks each, 2 up to frame 30) with its squelch at frame 32; else it
        /// flaps (0-23).
        /// </summary>
        void GlommerIdle(M m, PetProj p)
        {
            int frames = p.Frames;
            if (m.Vel.magnitude <= 0.01f)
            {
                bool ground = true;
                int i = TileOf(m.Center.x), j = TileOf(m.Center.y);
                const int depth = 4;
                for (int k = 0; k < depth + 1; k++)
                {
                    bool solid = Floor026(i, j);
                    if ((k == depth && !solid) || (k < depth && solid))
                    {
                        ground = false;
                        break;
                    }
                    j++;
                }
                if (ground)
                {
                    m.LocalAi0--;
                    if (m.LocalAi0 <= 0f)
                    {
                        m.LocalAi0 = 0f;
                        if (m.Frame < frames * 2) m.Frame = frames * 2;
                        int ticks = m.Frame <= 30 && m.Frame <= 33 ? 2 : 3;
                        if (++m.FrameCounter >= ticks)
                        {
                            m.FrameCounter = 0;
                            m.Frame++;
                            if (m.Frame >= frames * 3)
                            {
                                m.LocalAi0 = 200 + Random.Range(0, 150);
                                m.Frame = 0;
                            }
                            if (m.Frame == 32) Sound("Custom/glommer_bounce_*2", 0.5f);
                        }
                    }
                    else if (++m.FrameCounter >= 4)
                    {
                        m.FrameCounter = 0;
                        if (++m.Frame >= frames * 2) m.Frame = 0;
                    }
                    return;
                }
            }
            m.LocalAi0 = 300f;
            if (++m.FrameCounter >= 4)
            {
                m.FrameCounter = 0;
                if (++m.Frame >= frames * 2) m.Frame = 0;
            }
        }

        /// <summary>
        /// P:36261-36340: the Suspicious Looking Eye's pupil (localAI[0]: its angle and what it looks at; localAI[1]: how far
        /// out, and its size): along the player's way when he runs (over 3 a tick), else at the nearest enemy within 500 px,
        /// else at the treasure it spotted, else ahead of the player.
        /// </summary>
        void EyePupil(M m)
        {
            float angle = m.LocalAi0 % (Mathf.PI * 2f) - Mathf.PI;
            float reach = (float)System.Math.IEEERemainder(m.LocalAi1, 1.0);
            if (reach < 0f) reach++;
            float size = Mathf.Floor(m.LocalAi1);
            const float max = 0.999f;
            float look;
            int what;
            float ease = 0.1f;
            bool running = pVel.magnitude > 3f;
            Npc foe = null;
            float foeDist = 500f;
            foreach (var n in npcs)
            {
                float d = Vector2.Distance(m.Center, n.Center);
                if (d < foeDist)
                {
                    foeDist = d;
                    foe = n;
                }
            }
            if (running)
            {
                look = Mathf.Atan2(pVel.y, pVel.x);
                what = 1;
                reach = Mathf.Clamp(reach + 0.05f, 0f, max);
                size += Mathf.Sign(-10f - size);
            }
            else if (foe != null)
            {
                look = Mathf.Atan2(foe.Center.y - m.Center.y, foe.Center.x - m.Center.x);
                what = 2;
                reach = Mathf.Clamp(reach + 0.05f, 0f, max);
                size += Mathf.Sign(-12f - size);
            }
            else if (m.Ai0 > 0f)
            {
                look = m.Ai1;
                reach = Mathf.Clamp(reach + Mathf.Sign(0.75f - reach) * 0.05f, 0f, max);
                what = 4;
                size += Mathf.Sign(10f - size);
                if (Random.Range(0, 10) == 0)
                {
                    var tp = TerraParticles.Instance;
                    var at = m.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 6f * reach - Vector2.one * 4f;
                    int d = tp != null ? tp.NewDust(at, 8, 8, 204, 0f, 0f, 150, 0.3f) : -1;
                    if (d >= 0)
                    {
                        tp[d].FadeIn = 0.75f;
                        tp[d].Velocity *= 0.1f;
                    }
                }
            }
            else
            {
                look = pDir == 1 ? 0f : 3.1416028f;
                reach = Mathf.Clamp(reach + Mathf.Sign(0.75f - reach) * 0.05f, 0f, max);
                what = 0;
                size += Mathf.Sign(0f - size);
                ease = 0.12f;
            }
            var v = Vector2.Lerp(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), new Vector2(Mathf.Cos(look), Mathf.Sin(look)), ease);
            look = Mathf.Atan2(v.y, v.x);
            m.LocalAi0 = look + what * (Mathf.PI * 2f) + Mathf.PI;
            m.LocalAi1 = size + reach;
        }

        // ------------------------------------------------------------------ AI_144

        /// <summary>
        /// AI_144_DD2Pet (P:71133-71418): the Old One's Army pets, Estee and the Axe Fairy at a spot 30 px ahead of the player and
        /// 20 up (the Axe Fairy behind), bobbing (cos of localAI[0]), 64 px further back while the eye or the Flickerwick is out
        /// (not the Flickerwick itself); a tenth of the way a tick (Estee a fortieth), turning with their speed. The Flickerwick
        /// sheds its flame (dust 135), Estee her stars (292) and pulses in opacity. (The Axe Fairy's tree chopping: the grid has
        /// no trees.)
        /// </summary>
        void PetAI144(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            float near = 4f, ease = 0.1f, tilt = 0.08f;
            int idleTicks = 6, fastTicks = 4, frames = p.Frames, first = 0;
            bool ownCounter = false, shift = true;
            var offset = new Vector2(pDir * 30, -20f);
            var tp = TerraParticles.Instance;
            switch (type)
            {
                case 702:
                {
                    offset.y += Mathf.Cos(m.LocalAi0 * (Mathf.PI / 60f)) * 2f;
                    fastTicks = 4;
                    idleTicks = 10;
                    shift = false;
                    near = 6f;
                    var flame = Rotated(new Vector2(m.SpriteDir == -1 ? -6 : -2, -26f), m.Rot);
                    if (Random.Range(0, 24) == 0 && tp != null)
                    {
                        int d = tp.NewDust(m.Center + flame, 4, 4, 135, 0f, 0f, 100);
                        if (d >= 0)
                        {
                            if (Random.Range(0, 3) != 0)
                            {
                                tp[d].NoGravity = true;
                                tp[d].Velocity.y -= 3f;
                            }
                            tp[d].Velocity *= 0.5f;
                            tp[d].Velocity.y -= 0.9f;
                            tp[d].Scale += 0.1f + Random.value * 0.6f;
                        }
                    }
                    break;
                }
                case 703:
                    offset.y += Mathf.Cos(m.LocalAi0 * (Mathf.PI / 30f)) * 2f;
                    idleTicks = 4;
                    fastTicks = 2;
                    near = 3f;
                    frames = 4;
                    break;
                case 764:
                {
                    ease = 0.025f;
                    tilt = 0.04f;
                    ownCounter = true;
                    if (++m.LocalAi0 > 120f) m.LocalAi0 = 0f;
                    m.LocalAi1 += velocity.x * 0.01f;
                    m.LocalAi1 += 1f / 120f;
                    if (m.LocalAi1 < Mathf.PI * -2f) m.LocalAi1 += Mathf.PI * 2f;
                    if (m.LocalAi1 > Mathf.PI * 2f) m.LocalAi1 -= Mathf.PI * 2f;
                    if (velocity.magnitude < 4f)
                    {
                        m.LocalAi1 *= 0.9f;
                        if (velocity.magnitude > 0.1f && Random.Range(0, 30) == 0 && tp != null)
                        {
                            int d = tp.NewDust(m.Pos - velocity, m.W, m.H, 292, velocity.x * 0.5f, velocity.y * 0.5f, 150);
                            if (d >= 0) tp[d].Velocity *= 0.3f;
                        }
                    }
                    else if (Random.Range(0, 15) == 0 && tp != null) tp.NewDust(m.Pos - velocity, m.W, m.H, 292, velocity.x * 0.5f, velocity.y * 0.5f, 150, 0.9f);
                    float pulse = m.LocalAi0 / 120f * 2f;
                    if (pulse > 1f) pulse = 2f - pulse;
                    // Opacity 0.4-0.75: alpha = 255 × (1 − opacity).
                    SetAlpha(m, Mathf.RoundToInt(255f * (1f - Mathf.Lerp(0.4f, 0.75f, pulse))));
                    offset.y += Mathf.Cos(m.LocalAi0 / 120f * (Mathf.PI * 2f)) * 2f;
                    break;
                }
                case 1050:
                    idleTicks = 5;
                    fastTicks = 3;
                    frames = 3;
                    offset.x *= -1f;
                    near = 3f;
                    if (m.LocalAi1 < 0f) m.LocalAi1++;
                    if (m.LocalAi1 >= 0f)
                    {
                        if (Vector2.Distance(m.Center, pCenter) < 320f) m.LocalAi2++;
                        else m.LocalAi1 = -300f;
                    }
                    offset.y += Mathf.Cos(m.LocalAi0 * (Mathf.PI / 30f)) * 2f;
                    break;
            }
            if (shift && (PetOut(650) || PetOut(702))) offset.x += -pDir * 64;
            m.Dir = m.SpriteDir = pDir;
            var spot = pCenter + offset;
            float dist = Vector2.Distance(m.Center, spot);
            if (dist > 1000f) m.Center = pCenter + offset;
            var to = spot - m.Center;
            if (dist < near) velocity *= 0.25f;
            if (to != Vector2.zero) velocity = to.magnitude < near * 0.5f ? to : to * ease;
            if (velocity.magnitude > 6f)
            {
                m.Rot = EaseAngle(m.Rot, velocity.x * tilt + velocity.y * m.SpriteDir * 0.02f, 12f);
                if (++m.FrameCounter >= fastTicks)
                {
                    m.FrameCounter = 0;
                    if (++m.Frame >= frames) m.Frame = first;
                }
            }
            else
            {
                if (m.Rot > Mathf.PI) m.Rot -= Mathf.PI * 2f;
                if (m.Rot > -0.005f && m.Rot < 0.005f) m.Rot = 0f;
                else m.Rot *= 0.96f;
                if (++m.FrameCounter >= idleTicks)
                {
                    m.FrameCounter = 0;
                    if (++m.Frame >= frames) m.Frame = first;
                }
            }
            if (!ownCounter && ++m.LocalAi0 > 120f) m.LocalAi0 = 0f;
        }

        // ------------------------------------------------------------------ AI_174

        /// <summary>
        /// AI_174_MultisegmentPet (P:46178-46255): the worms swim after the player as fast as he goes (4 to 12), turning at most
        /// 5° a tick toward him once 120 px off, drifting up when level; their path (oldPos) for the segments.
        /// </summary>
        void PetAI174(M m, PetProj p)
        {
            ref var velocity = ref m.Vel;
            if (Vector2.Distance(m.Center, pCenter) > 2000f)
            {
                m.Center = pCenter;
                velocity = Vector2.zero;
            }
            if (!wormTrails.TryGetValue(m, out var trail)) wormTrails[m] = trail = new System.Collections.Generic.List<Vector2>();
            float dist = (pCenter - m.Center).magnitude;
            float speed = Mathf.Min(12f, Mathf.Max(4f, pVel.magnitude));
            if (velocity == Vector2.zero)
            {
                // A new worm lies out behind its head.
                velocity.x = 2f * pDir;
                trail.Clear();
                var c = m.Center;
                for (int i = 0; i < 200; i++)
                {
                    c -= velocity;
                    trail.Add(c);
                }
            }
            if (!(dist < 120f))
            {
                float target = Mathf.Atan2(pCenter.y - m.Center.y, pCenter.x - m.Center.x);
                float f = AngleTowards(Mathf.Atan2(velocity.y, velocity.x), target, 5f * Mathf.Deg2Rad);
                velocity = new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * speed;
            }
            if (velocity.magnitude > speed) velocity = velocity.normalized * speed;
            if (Mathf.Abs(velocity.y) < 1f) velocity.y -= 0.1f;
            m.Rot = Mathf.Atan2(velocity.y, velocity.x) + Mathf.PI / 2f;
            m.Dir = m.SpriteDir = velocity.x > 0f ? 1 : -1;
            trail.Insert(0, m.Center);
            if (trail.Count > 200) trail.RemoveAt(trail.Count - 1);
        }

        // ------------------------------------------------------------------ Chester

        public static int DevChesterOpens, DevChesterCloses;

        /// <summary>The Eye Bone's Chester out, if any.</summary>
        M Chester => pets[0] != null && pets[0].Pet.Id == 960 ? pets[0] : null;

        /// <summary>Chester_IsAnyPlayerTrackingThisProjectile: the Piggy Bank open through this Chester.</summary>
        bool ChesterTracked(M m) => m != null && m == Chester && Chests.OpenWith == ItemType.ChesterPetItem;

        /// <summary>
        /// Player.IsProjectileInteractableAndInInteractionRange with TileReachCheckSettings.Simple: the tile of the point of
        /// Chester's box nearest the player's middle within 5 tiles across and 4 up or down of the tiles he stands in
        /// (Player.tileRangeX / tileRangeY).
        /// </summary>
        bool ChesterInReach(M m)
        {
            var b = m.Box;
            var near = new Vector2(Mathf.Clamp(pCenter.x, b.xMin, b.xMax), Mathf.Clamp(pCenter.y, b.yMin, b.yMax));
            int x = TileOf(near.x), y = TileOf(near.y);
            int lx = TileOf(pBox.xMin) - 5, hx = Mathf.CeilToInt(pBox.xMax / 16f) - 1 + 5;
            int ly = TileOf(pBox.yMin) - 4, hy = Mathf.CeilToInt(pBox.yMax / 16f) - 1 + 4;
            return x >= lx && x <= hx && y >= ly && y <= hy;
        }

        /// <summary>
        /// Main.TryInteractingWithMoneyTrough for Chester: a right click on him in reach shuts the Piggy Bank if it is open
        /// (player.chest == -2), else opens it on him (the player's own bank, Inventory.BankOf(0)), with his own sounds.
        /// </summary>
        public static bool TryChester(Vector2 mouseWorld) => Instance != null && Instance.ChesterClick(TerraParticles.ToPx(mouseWorld));

        bool ChesterClick(Vector2 mouse)
        {
            RefreshOwner();
            var m = Chester;
            if (!hasOwner || m == null || ownerDead || !m.Box.Contains(mouse) || !ChesterInReach(m)) return false;
            if (Chests.OpenBankKind == 0)
            {
                Chests.Close(false);
                Sound("Custom/chester_close_*2");
                DevChesterCloses++;
                return true;
            }
            Chests.TogglePortable(0, ItemType.ChesterPetItem);
            if (Chests.OpenWith == ItemType.ChesterPetItem)
            {
                Sound("Custom/chester_open_*2");
                DevChesterOpens++;
            }
            return true;
        }

        /// <summary>Dev: the right click on Chester (no cursor): the same as a click on his middle.</summary>
        public bool DevClickChester()
        {
            RefreshOwner();
            var m = Chester;
            return m != null && ChesterClick(m.Center);
        }

        /// <summary>Dev: Chester's frame and place, and whether the bank is open through him.</summary>
        public static (int frame, Vector2 at, bool open)? DevChester()
        {
            var m = Instance?.Chester;
            if (m == null) return null;
            return (m.Frame, m.Center, Instance.ChesterTracked(m));
        }

        /// <summary>
        /// Player.HandleBeingInChestRange (Pl:31244-31280): the bank opened on Chester shuts once he is gone or out of the
        /// player's reach, with his closing sound.
        /// </summary>
        void ChesterTick()
        {
            if (Chests.OpenWith != ItemType.ChesterPetItem) return;
            var m = Chester;
            if (m != null && !ownerDead && ChesterInReach(m)) return;
            Chests.Close(false);
            Sound("Custom/chester_close_*2");
            DevChesterCloses++;
        }
    }
}
